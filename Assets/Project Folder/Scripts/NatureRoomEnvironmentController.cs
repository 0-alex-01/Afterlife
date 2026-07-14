using System.Collections;
using System.Collections.Generic;
using Autohand;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Richtet die drei eigenständigen Naturräume für VR ein. Es werden keine
/// Szenen additiv geladen: Jede Szene erhält nur ihre eigene Beleuchtung,
/// Performance-Regeln, Wolkenkante, Absturzsicherung und einen dezenten Wegweiser.
/// </summary>
[ExecuteAlways]
public sealed class NatureRoomEnvironmentController : MonoBehaviour
{
    private enum RoomProfile
    {
        Harmony,
        Chaos,
        Decay
    }

    private const string RuntimeObjectName = "[Nature Room Environment]";
    private const string EditorPreviewObjectName = "[Nature Room Scene Preview]";

    private RoomProfile profile;
    private Scene roomScene;
    private Transform playerRoot;
    private Transform guideTarget;
    private GameObject generatedRoot;
    private Vector3 boundaryCenter;
    private float boundaryRadius;
    private float safeFloorY;
    private Vector3 lastSafePlayerPosition;
    private float nextSafetyProbeTime;
    private Terrain[] loadedTerrains;
    private bool chaosCaveDisabled;
    private Transform cloudBankRoot;
    private NatureCloudAssetLibrary cloudAssets;
    private Vector3 safePathStart;
    private Vector3 safePathEnd;
    private float safePathHalfWidth;
    private bool hasSafePath;
    private bool environmentReady;
    private readonly Queue<AudioClip> voiceQueue = new Queue<AudioClip>();
    private AudioSource narratorSource;
    private Coroutine voiceQueueRoutine;
    private float voicePlaybackNotBefore;
    private bool harmonyCaveLinePlayed;
    private bool chaosRetryLinePlayed;
    private MysticalPortalController narratedChaosPortal;
    private DecayWorldEscapeController narratedDecayLoop;
    private int generatedPathSegments;
    private int generatedAntiDropRails;
    private MeshCollider naturalRampCollider;
    private float maximumRampSlope;
    private bool naturalRampCreated;
    [SerializeField, HideInInspector] private bool editorPreview;

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InstallEditorPreviewHooks()
    {
        // Editor previews are intentionally disabled. The nature scenes contain
        // hundreds of thousands of objects; scanning them from an InitializeOnLoad
        // callback can overlap OpenXR's startup reload and stall the AppDomain.
    }

