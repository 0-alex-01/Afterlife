using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 3);
        private static readonly float TestTimeout = SessionState.GetFloat("PlayModeTest.TestTimeout", 20.0f);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 50;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle": break;
                case "WaitingForCompile":
                    Debug.Log("[PlayModeTest] Bootstrap compiled. Scheduling Play Mode entry.");
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;
                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;
                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                        EditorApplication.update += WaitFramesThenRun;
                    break;
                case "Done":
                    Debug.Log(SentinelLog);
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _setupDone = false;
        private static bool _testDone = false;
        private static double _testStartTime = 0;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;
            if (_testDone) return;

            if (!_setupDone)
            {
                _setupDone = true;
                Application.logMessageReceived += OnLogMessage;
                _testStartTime = EditorApplication.timeSinceStartup;
                try { Setup(); }
                catch (System.Exception e) { Debug.LogError("[PlayModeTest] Setup threw: " + e); FinishTest(true, e.Message); }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try
            {
                bool complete = Tick(elapsed);
                if (complete || timedOut) FinishTest(timedOut && !complete, timedOut ? "Timed out" : null);
            }
            catch (System.Exception e) { Debug.LogError("[PlayModeTest] Tick threw: " + e); FinishTest(true, e.Message); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            string resultJson;
            try { resultJson = GetResult(); }
            catch (System.Exception e) { resultJson = JsonUtility.ToJson(new TestResult { success = false, error = "GetResult threw: " + e.Message, logs = _capturedLogs.ToArray() }); }
            if (isError && errorMessage != null)
                resultJson = JsonUtility.ToJson(new TestResult { success = false, error = errorMessage, logs = _capturedLogs.ToArray() });
            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            if (type == LogType.Error || type == LogType.Exception || message.Contains("[Test]") || message.Contains("TEST_RESULT"))
                _capturedLogs.Add("[" + type + "] " + message);
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
                AssetDatabase.DeleteAsset(scriptPath);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public bool sourceFound;
            public bool everPlayed;
            public float maxObservedTime;
            public string clipName;
        }

        private static AudioSource _src;
        private static bool _everPlayed = false;
        private static float _maxTime = 0f;
        private static string _clipName = "";

        private static void Setup()
        {
            var bp1 = GameObject.Find("BranchPlatform_1");
            if (bp1 != null) _src = bp1.GetComponent<AudioSource>();
            if (_src != null)
            {
                _clipName = _src.clip != null ? _src.clip.name : "NULL";
                Debug.Log("[Test] Setup: source found, clip=" + _clipName);
            }
            else Debug.Log("[Test] Setup: BranchPlatform_1 AudioSource NOT found");
        }

        private static bool Tick(float elapsed)
        {
            if (_src == null) return true;
            if (_src.isPlaying)
            {
                _everPlayed = true;
                if (_src.time > _maxTime) _maxTime = _src.time;
            }
            return elapsed >= 12f; // observe long enough to catch a play + advance
        }

        private static string GetResult()
        {
            // success = played AND playback time actually advanced (real audio output)
            bool ok = _src != null && _everPlayed && _maxTime > 0.05f;
            var r = new TestResult
            {
                success = ok,
                error = ok ? null : (_src == null ? "no AudioSource" : (!_everPlayed ? "never played" : "played but time never advanced")),
                logs = _capturedLogs.ToArray(),
                sourceFound = _src != null,
                everPlayed = _everPlayed,
                maxObservedTime = _maxTime,
                clipName = _clipName
            };
            Debug.Log("TEST_RESULT " + JsonUtility.ToJson(r));
            return JsonUtility.ToJson(r);
        }
    }
}
