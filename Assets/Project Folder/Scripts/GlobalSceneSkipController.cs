using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Globale Tastatur-Navigation für End-to-End-Tests im Desktop- und VR-Build.
/// K folgt dem Standardpfad; an Verzweigungen wählen Shift/Strg die Alternativen.
/// J kehrt zur tatsächlich zuletzt besuchten Szene zurück.
/// </summary>
public sealed class GlobalSceneSkipController : MonoBehaviour
{
    private const string RuntimeObjectName = "[Global Scene Navigation - J/K]";
    private const int MaxHistoryEntries = 32;

    private enum SkipVariant
    {
        Default,
        Second,
        Third
    }

    private readonly List<string> sceneHistory = new List<string>();
    private bool sceneLoadStarted;
    private bool backNavigationPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        GlobalSceneSkipController existing =
            Object.FindFirstObjectByType<GlobalSceneSkipController>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        var runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<GlobalSceneSkipController>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void Start()
    {
        ValidateRequiredScenes();
        Debug.Log(
            "[Scene Navigation] Bereit: J = vorherige Welt, K = Standard, " +
            "Shift+K = zweite Wahl, Strg+K = dritte Wahl. Funktioniert auch während VR.",
            this);
    }

    private void Update()
    {
        if (sceneLoadStarted)
            return;

        if (TryReadBackKey())
        {
            TryNavigateBack();
            return;
        }

        if (!TryReadSkipKey(out SkipVariant variant))
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!TryGetNextScene(currentScene, variant, out string nextScene))
        {
            Debug.LogWarning(
                $"[Scene Navigation] Für '{currentScene}' ist keine nächste Szene definiert.",
                this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError(
                $"[Scene Navigation] Zielszene '{nextScene}' fehlt in den Build Settings.",
                this);
            return;
        }

        sceneLoadStarted = true;
        Debug.Log($"[Scene Navigation] Vorwärts: {currentScene} -> {nextScene}", this);
        StartCoroutine(LoadSceneOnNextFrame(nextScene, false));
    }

    private void TryNavigateBack()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        // Defensive Bereinigung, falls ein externer Loader dieselbe Szene erneut geladen hat.
        while (sceneHistory.Count > 0 && sceneHistory[sceneHistory.Count - 1] == currentScene)
            sceneHistory.RemoveAt(sceneHistory.Count - 1);

        if (sceneHistory.Count == 0)
        {
            Debug.LogWarning(
                $"[Scene Navigation] Vor '{currentScene}' ist in dieser Sitzung keine Szene gespeichert.",
                this);
            return;
        }

        string previousScene = sceneHistory[sceneHistory.Count - 1];
        if (!Application.CanStreamedLevelBeLoaded(previousScene))
        {
            Debug.LogError(
                $"[Scene Navigation] Vorherige Szene '{previousScene}' fehlt in den Build Settings.",
                this);
            return;
        }

        sceneLoadStarted = true;
        Debug.Log($"[Scene Navigation] Zurück: {currentScene} -> {previousScene}", this);
        StartCoroutine(LoadSceneOnNextFrame(previousScene, true));
    }

    private static bool TryReadSkipKey(out SkipVariant variant)
    {
        variant = SkipVariant.Default;
        bool pressed = false;
        bool shift = false;
        bool control = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
        {
            pressed = true;
            shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed && Input.GetKeyDown(KeyCode.K))
        {
            pressed = true;
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }
#endif

        if (!pressed)
            return false;

        variant = control
            ? SkipVariant.Third
            : shift
                ? SkipVariant.Second
                : SkipVariant.Default;
        return true;
    }

    private static bool TryReadBackKey()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        pressed = keyboard != null && keyboard.jKey.wasPressedThisFrame;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
            pressed = Input.GetKeyDown(KeyCode.J);
#endif

        return pressed;
    }

    private static bool TryGetNextScene(
        string currentScene,
        SkipVariant variant,
        out string nextScene)
    {
        switch (currentScene)
        {
            case "IntroScene":
                nextScene = "StartPortalRoom";
                return true;

            case "StartPortalRoom":
                nextScene = variant == SkipVariant.Third
                    ? "Vergänglichkeit"
                    : variant == SkipVariant.Second
                        ? "Chaos 1"
                        : "lakeScene";
                return true;

            case "lakeScene":
            case "Chaos 1":
            case "Vergänglichkeit":
                nextScene = "Tree_Scene";
                return true;

            case "Tree_Scene":
                nextScene = "CemeteryOfUnfinishedGoodbyes";
                return true;

            case "CemeteryOfUnfinishedGoodbyes":
                nextScene = "Mirrorroom";
                return true;

            case "Mirrorroom":
                nextScene = variant == SkipVariant.Third
                    ? "WaageScene"
                    : variant == SkipVariant.Second
                        ? "Wiedergeburt"
                        : "Leere";
                return true;

            case "Leere":
                nextScene = "Wiedergeburt";
                return true;

            case "Wiedergeburt":
                nextScene = "WaageScene";
                return true;

            case "WaageScene":
                // K durchläuft erst alle drei Afterlife-Stränge und beginnt danach von vorn.
                nextScene = "IntroScene";
                return true;

            default:
                nextScene = null;
                return false;
        }
    }

    private IEnumerator LoadSceneOnNextFrame(string sceneName, bool navigatingBack)
    {
        // Verhindert, dass derselbe Tastendruck vom neuen Input-System im
        // Zielbild noch einmal ausgewertet wird.
        yield return null;

        backNavigationPending = navigatingBack;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            sceneLoadStarted = false;
            backNavigationPending = false;
            Debug.LogError(
                $"[Scene Navigation] Laden von '{sceneName}' konnte nicht gestartet werden.",
                this);
            yield break;
        }

        if (navigatingBack &&
            sceneHistory.Count > 0 &&
            sceneHistory[sceneHistory.Count - 1] == sceneName)
        {
            sceneHistory.RemoveAt(sceneHistory.Count - 1);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadStarted = false;
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        sceneLoadStarted = false;

        if (backNavigationPending)
        {
            // Beim Zurückgehen die verlassene Szene nicht sofort wieder in die
            // Historie legen, sonst würde J nur zwischen zwei Szenen pendeln.
            backNavigationPending = false;
            return;
        }

        string previousName = previousScene.name;
        string nextName = nextScene.name;
        if (string.IsNullOrEmpty(previousName) ||
            previousName == nextName ||
            !Application.CanStreamedLevelBeLoaded(previousName))
        {
            return;
        }

        if (sceneHistory.Count == 0 || sceneHistory[sceneHistory.Count - 1] != previousName)
            sceneHistory.Add(previousName);

        if (sceneHistory.Count > MaxHistoryEntries)
            sceneHistory.RemoveAt(0);
    }

    private void ValidateRequiredScenes()
    {
        string[] requiredScenes =
        {
            "IntroScene",
            "StartPortalRoom",
            "lakeScene",
            "Chaos 1",
            "Vergänglichkeit",
            "Tree_Scene",
            "CemeteryOfUnfinishedGoodbyes",
            "Mirrorroom",
            "Leere",
            "Wiedergeburt",
            "WaageScene"
        };

        foreach (string sceneName in requiredScenes)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[Scene Navigation] Build-Settings-Prüfung: '{sceneName}' fehlt.",
                    this);
            }
        }
    }
}