    private static void HandleEditorSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += DestroyAllEditorPreviews;
    }

    private static void HandleEditorPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            DestroyAllEditorPreviews();
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += DestroyAllEditorPreviews;
    }

    [MenuItem("Afterlife/Nature Rooms/Remove Generated Scene Previews")]
    private static void RebuildEditorPreviews()
    {
        DestroyAllEditorPreviews();
        SceneView.RepaintAll();
    }

    private static void EnsureEditorPreviewsForOpenedScenes()
    {
        // Generated previews previously overlaid Harmony, Chaos and Decay when
        // scenes were open additively. Keep authored scene contents untouched.
        DestroyAllEditorPreviews();
    }

    private static void EnsureEditorPreviewForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded ||
            !TryGetProfile(scene.name, out RoomProfile sceneProfile))
        {
            return;
        }

        RemoveDecayArtifactsFromNonDecayScene(scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Editor previews are deliberately rebuilt after a script reload or
            // scene reopen. This removes stale dressing from an older profile
            // instead of carrying paths/decay fragments into another room.
            if (root.name == EditorPreviewObjectName)
                DestroyImmediate(root);
        }

        var previewObject = new GameObject(EditorPreviewObjectName)
        {
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };
        SceneManager.MoveGameObjectToScene(previewObject, scene);
        var controller = previewObject.AddComponent<NatureRoomEnvironmentController>();
        controller.editorPreview = true;
        controller.profile = sceneProfile;
        controller.roomScene = scene;
        controller.BuildEditorPreview();
    }

    private static void DestroyAllEditorPreviews()
    {
        var previewObjects = new HashSet<GameObject>();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == EditorPreviewObjectName ||
                    root.name.Contains("Scene Preview"))
                {
                    previewObjects.Add(root);
                }

                foreach (NatureRoomEnvironmentController controller in
                         root.GetComponentsInChildren<NatureRoomEnvironmentController>(true))
                {
                    if (controller.editorPreview)
                        previewObjects.Add(controller.gameObject);
                }
            }
        }

        foreach (GameObject previewObject in previewObjects)
        {
            if (previewObject != null)
                DestroyImmediate(previewObject);
        }

        if (previewObjects.Count > 0)
            SceneView.RepaintAll();
    }

    private void BuildEditorPreview()
    {
        if (Application.isPlaying || !editorPreview)
            return;

        cloudAssets = Resources.Load<NatureCloudAssetLibrary>(
            "NatureClouds/TrueCloudIslandLibrary");
        playerRoot = FindPlayerRoot();
        guideTarget = FindGuideTarget();
        CalculateBoundary();

        generatedRoot = new GameObject("Generated Nature Room Dressing (Scene Preview)");
        generatedRoot.transform.SetParent(transform, false);
        CreateTrueCloudIslandRing();

        // Runtime-only gameplay objects must never be part of the editor preview.
        // Otherwise additive scene editing makes decay monoliths and invisible
        // anti-drop walls appear in Harmony/Chaos again after every domain reload.

        MarkPreviewHierarchyDontSave(transform);
        environmentReady = true;
    }

    private static void MarkPreviewHierarchyDontSave(Transform root)
    {
        HideFlags flags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.hideFlags = flags;

            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null &&
                !EditorUtility.IsPersistent(meshFilter.sharedMesh))
            {
                meshFilter.sharedMesh.hideFlags = flags;
            }

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && !EditorUtility.IsPersistent(material))
                    material.hideFlags = flags;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!editorPreview || boundaryRadius <= 0f)
            return;

        Handles.color = new Color(0.28f, 0.92f, 1f, 0.72f);
        Handles.DrawWireDisc(boundaryCenter, Vector3.up, boundaryRadius);
        Handles.Label(
            boundaryCenter + Vector3.up * 2f,
            $"{profile} – Wolken-/Anti-Drop-Grenze ({boundaryRadius:0} m)");

        if (hasSafePath)
        {
            Handles.color = new Color(0.3f, 1f, 0.55f, 0.72f);
            Handles.DrawLine(safePathStart, safePathEnd, 3f);
        }
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForCurrentScene()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            EnsureForScene(scene);
    }

    private static void EnsureForScene(Scene scene)
    {
        if (!TryGetProfile(scene.name, out RoomProfile sceneProfile))
            return;

        RemoveDecayArtifactsFromNonDecayScene(scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RuntimeObjectName)
                return;
        }

        var runtimeObject = new GameObject(RuntimeObjectName);
        SceneManager.MoveGameObjectToScene(runtimeObject, scene);
        var controller = runtimeObject.AddComponent<NatureRoomEnvironmentController>();
        controller.profile = sceneProfile;
        controller.roomScene = scene;

        if (sceneProfile == RoomProfile.Chaos)
            controller.DisableChaosCavePortal();
    }

    private static bool TryGetProfile(string sceneName, out RoomProfile sceneProfile)
    {
        switch (sceneName)
        {
            case "lakeScene":
                sceneProfile = RoomProfile.Harmony;
                return true;
            case "Chaos 1":
                sceneProfile = RoomProfile.Chaos;
                return true;
            case "Vergänglichkeit":
                sceneProfile = RoomProfile.Decay;
                return true;
            default:
                sceneProfile = default;
                return false;
        }
    }

    private static void RemoveDecayArtifactsFromNonDecayScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name == "Vergänglichkeit")
            return;

        var artifacts = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsDecayArtifact(candidate))
                    continue;

                bool coveredByParent = false;
                for (Transform parent = candidate.parent; parent != null; parent = parent.parent)
                {
                    if (IsDecayArtifact(parent))
                    {
                        coveredByParent = true;
                        break;
                    }
                }

                if (!coveredByParent)
                    artifacts.Add(candidate.gameObject);
            }
        }

        foreach (GameObject artifact in artifacts)
        {
            if (artifact == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(artifact);
#if UNITY_EDITOR
            else
                Object.DestroyImmediate(artifact);
#endif
        }

        if (artifacts.Count > 0)
        {
            Debug.Log(
                $"[Nature Room] {scene.name}: {artifacts.Count} falsch einsortierte " +
                "Verwesungs-Hierarchien entfernt.");
        }
    }

    private static bool IsDecayArtifact(Transform candidate)
    {
        if (candidate == null)
            return false;

        if (candidate.GetComponent<DecayWorldEscapeController>() != null)
            return true;

        string objectName = candidate.name;
        return objectName == "Verwesung - Memory Against Decay" ||
               objectName == "Field of Broken Time" ||
               objectName.StartsWith("Decaying Time Monolith", System.StringComparison.Ordinal) ||
               objectName.StartsWith("Memory Anchor", System.StringComparison.Ordinal) ||
               objectName == "Remembered Exit Portal";
    }

    private IEnumerator Start()
    {
        if (!Application.isPlaying)
            yield break;

        roomScene = gameObject.scene;
        RemoveDecayArtifactsFromNonDecayScene(roomScene);

        // Die großen Szenen initialisieren beim Laden mehrere Prefabs. Zwei Frames
        // warten verhindert, dass wir noch nicht aktivierte Rig-/Portalobjekte erwischen.
        yield return null;
        yield return null;

        cloudAssets = Resources.Load<NatureCloudAssetLibrary>(
            "NatureClouds/TrueCloudIslandLibrary");

        if (profile == RoomProfile.Decay)
            EnsureDecayAutoHandRig();

        playerRoot = FindPlayerRoot();
        guideTarget = FindGuideTarget();

        SetupLightingAndFog();
        yield return StartCoroutine(OptimizeRoom());

        if (profile == RoomProfile.Chaos)
            DisableChaosCavePortal();

        CalculateBoundary();
        generatedRoot = new GameObject("Generated Nature Room Dressing");
        generatedRoot.transform.SetParent(transform, false);

        CreateTrueCloudIslandRing();

        // Harmony and Chaos use their authored geometry. A generated horizontal
        // boundary moved the Harmony player a second time after the real spawn and
        // produced the unwanted barrier ring in Chaos. Decay keeps its safety ring.
        if (profile == RoomProfile.Decay)
            CreateInvisibleBoundary();

        if (profile == RoomProfile.Decay)
            CreateDecayWorldConcept();

        SetupVoiceLines();

        if (playerRoot != null)
        {
            lastSafePlayerPosition = playerRoot.position;
            safeFloorY = playerRoot.position.y - 14f;
        }
        else
        {
            lastSafePlayerPosition = boundaryCenter;
            safeFloorY = boundaryCenter.y - 14f;
        }

        environmentReady = true;

        if (profile == RoomProfile.Decay)
            StartCoroutine(ValidateDecayRoomSetup());
        else if (profile == RoomProfile.Harmony)
            StartCoroutine(ValidateNaturalRampSetup());

        Debug.Log(
            $"[Nature Room] {roomScene.name}: eigenständige VR-Umgebung aktiv " +
            $"(Wolkenrand {boundaryRadius:0} m, direkte Szene).",
            this);
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        // A scene can be unloaded while this frame is still finishing. Do not let
        // a stale controller query an invalid scene during direct room changes.
        if (!roomScene.IsValid() || !roomScene.isLoaded)
        {
            enabled = false;
            return;
        }

        if (!environmentReady)
            return;

        if (cloudBankRoot != null)
            cloudBankRoot.Rotate(Vector3.up, 0.16f * Time.deltaTime, Space.Self);

        if (playerRoot == null)
        {
            if (Time.unscaledTime >= nextSafetyProbeTime)
            {
                nextSafetyProbeTime = Time.unscaledTime + 1f;
                playerRoot = FindPlayerRoot();
            }

            return;
        }

        Vector3 playerPosition = playerRoot.position;

        if (profile == RoomProfile.Harmony &&
            !harmonyCaveLinePlayed &&
            guideTarget != null &&
            Vector3.Distance(playerPosition, guideTarget.position) <= 16f)
        {
            harmonyCaveLinePlayed = true;
            QueueVoice(cloudAssets != null ? cloudAssets.harmonyCaveLine : null);
        }

        Vector2 fromCenter = new Vector2(
            playerPosition.x - boundaryCenter.x,
            playerPosition.z - boundaryCenter.z);

        if (profile == RoomProfile.Decay &&
            fromCenter.magnitude > boundaryRadius - 1.5f &&
            !IsInsideSafePathCorridor(playerPosition))
        {
            Vector2 clamped = fromCenter.normalized * (boundaryRadius - 2.5f);
            MovePlayerSafely(new Vector3(
                boundaryCenter.x + clamped.x,
                Mathf.Max(playerPosition.y, lastSafePlayerPosition.y),
                boundaryCenter.z + clamped.y));
            return;
        }

        if (playerPosition.y < safeFloorY)
        {
            MovePlayerSafely(lastSafePlayerPosition + Vector3.up * 0.15f);
            return;
        }

        if (Time.unscaledTime >= nextSafetyProbeTime)
        {
            nextSafetyProbeTime = Time.unscaledTime + 0.35f;

            if (Physics.Raycast(
                    playerPosition + Vector3.up * 0.5f,
                    Vector3.down,
                    out RaycastHit hit,
                    4f,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                hit.normal.y > 0.45f)
            {
                lastSafePlayerPosition = playerPosition;
            }
        }
    }

    private Transform FindPlayerRoot()
    {
        if (!roomScene.IsValid() || !roomScene.isLoaded)
            return null;

        foreach (AutoHandPlayer autoHandPlayer in
                 Object.FindObjectsByType<AutoHandPlayer>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (autoHandPlayer.gameObject.scene == roomScene)
                return autoHandPlayer.transform.root;
        }

        foreach (GameObject root in roomScene.GetRootGameObjects())
        {
            if (root.CompareTag("Player") ||
                root.name.ToLowerInvariant().Contains("xr origin") ||
                root.name.ToLowerInvariant().Contains("autohandplayer"))
            {
                return root.transform;
            }
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null && mainCamera.gameObject.scene == roomScene
            ? mainCamera.transform.root
            : null;
    }

    private void EnsureDecayAutoHandRig()
    {
        Transform authoredSpawn = FindDecayPathSpawn();
        Transform centerIsland = FindRoomTransform("center_island");
        Vector3 spawnPosition = authoredSpawn != null
            ? authoredSpawn.position
            : Vector3.zero;
        Quaternion spawnRotation = authoredSpawn != null
            ? Quaternion.Euler(0f, authoredSpawn.eulerAngles.y, 0f)
            : Quaternion.identity;

        if (centerIsland != null)
        {
            Vector3 towardIsland = Vector3.ProjectOnPlane(
                centerIsland.position - spawnPosition,
                Vector3.up);
            if (towardIsland.sqrMagnitude > 0.01f)
                spawnRotation = Quaternion.LookRotation(towardIsland.normalized, Vector3.up);
        }

        foreach (AutoHandPlayer autoHandPlayer in
                 Object.FindObjectsByType<AutoHandPlayer>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (autoHandPlayer.gameObject.scene == roomScene &&
                autoHandPlayer.gameObject.activeInHierarchy)
            {
                PositionDecayRig(autoHandPlayer.transform.root, spawnPosition, spawnRotation);
                DisableLegacyDecayPlayer(autoHandPlayer.transform.root);
                return;
            }
        }

        if (cloudAssets == null || cloudAssets.autoHandPlayerPrefab == null)
        {
            Debug.LogError(
                "[Nature Room] AutoHandPlayer_Afterlife fehlt in der NatureCloudAssetLibrary.",
                this);
            return;
        }

        Transform previousRig = FindPlayerRoot();
        if (authoredSpawn == null && previousRig != null)
        {
            spawnPosition = previousRig.position;
            spawnRotation = Quaternion.Euler(0f, previousRig.eulerAngles.y, 0f);
        }

        GameObject autoHandRig = Instantiate(
            cloudAssets.autoHandPlayerPrefab,
            spawnPosition,
            spawnRotation);
        autoHandRig.name = "AutoHandPlayer_Afterlife [Vergänglichkeit]";
        SceneManager.MoveGameObjectToScene(autoHandRig, roomScene);
        PositionDecayRig(autoHandRig.transform, spawnPosition, spawnRotation);

        if (previousRig != null && previousRig.gameObject != autoHandRig)
            previousRig.gameObject.SetActive(false);

        DisableLegacyDecayPlayer(autoHandRig.transform);

        Debug.Log(
            "[Nature Room] Vergänglichkeit verwendet jetzt AutoHand samt F1-Noclip.",
            autoHandRig);
    }

    private Transform FindDecayPathSpawn()
    {
        foreach (GameObject root in roomScene.GetRootGameObjects())
        {
            if (root.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
                return root.transform;
        }

        foreach (GameObject root in roomScene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("decay spawn") ||
                    lowerName.Contains("vergänglichkeit spawn") ||
                    lowerName.Contains("vergaenglichkeit spawn"))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private Transform FindRoomTransform(string objectName)
    {
        foreach (GameObject root in roomScene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }
        }

        return null;
    }

    private void PositionDecayRig(
        Transform rig,
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        if (rig == null)
            return;

        CharacterController controller = rig.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        rig.SetPositionAndRotation(spawnPosition, spawnRotation);

        Rigidbody body = rig.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (controller != null)
            controller.enabled = true;
    }

    private void DisableLegacyDecayPlayer(Transform activeRig)
    {
        foreach (GameObject root in roomScene.GetRootGameObjects())
        {
            if (root.transform == activeRig)
                continue;

            if (root.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
                root.SetActive(false);
        }
    }

    private Transform FindGuideTarget()
    {
        if (profile == RoomProfile.Chaos)
        {
            SteppingStonePortalTrigger steppingTrigger =
                Object.FindFirstObjectByType<SteppingStonePortalTrigger>(FindObjectsInactive.Include);
            if (steppingTrigger != null && steppingTrigger.gameObject.scene == roomScene)
                return steppingTrigger.transform;

            MysticalPortalController portal =
                Object.FindFirstObjectByType<MysticalPortalController>(FindObjectsInactive.Include);
            if (portal != null && portal.gameObject.scene == roomScene)
                return portal.transform;
        }

        foreach (CaveWisdomTransitionTrigger caveTrigger in
                 Object.FindObjectsByType<CaveWisdomTransitionTrigger>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (caveTrigger.gameObject.scene == roomScene)
                return caveTrigger.transform;
        }

        return null;
    }

    private void SetupLightingAndFog()
    {
        Color fogColor;
        Color sunColor;
        Color ambientSky;
        Color ambientGround;
        float sunIntensity;
        float fogStart;
        float fogEnd;

        switch (profile)
        {
            case RoomProfile.Harmony:
                fogColor = new Color(0.63f, 0.74f, 0.78f);
                sunColor = new Color(1f, 0.92f, 0.76f);
                ambientSky = new Color(0.58f, 0.69f, 0.74f);
                ambientGround = new Color(0.16f, 0.22f, 0.13f);
                sunIntensity = 1.15f;
                fogStart = 42f;
                fogEnd = 175f;
                break;
            case RoomProfile.Chaos:
                fogColor = new Color(0.19f, 0.18f, 0.27f);
                sunColor = new Color(0.68f, 0.72f, 0.9f);
                ambientSky = new Color(0.24f, 0.23f, 0.34f);
                ambientGround = new Color(0.08f, 0.07f, 0.11f);
                sunIntensity = 0.78f;
                fogStart = 26f;
                fogEnd = 120f;
                break;
            default:
                fogColor = new Color(0.18f, 0.16f, 0.12f);
                sunColor = new Color(0.84f, 0.39f, 0.16f);
                ambientSky = new Color(0.2f, 0.17f, 0.14f);
                ambientGround = new Color(0.035f, 0.027f, 0.022f);
                sunIntensity = 0.68f;
                fogStart = 20f;
                fogEnd = 105f;
                break;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = profile == RoomProfile.Decay
            ? FogMode.Exponential
            : FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.fogDensity = profile == RoomProfile.Decay ? 0.0125f : 0.004f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = Color.Lerp(ambientSky, ambientGround, 0.45f);
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = profile == RoomProfile.Chaos ? 0.68f : 0.88f;

        Light primarySun = null;
        foreach (Light lightComponent in
                 Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (lightComponent.gameObject.scene != roomScene ||
                lightComponent.type != LightType.Directional)
            {
                continue;
            }

            if (primarySun == null || lightComponent.intensity > primarySun.intensity)
                primarySun = lightComponent;
        }

        if (primarySun == null)
        {
            var sunObject = new GameObject("Nature Room Sun");
            SceneManager.MoveGameObjectToScene(sunObject, roomScene);
            primarySun = sunObject.AddComponent<Light>();
            primarySun.type = LightType.Directional;
            primarySun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }

        primarySun.enabled = true;
        primarySun.color = sunColor;
        primarySun.intensity = sunIntensity;
        primarySun.shadows = LightShadows.Soft;
        primarySun.shadowStrength = profile == RoomProfile.Chaos ? 0.82f : 0.72f;
        primarySun.shadowResolution = LightShadowResolution.Medium;
        primarySun.shadowBias = 0.055f;
        primarySun.shadowNormalBias = 0.38f;
        if (profile == RoomProfile.Decay)
            primarySun.transform.rotation = Quaternion.Euler(18f, 132f, 7f);
        RenderSettings.sun = primarySun;

        foreach (Light lightComponent in
                 Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (lightComponent.gameObject.scene == roomScene &&
                lightComponent.type == LightType.Directional &&
                lightComponent != primarySun)
            {
                lightComponent.enabled = false;
            }
        }

        QualitySettings.shadowDistance = 68f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.9f);
        QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 2);
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 90;
        DynamicGI.UpdateEnvironment();
    }

    private IEnumerator OptimizeRoom()
    {
        int processedRenderers = 0;
        int disabledGrassRenderers = 0;
        foreach (Renderer renderer in
                 Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != roomScene)
                continue;

            if (IsSmallVegetation(renderer.transform))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                bool reduceThisRenderer =
                    IsGrass(renderer.transform) && !ShouldKeepGrass(renderer.transform.position);
                if (profile == RoomProfile.Decay &&
                    !ShouldKeepDecayVegetation(renderer.transform.position))
                {
                    reduceThisRenderer = true;
                }

                if (reduceThisRenderer)
                {
                    renderer.enabled = false;
                    disabledGrassRenderers++;
                }
            }

            renderer.allowOcclusionWhenDynamic = true;

            processedRenderers++;
            if (processedRenderers % 8000 == 0)
                yield return null;
        }

        loadedTerrains = Object.FindObjectsByType<Terrain>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Terrain terrain in loadedTerrains)
        {
            if (terrain.gameObject.scene != roomScene)
                continue;

            terrain.drawInstanced = true;
            terrain.detailObjectDensity = Mathf.Min(terrain.detailObjectDensity, 0.58f);
            terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, 32f);
            terrain.treeDistance = Mathf.Min(terrain.treeDistance, 170f);
            terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, 65f);
            terrain.basemapDistance = Mathf.Min(terrain.basemapDistance, 75f);
            terrain.heightmapPixelError = Mathf.Max(terrain.heightmapPixelError, 9f);
        }

        foreach (ParticleSystem particles in
                 Object.FindObjectsByType<ParticleSystem>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (particles.gameObject.scene != roomScene)
                continue;

            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = Mathf.Min(main.maxParticles, 500);
        }

        foreach (ReflectionProbe probe in
                 Object.FindObjectsByType<ReflectionProbe>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (probe.gameObject.scene == roomScene &&
                probe.mode == ReflectionProbeMode.Realtime)
            {
                probe.enabled = false;
            }
        }

        foreach (Camera cameraComponent in
                 Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cameraComponent.gameObject.scene == roomScene)
            {
                cameraComponent.farClipPlane = Mathf.Min(cameraComponent.farClipPlane, 360f);
                cameraComponent.useOcclusionCulling = true;
            }
        }

        Debug.Log(
            $"[Nature Room] {roomScene.name}: {disabledGrassRenderers} Gras-Renderer " +
            "deaktiviert; große Schatten bleiben aktiv.",
            this);
    }

    private bool ShouldKeepGrass(Vector3 position)
    {
        if (playerRoot != null && Vector3.SqrMagnitude(position - playerRoot.position) < 15f * 15f)
            return true;
        if (guideTarget != null && Vector3.SqrMagnitude(position - guideTarget.position) < 11f * 11f)
            return true;

        float keepRatio = profile == RoomProfile.Harmony ? 0.5f : 0.2f;
        float hash = Mathf.Sin(position.x * 12.9898f + position.z * 78.233f) * 43758.5453f;
        hash -= Mathf.Floor(hash);
        return hash <= keepRatio;
    }

    private bool ShouldKeepDecayVegetation(Vector3 position)
    {
        if (playerRoot != null && Vector3.SqrMagnitude(position - playerRoot.position) < 11f * 11f)
            return true;

        float hash = Mathf.Sin(position.x * 19.191f + position.z * 47.733f) * 18317.173f;
        hash -= Mathf.Floor(hash);
        return hash <= 0.11f;
    }

    private static bool IsGrass(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName.IndexOf("grass_1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("grass_4", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.Equals("grass", System.StringComparison.OrdinalIgnoreCase) ||
                objectName.IndexOf("grass patch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSmallVegetation(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("flower", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("fern", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("bush", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("small plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("ground plant", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void DisableChaosCavePortal()
    {
        if (chaosCaveDisabled)
            return;

        foreach (CaveWisdomTransitionTrigger caveTrigger in
                 Object.FindObjectsByType<CaveWisdomTransitionTrigger>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (caveTrigger.gameObject.scene != roomScene)
                continue;

            caveTrigger.enabled = false;
            foreach (Collider triggerCollider in caveTrigger.GetComponents<Collider>())
                triggerCollider.enabled = false;

            Debug.Log(
                "[Nature Room] Chaos-Höhlenportal deaktiviert; nur die Portaljagd ist aktiv.",
                caveTrigger);

            chaosCaveDisabled = true;
        }
    }

    private void CalculateBoundary()
    {
        var points = new List<Vector3>();
        if (guideTarget != null)
            points.Add(guideTarget.position);

        if (profile == RoomProfile.Chaos)
        {
            MysticalPortalController portal =
                Object.FindFirstObjectByType<MysticalPortalController>(FindObjectsInactive.Include);
            if (portal != null && portal.gameObject.scene == roomScene)
                points.AddRange(portal.GetHuntWaypointPositions());
        }

        // Der Spawn liegt bewusst vor der Insel und gehört nicht zu deren Umfang.
        // Nur ohne ein Ziel verwenden wir ihn als sicheren Fallback.
        if (points.Count == 0 && playerRoot != null)
            points.Add(playerRoot.position);

        if (points.Count == 0)
            points.Add(Vector3.zero);

        Vector3 min = points[0];
        Vector3 max = points[0];
        foreach (Vector3 point in points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        boundaryCenter = (min + max) * 0.5f;
        boundaryCenter.y = playerRoot != null ? playerRoot.position.y : boundaryCenter.y;

        float requiredRadius = 0f;
        foreach (Vector3 point in points)
        {
            requiredRadius = Mathf.Max(
                requiredRadius,
                Vector2.Distance(
                    new Vector2(boundaryCenter.x, boundaryCenter.z),
                    new Vector2(point.x, point.z)));
        }

        boundaryRadius = Mathf.Clamp(requiredRadius + 38f, 62f, 145f);
    }

    private void CreateTrueCloudIslandRing()
    {
        if (cloudAssets == null ||
            cloudAssets.cloudMeshes == null ||
            cloudAssets.cloudMeshes.Length == 0 ||
            cloudAssets.cloudMaterial == null)
        {
            Debug.LogWarning(
                "[Nature Room] TrueClouds-Bibliothek fehlt; Wolkenbank wird ausgelassen.",
                this);
            return;
        }

        cloudBankRoot = new GameObject("TrueClouds Island Bank").transform;
        cloudBankRoot.SetParent(generatedRoot.transform, false);
        cloudBankRoot.position = boundaryCenter + Vector3.down * 10.5f;

        int cloudCount = profile == RoomProfile.Decay ? 26 : 22;
        Vector3 entranceDirection = playerRoot != null
            ? Vector3.ProjectOnPlane(playerRoot.position - boundaryCenter, Vector3.up).normalized
            : Vector3.back;
        Color brightColor;
        Color shadowColor;
        switch (profile)
        {
            case RoomProfile.Harmony:
                brightColor = new Color(0.88f, 0.94f, 0.98f, 1f);
                shadowColor = new Color(0.42f, 0.53f, 0.61f, 1f);
                break;
            case RoomProfile.Chaos:
                brightColor = new Color(0.53f, 0.48f, 0.66f, 1f);
                shadowColor = new Color(0.14f, 0.12f, 0.22f, 1f);
                break;
            default:
                brightColor = new Color(0.55f, 0.59f, 0.48f, 1f);
                shadowColor = new Color(0.16f, 0.12f, 0.095f, 1f);
                break;
        }

        for (int index = 0; index < cloudCount; index++)
        {
            float normalized = index / (float)cloudCount;
            float angle = normalized * Mathf.PI * 2f + Mathf.Sin(index * 2.13f) * 0.12f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Am geraden Zugang bleibt eine klare Öffnung in der Wolkenbank.
            if (Vector3.Dot(outward, entranceDirection) > 0.955f)
                continue;

            // Die Meshes sind breit. Ihr Mittelpunkt muss deshalb deutlich hinter
            // dem Inselrand liegen, damit sie nicht mehr in Bäume und Weg schneiden.
            float ringRadius = boundaryRadius + 24f + Mathf.Repeat(index * 5.73f, 8f);

            var cloud = new GameObject($"TrueCloud {index + 1:00}");
            cloud.transform.SetParent(cloudBankRoot, false);
            cloud.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * ringRadius,
                Mathf.Sin(index * 1.91f) * 2.1f - 2.2f,
                Mathf.Sin(angle) * ringRadius);
            cloud.transform.localRotation = Quaternion.Euler(
                Mathf.Sin(index * 0.71f) * 8f,
                -angle * Mathf.Rad2Deg + 90f + index * 17f,
                Mathf.Cos(index * 0.53f) * 5f);
            float horizontalScale = 10f + Mathf.Repeat(index * 7.31f, 6f);
            cloud.transform.localScale = new Vector3(
                horizontalScale,
                4.2f + Mathf.Repeat(index * 3.77f, 3.1f),
                horizontalScale * Mathf.Lerp(0.72f, 1.18f, Mathf.Repeat(index * 0.37f, 1f)));

            MeshFilter meshFilter = cloud.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = cloudAssets.cloudMeshes[index % cloudAssets.cloudMeshes.Length];
            MeshRenderer meshRenderer = cloud.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = cloudAssets.cloudMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = true;

            var properties = new MaterialPropertyBlock();
            properties.SetColor("_CloudTint", brightColor);
            properties.SetColor("_CloudShadow", shadowColor);
            properties.SetFloat("_Opacity", profile == RoomProfile.Decay ? 0.78f : 0.86f);
            properties.SetFloat("_Motion", 0.1f + Mathf.Repeat(index * 0.041f, 0.09f));
            meshRenderer.SetPropertyBlock(properties);
        }
    }

    private void CreateDecayWorldConcept()
    {
        // Belt-and-suspenders guard: decay gameplay may only be generated inside
        // the dedicated Vergänglichkeit scene, even when scenes are open additively.
        if (profile != RoomProfile.Decay || roomScene.name != "Vergänglichkeit")
            return;

        var decayLoopObject = new GameObject("Verwesung - Memory Against Decay");
        decayLoopObject.transform.SetParent(generatedRoot.transform, false);
        DecayWorldEscapeController decayLoop =
            decayLoopObject.AddComponent<DecayWorldEscapeController>();
        narratedDecayLoop = decayLoop;
        Vector3 startPosition = playerRoot != null ? playerRoot.position : boundaryCenter;
        Transform centerIsland = FindRoomTransform("center_island");
        Vector3 anchorAreaCenter = centerIsland != null
            ? centerIsland.position
            : boundaryCenter;
        decayLoop.Initialize(
            playerRoot,
            startPosition,
            guideTarget,
            boundaryCenter,
            anchorAreaCenter,
            boundaryRadius,
            generatedRoot.transform,
            cloudAssets);
    }

    private void SetupVoiceLines()
    {
        if (cloudAssets == null || generatedRoot == null)
            return;

        var narratorObject = new GameObject("Nature Room Narrator");
        narratorObject.transform.SetParent(generatedRoot.transform, false);
        narratorSource = narratorObject.AddComponent<AudioSource>();
        narratorSource.playOnAwake = false;
        narratorSource.loop = false;
        narratorSource.spatialBlend = 0f;
        narratorSource.volume = 0.92f;
        narratorSource.priority = 48;
        VoiceLineReverb.Apply(narratorSource);
        voicePlaybackNotBefore = Time.time + 1.15f;

        switch (profile)
        {
            case RoomProfile.Harmony:
                QueueVoice(cloudAssets.harmonyArrivalLine);
                break;

            case RoomProfile.Chaos:
                QueueVoice(cloudAssets.chaosArrivalLine);
                narratedChaosPortal = Object.FindFirstObjectByType<MysticalPortalController>(
                    FindObjectsInactive.Include);
                if (narratedChaosPortal != null &&
                    narratedChaosPortal.gameObject.scene == roomScene)
                {
                    narratedChaosPortal.HuntActivated += HandleChaosHuntActivated;
                    narratedChaosPortal.PortalEscaped += HandleChaosPortalEscaped;
                    if (narratedChaosPortal.IsHuntActivated)
                        HandleChaosHuntActivated();
                }
                break;

            case RoomProfile.Decay:
                QueueVoice(cloudAssets.decayArrivalLine);
                if (narratedDecayLoop != null)
                {
                    narratedDecayLoop.FirstAnchorStabilized += HandleFirstDecayAnchor;
                    narratedDecayLoop.AllAnchorsStabilized += HandleAllDecayAnchors;
                }
                break;
        }
    }

    private void HandleChaosHuntActivated()
    {
        QueueVoice(cloudAssets != null ? cloudAssets.chaosMovingPortalLine : null);
    }

    private void HandleChaosPortalEscaped()
    {
        if (chaosRetryLinePlayed)
            return;

        chaosRetryLinePlayed = true;
        QueueVoice(cloudAssets != null ? cloudAssets.chaosRetryLine : null, 0.3f);
    }

    private void HandleFirstDecayAnchor()
    {
        QueueVoice(cloudAssets != null ? cloudAssets.decayFirstAnchorLine : null, 0.2f);
    }

    private void HandleAllDecayAnchors()
    {
        QueueVoice(cloudAssets != null ? cloudAssets.decayCompletionLine : null, 0.35f);
    }

    private void QueueVoice(AudioClip clip, float delay = 0f)
    {
        if (clip == null || narratorSource == null)
            return;

        if (delay > 0f)
        {
            StartCoroutine(QueueVoiceAfterDelay(clip, delay));
            return;
        }

        voiceQueue.Enqueue(clip);
        if (voiceQueueRoutine == null)
            voiceQueueRoutine = StartCoroutine(PlayVoiceQueue());
    }

    private IEnumerator QueueVoiceAfterDelay(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        QueueVoice(clip);
    }

    private IEnumerator PlayVoiceQueue()
    {
        if (Time.time < voicePlaybackNotBefore)
            yield return new WaitForSeconds(voicePlaybackNotBefore - Time.time);

        while (voiceQueue.Count > 0)
        {
            AudioClip clip = voiceQueue.Dequeue();
            if (clip == null)
                continue;

            narratorSource.clip = clip;
            narratorSource.Play();
            yield return new WaitWhile(() => narratorSource != null && narratorSource.isPlaying);
            yield return new WaitForSeconds(0.35f);
        }

        voiceQueueRoutine = null;
    }

    private void OnDestroy()
    {
        if (narratedChaosPortal != null)
        {
            narratedChaosPortal.HuntActivated -= HandleChaosHuntActivated;
            narratedChaosPortal.PortalEscaped -= HandleChaosPortalEscaped;
        }

        if (narratedDecayLoop != null)
        {
            narratedDecayLoop.FirstAnchorStabilized -= HandleFirstDecayAnchor;
            narratedDecayLoop.AllAnchorsStabilized -= HandleAllDecayAnchors;
        }
    }

    private void CreateInvisibleBoundary()
    {
        const int segmentCount = 32;
        float segmentLength = 2f * Mathf.PI * boundaryRadius / segmentCount * 1.08f;
        Vector3 entranceDirection = playerRoot != null
            ? Vector3.ProjectOnPlane(playerRoot.position - boundaryCenter, Vector3.up).normalized
            : Vector3.back;

        for (int index = 0; index < segmentCount; index++)
        {
            float angle = index * Mathf.PI * 2f / segmentCount;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Der Zugangsweg bekommt eine echte Lücke. Ohne diese würden die
            // unsichtbaren Inselwände den Spieler direkt vor dem Ziel stoppen.
            if (Vector3.Dot(outward, entranceDirection) > 0.965f)
                continue;

            var wall = new GameObject($"Cloud Barrier {index:00}");
            wall.transform.SetParent(generatedRoot.transform, false);
            wall.transform.position = boundaryCenter + outward * boundaryRadius + Vector3.up * 4f;
            wall.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.2f, 18f, segmentLength);
        }
    }

    private void CreateGuidedPath()
    {
        if (playerRoot == null || guideTarget == null)
            return;

        Vector3 start = playerRoot.position;
        Vector3 end = guideTarget.position;
        float distance = Vector3.Distance(start, end);
        if (distance < 3f)
            return;

        int segmentCount = Mathf.Clamp(Mathf.CeilToInt(distance / 3.25f), 3, 64);
        bool useNaturalRamp = profile == RoomProfile.Harmony || profile == RoomProfile.Decay;
        Material pathMaterial = useNaturalRamp
            ? CreateNaturalRampMaterial()
            : CreatePathMaterial();
        Color pathColor = GetPathColor();
        safePathStart = start;
        safePathEnd = end;
        safePathHalfWidth = 2.55f;
        hasSafePath = true;

        List<Vector3> pathPoints = BuildWalkablePathPoints(
            start,
            end,
            segmentCount,
            useNaturalRamp);

        if (useNaturalRamp)
        {
            CreateNaturalIslandRamp(pathPoints, pathMaterial);
            return;
        }

        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 segmentStart = pathPoints[index];
            Vector3 segmentEnd = pathPoints[index + 1];

            Vector3 direction = segmentEnd - segmentStart;
            float segmentLength = direction.magnitude;
            if (segmentLength < 0.05f)
                continue;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Vector3 center = (segmentStart + segmentEnd) * 0.5f;
            Vector3 right = rotation * Vector3.right;

            GameObject pathSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pathSlab.name = $"Straight Island Path {index + 1:00}";
            pathSlab.transform.SetParent(generatedRoot.transform, true);
            pathSlab.transform.SetPositionAndRotation(center, rotation);
            pathSlab.transform.localScale = new Vector3(4.4f, 0.16f, segmentLength + 0.12f);
            pathSlab.GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;
            generatedPathSegments++;

            for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Anti-Drop Rail {(sideIndex < 0 ? "Left" : "Right")} {index + 1:00}";
                rail.transform.SetParent(generatedRoot.transform, true);
                rail.transform.SetPositionAndRotation(
                    center + right * (2.32f * sideIndex) + Vector3.up * 0.42f,
                    rotation);
                rail.transform.localScale = new Vector3(0.14f, 0.22f, segmentLength + 0.16f);
                rail.GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;

                // Die sichtbare Leiste bleibt niedrig; ihr Collider reicht in VR bis
                // über Kopfhöhe und verhindert zuverlässig das seitliche Fallen.
                BoxCollider railCollider = rail.GetComponent<BoxCollider>();
                railCollider.center = new Vector3(0f, 5.3f, 0f);
                railCollider.size = new Vector3(1f, 11.6f, 1f);
                generatedAntiDropRails++;
            }

            if (index % 5 == 2)
            {
                var guideLightObject = new GameObject($"Path Glow {index + 1:00}");
                guideLightObject.transform.SetParent(generatedRoot.transform, false);
                guideLightObject.transform.position = center + Vector3.up * 0.55f;
                Light guideLight = guideLightObject.AddComponent<Light>();
                guideLight.type = LightType.Point;
                guideLight.color = pathColor;
                guideLight.intensity = 0.58f;
                guideLight.range = 4.2f;
                guideLight.shadows = LightShadows.None;
            }
        }
    }

    private List<Vector3> BuildWalkablePathPoints(
        Vector3 start,
        Vector3 end,
        int segmentCount,
        bool smoothForRamp)
    {
        var points = new List<Vector3>(segmentCount + 1);
        for (int index = 0; index <= segmentCount; index++)
        {
            Vector3 point = Vector3.Lerp(start, end, index / (float)segmentCount);
            if (TryFindGroundHeight(point, out float groundY))
                point.y = groundY + 0.12f;
            points.Add(point);
        }

        if (!smoothForRamp || points.Count < 4)
            return points;

        // Kleine Terrainspitzen werden geglättet. Danach begrenzen zwei Vorwärts-/
        // Rückwärtsläufe die Steigung, ohne am Inselanschluss eine Stufe zu erzeugen.
        for (int pass = 0; pass < 3; pass++)
        {
            float[] smoothedHeights = new float[points.Count];
            smoothedHeights[0] = points[0].y;
            smoothedHeights[points.Count - 1] = points[points.Count - 1].y;
            for (int index = 1; index < points.Count - 1; index++)
            {
                smoothedHeights[index] =
                    (points[index - 1].y + points[index].y * 2f + points[index + 1].y) * 0.25f;
            }

            for (int index = 1; index < points.Count - 1; index++)
            {
                Vector3 point = points[index];
                point.y = smoothedHeights[index];
                points[index] = point;
            }
        }

        const float maximumSlopeDegrees = 16f;
        float slopeFactor = Mathf.Tan(maximumSlopeDegrees * Mathf.Deg2Rad);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 1; index < points.Count; index++)
            {
                float horizontalDistance = Vector2.Distance(
                    new Vector2(points[index - 1].x, points[index - 1].z),
                    new Vector2(points[index].x, points[index].z));
                float maxRise = Mathf.Max(0.08f, horizontalDistance * slopeFactor);
                Vector3 point = points[index];
                point.y = Mathf.Clamp(
                    point.y,
                    points[index - 1].y - maxRise,
                    points[index - 1].y + maxRise);
                points[index] = point;
            }

            for (int index = points.Count - 2; index >= 0; index--)
            {
                float horizontalDistance = Vector2.Distance(
                    new Vector2(points[index + 1].x, points[index + 1].z),
                    new Vector2(points[index].x, points[index].z));
                float maxRise = Mathf.Max(0.08f, horizontalDistance * slopeFactor);
                Vector3 point = points[index];
                point.y = Mathf.Clamp(
                    point.y,
                    points[index + 1].y - maxRise,
                    points[index + 1].y + maxRise);
                points[index] = point;
            }
        }

        return points;
    }

    private void CreateNaturalIslandRamp(List<Vector3> points, Material rampMaterial)
    {
        if (points == null || points.Count < 2)
            return;

        const float halfWidth = 2.2f;
        int segmentCount = points.Count - 1;
        var vertices = new Vector3[points.Count * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segmentCount * 6];

        Vector3 flatForward = Vector3.ProjectOnPlane(
            points[points.Count - 1] - points[0],
            Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.01f)
            flatForward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;

        maximumRampSlope = 0f;
        float accumulatedDistance = 0f;
        for (int index = 0; index < points.Count; index++)
        {
            if (index > 0)
            {
                Vector3 segment = points[index] - points[index - 1];
                accumulatedDistance += segment.magnitude;
                float horizontal = Vector3.ProjectOnPlane(segment, Vector3.up).magnitude;
                float slope = Mathf.Atan2(Mathf.Abs(segment.y), Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg;
                maximumRampSlope = Mathf.Max(maximumRampSlope, slope);
            }

            Vector3 surfacePoint = points[index] + Vector3.up * 0.055f;
            vertices[index * 2] = surfacePoint - right * halfWidth;
            vertices[index * 2 + 1] = surfacePoint + right * halfWidth;
            uvs[index * 2] = new Vector2(0f, accumulatedDistance * 0.22f);
            uvs[index * 2 + 1] = new Vector2(1f, accumulatedDistance * 0.22f);

            if (index >= segmentCount)
                continue;

            int vertex = index * 2;
            int triangle = index * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 3;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        var rampMesh = new Mesh { name = $"Natural {profile} Island Ramp" };
        rampMesh.vertices = vertices;
        rampMesh.triangles = triangles;
        rampMesh.uv = uvs;
        rampMesh.RecalculateNormals();
        rampMesh.RecalculateBounds();

        var rampObject = new GameObject("Natural Stone Causeway - Walkable Surface");
        rampObject.transform.SetParent(generatedRoot.transform, false);
        MeshFilter meshFilter = rampObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = rampMesh;
        MeshRenderer meshRenderer = rampObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = rampMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        naturalRampCollider = rampObject.AddComponent<MeshCollider>();
        naturalRampCollider.sharedMesh = rampMesh;

        generatedPathSegments = segmentCount;
        naturalRampCreated = true;

        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 segmentStart = points[index];
            Vector3 segmentEnd = points[index + 1];
            Vector3 direction = segmentEnd - segmentStart;
            float length = direction.magnitude;
            if (length < 0.05f)
                continue;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Vector3 center = (segmentStart + segmentEnd) * 0.5f;
            Vector3 segmentRight = rotation * Vector3.right;

            for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                var barrier = new GameObject(
                    $"Natural Ramp Anti-Drop {(sideIndex < 0 ? "Left" : "Right")} {index + 1:00}");
                barrier.transform.SetParent(generatedRoot.transform, false);
                barrier.transform.SetPositionAndRotation(
                    center + segmentRight * (2.34f * sideIndex) + Vector3.up * 1.32f,
                    rotation);
                BoxCollider barrierCollider = barrier.AddComponent<BoxCollider>();
                barrierCollider.size = new Vector3(0.28f, 2.75f, length + 0.32f);
                generatedAntiDropRails++;

                CreateNaturalRampEdgeStone(
                    center,
                    segmentRight,
                    sideIndex,
                    index,
                    rampMaterial);
            }
        }
    }

    private void CreateNaturalRampEdgeStone(
        Vector3 center,
        Vector3 right,
        int sideIndex,
        int index,
        Material material)
    {
        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stone.name = $"Still Edge Stone {(sideIndex < 0 ? "L" : "R")} {index + 1:00}";
        stone.transform.SetParent(generatedRoot.transform, true);
        float variation = Mathf.Repeat(index * 0.618f + (sideIndex > 0 ? 0.27f : 0f), 1f);
        stone.transform.position =
            center + right * (2.13f * sideIndex) + Vector3.up * (0.12f + variation * 0.06f);
        stone.transform.rotation = Quaternion.Euler(
            variation * 9f - 4.5f,
            index * 47f + sideIndex * 13f,
            variation * 12f - 6f);
        stone.transform.localScale = new Vector3(
            0.58f + variation * 0.34f,
            0.22f + variation * 0.12f,
            0.72f + (1f - variation) * 0.3f);
        Collider stoneCollider = stone.GetComponent<Collider>();
        if (Application.isPlaying)
            Destroy(stoneCollider);
#if UNITY_EDITOR
        else
            DestroyImmediate(stoneCollider);
#endif
        stone.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private IEnumerator ValidateDecayRoomSetup()
    {
        // Einen Physikschritt warten, damit alle zur Laufzeit erzeugten Trigger
        // und AutoHand-Rigidbodies in PhysX registriert sind.
        yield return new WaitForFixedUpdate();

        var problems = new List<string>();
        if (playerRoot == null ||
            playerRoot.GetComponentInChildren<AutoHandPlayer>(true) == null)
        {
            problems.Add("AutoHand-Spieler fehlt");
        }

        if (guideTarget == null)
            problems.Add("Pfadziel fehlt");
        if (!ValidateNaturalRamp(out string rampReport))
            problems.Add(rampReport);

        if (narratedDecayLoop == null)
        {
            problems.Add("Anker-Controller fehlt");
        }
        else if (!narratedDecayLoop.ValidateRuntimeSetup(out string anchorReport))
        {
            problems.Add(anchorReport);
        }

        if (problems.Count == 0)
        {
            Debug.Log(
                $"[Vergänglichkeit Test] OK: durchgehende Naturrampe, " +
                $"max. {maximumRampSlope:0.0}° Steigung, {generatedAntiDropRails} " +
                "Anti-Drop-Barrieren, AutoHand und 3 Anker bereit.",
                this);
        }
        else
        {
            Debug.LogError(
                "[Vergänglichkeit Test] FEHLER: " + string.Join("; ", problems),
                this);
        }
    }

    private IEnumerator ValidateNaturalRampSetup()
    {
        yield return new WaitForFixedUpdate();
        if (ValidateNaturalRamp(out string report))
        {
            Debug.Log(
                $"[Harmony Ramp Test] OK: durchgehender MeshCollider, " +
                $"max. {maximumRampSlope:0.0}° Steigung, {generatedAntiDropRails} " +
                "Anti-Drop-Barrieren.",
                this);
        }
        else
        {
            Debug.LogError("[Harmony Ramp Test] FEHLER: " + report, this);
        }
    }

    private bool ValidateNaturalRamp(out string report)
    {
        var problems = new List<string>();
        if (!naturalRampCreated)
            problems.Add("Naturrampe wurde nicht erzeugt");
        if (naturalRampCollider == null || naturalRampCollider.sharedMesh == null)
            problems.Add("durchgehender Rampen-MeshCollider fehlt");
        else if (!naturalRampCollider.enabled || naturalRampCollider.isTrigger)
            problems.Add("Rampen-Collider ist nicht begehbar");
        if (generatedPathSegments < 3)
            problems.Add($"nur {generatedPathSegments} Rampenabschnitte");
        if (generatedAntiDropRails != generatedPathSegments * 2)
        {
            problems.Add(
                $"Anti-Drop unvollständig ({generatedAntiDropRails}/{generatedPathSegments * 2})");
        }
        if (maximumRampSlope > 17f)
            problems.Add($"Steigung zu hoch ({maximumRampSlope:0.0}°)");

        report = problems.Count == 0
            ? "Naturrampe ist VR-begehbar"
            : string.Join("; ", problems);
        return problems.Count == 0;
    }

    private bool IsInsideSafePathCorridor(Vector3 worldPosition)
    {
        if (!hasSafePath)
            return false;

        Vector2 start = new Vector2(safePathStart.x, safePathStart.z);
        Vector2 end = new Vector2(safePathEnd.x, safePathEnd.z);
        Vector2 point = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 line = end - start;
        float lengthSquared = line.sqrMagnitude;
        if (lengthSquared < 0.01f)
            return false;

        float t = Mathf.Clamp01(Vector2.Dot(point - start, line) / lengthSquared);
        Vector2 closest = start + line * t;
        return Vector2.Distance(point, closest) <= safePathHalfWidth;
    }

    private Material CreatePathMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Hidden/InternalErrorShader");

        var material = new Material(shader)
        {
            name = $"Nature Path {profile}",
            enableInstancing = true
        };

        Color pathColor = GetPathColor();
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.Lerp(pathColor, Color.black, 0.48f));
        else
            material.color = Color.Lerp(pathColor, Color.black, 0.48f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", pathColor * 1.4f);
        }

        return material;
    }

    private Material CreateNaturalRampMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Hidden/InternalErrorShader");

        Color earthColor = profile == RoomProfile.Harmony
            ? new Color(0.3f, 0.255f, 0.17f, 1f)
            : new Color(0.18f, 0.115f, 0.075f, 1f);
        var material = new Material(shader)
        {
            name = $"Natural Earth Ramp {profile}",
            enableInstancing = true
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", earthColor);
        else
            material.color = earthColor;
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        return material;
    }

    private bool TryFindGroundHeight(Vector3 position, out float groundY)
    {
        if (loadedTerrains == null)
        {
            loadedTerrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        foreach (Terrain terrain in loadedTerrains)
        {
            if (terrain.gameObject.scene != roomScene || terrain.terrainData == null)
                continue;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            if (position.x < terrainPosition.x || position.x > terrainPosition.x + terrainSize.x ||
                position.z < terrainPosition.z || position.z > terrainPosition.z + terrainSize.z)
            {
                continue;
            }

            groundY = terrain.SampleHeight(position) + terrainPosition.y;
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * 45f,
            Vector3.down,
            100f,
            ~0,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.62f || ShouldIgnoreAsPathGround(hit.collider.transform))
                continue;

            groundY = hit.point.y;
            return true;
        }

        groundY = position.y;
        return false;
    }

    private static bool ShouldIgnoreAsPathGround(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("tree") ||
                lowerName.Contains("leaf") ||
                lowerName.Contains("branch") ||
                lowerName.Contains("animal") ||
                lowerName.Contains("deer") ||
                lowerName.Contains("water") ||
                lowerName.Contains("portal"))
            {
                return true;
            }
        }

        return false;
    }

    private Color GetPathColor()
    {
        switch (profile)
        {
            case RoomProfile.Harmony:
                return new Color(0.34f, 0.94f, 0.82f);
            case RoomProfile.Chaos:
                return new Color(0.72f, 0.25f, 1f);
            default:
                return new Color(1f, 0.53f, 0.2f);
        }
    }

    private void MovePlayerSafely(Vector3 targetPosition)
    {
        CharacterController controller = playerRoot.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        playerRoot.position = targetPosition;

        Rigidbody body = playerRoot.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (controller != null)
            controller.enabled = true;
    }
}
