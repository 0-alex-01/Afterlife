using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Globaler Tastatur-Skip für End-to-End-Tests im Desktop- und VR-Build.
/// K folgt dem Standardpfad; an Verzweigungen wählen Shift/Strg die Alternativen.
/// </summary>
public sealed class GlobalSceneSkipController : MonoBehaviour
{
    private const string RuntimeObjectName = "[Global Scene Skip - K]";

    private enum SkipVariant
    {
        Default,
        Second,
        Third
    }

    private bool sceneLoadStarted;

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
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        ValidateRequiredScenes();
        Debug.Log(
            "[Scene Skip] Bereit: K = Standard, Shift+K = zweite Wahl, " +
            "Strg+K = dritte Wahl. Funktioniert auch während VR.",
            this);
    }

    private void Update()
    {
        if (sceneLoadStarted || !TryReadSkipKey(out SkipVariant variant))
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!TryGetNextScene(currentScene, variant, out string nextScene))
        {
            Debug.LogWarning(
                $"[Scene Skip] Für '{currentScene}' ist keine nächste Szene definiert.",
                this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError(
                $"[Scene Skip] Zielszene '{nextScene}' fehlt in den Build Settings.",
                this);
            return;
        }

        sceneLoadStarted = true;
        Debug.Log($"[Scene Skip] {currentScene} -> {nextScene}", this);
        StartCoroutine(LoadSceneOnNextFrame(nextScene));
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
            case "Wiedergeburt":
            case "WaageScene":
                // Nach einem Ende beginnt die Testschleife wieder beim Intro.
                nextScene = "IntroScene";
                return true;

            default:
                nextScene = null;
                return false;
        }
    }

    private IEnumerator LoadSceneOnNextFrame(string sceneName)
    {
        // Verhindert, dass derselbe Tastendruck vom neuen Input-System im
        // Zielbild noch einmal ausgewertet wird.
        yield return null;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            sceneLoadStarted = false;
            Debug.LogError($"[Scene Skip] Laden von '{sceneName}' konnte nicht gestartet werden.", this);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadStarted = false;
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
                    $"[Scene Skip] Build-Settings-Prüfung: '{sceneName}' fehlt.",
                    this);
            }
        }
    }
}
