using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aktiviert in den drei Naturwelten automatisch die passende Atmosphäre aus
/// "Naturwelt Eben 1", ohne die sehr großen Scene-Dateien neu serialisieren zu müssen.
/// </summary>
public sealed class WorldAtmosphereBootstrap : MonoBehaviour
{
    private const string HostPrefix = "[Afterlife Atmosphere] ";

    private string prefabResourcePath;
    private float sourceHeight;
    private Transform atmosphere;
    private Transform followTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // In der zusammengeführten Naturwelt setzt der FoundationLoader die
        // Atmosphären statisch an die drei Weltpositionen. Sie dürfen dort nicht
        // zusätzlich der MainCamera folgen.
        if (mode == LoadSceneMode.Additive &&
            SceneManager.GetActiveScene().name == "Naturwelt Eben 1")
        {
            return;
        }

        if (!TryGetAtmosphere(scene.name, out string resourcePath, out float sourceY))
            return;

        string hostName = HostPrefix + scene.name;
        if (GameObject.Find(hostName) != null)
            return;

        var host = new GameObject(hostName);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<WorldAtmosphereBootstrap>().Initialize(resourcePath, sourceY);
    }

    private static bool TryGetAtmosphere(string sceneName, out string resourcePath, out float sourceY)
    {
        switch (sceneName)
        {
            case "Chaos 1":
                resourcePath = "AfterlifeAtmospheres/Atmosphere_Chaos";
                sourceY = 5f;
                return true;
            case "lakeScene":
                resourcePath = "AfterlifeAtmospheres/Atmosphere_Harmonie";
                sourceY = 8f;
                return true;
            case "Vergänglichkeit":
                resourcePath = "AfterlifeAtmospheres/Atmosphere_Zerfall";
                sourceY = 12f;
                return true;
            default:
                resourcePath = null;
                sourceY = 0f;
                return false;
        }
    }

    private void Initialize(string resourcePath, float sourceY)
    {
        prefabResourcePath = resourcePath;
        sourceHeight = sourceY;
    }

    private IEnumerator Start()
    {
        GameObject prefab = Resources.Load<GameObject>(prefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"Atmosphären-Prefab fehlt: Resources/{prefabResourcePath}", this);
            yield break;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = prefab.name;
        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        atmosphere = instance.transform;

        while (followTarget == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                followTarget = mainCamera.transform;
            else
                yield return null;
        }

        AlignWithPlayer();
    }

    private void LateUpdate()
    {
        if (atmosphere == null)
            return;

        if (followTarget == null && Camera.main != null)
            followTarget = Camera.main.transform;

        AlignWithPlayer();
    }

    private void AlignWithPlayer()
    {
        if (atmosphere == null || followTarget == null)
            return;

        // Die Quellscene ist auf etwa 1,70 m Augenhöhe abgestimmt. X/Z folgen
        // dem VR-Spieler, während der originale vertikale Abstand erhalten bleibt.
        float verticalOffsetFromEyes = sourceHeight - 1.7f;
        Vector3 playerPosition = followTarget.position;
        atmosphere.position = new Vector3(
            playerPosition.x,
            playerPosition.y + verticalOffsetFromEyes,
            playerPosition.z);
    }
}
