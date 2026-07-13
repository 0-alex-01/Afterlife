using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Erzeugt einmalig Resources-Prefabs aus den drei Atmosphärenobjekten der
/// Naturwelt-Quellscene. Die Quellscene und aktuell geöffnete Scenes bleiben unverändert.
/// </summary>
[InitializeOnLoad]
internal static class WorldAtmospherePrefabBuilder
{
    private const string SourceScenePath = "Assets/Project Folder/Naturwelt Eben 1.unity";
    private const string ResourcesFolder = "Assets/Resources";
    private const string OutputFolder = "Assets/Resources/AfterlifeAtmospheres";

    private static readonly string[] AtmosphereNames =
    {
        "Atmosphere_Chaos",
        "Atmosphere_Harmonie",
        "Atmosphere_Zerfall"
    };

    private static bool buildQueued;

    static WorldAtmospherePrefabBuilder()
    {
        QueueBuild();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                QueueBuild();
        };
    }

    [MenuItem("Afterlife/Refresh World Atmosphere Prefabs")]
    private static void ForceRefresh()
    {
        BuildPrefabs(force: true);
    }

    private static void QueueBuild()
    {
        if (buildQueued)
            return;

        buildQueued = true;
        EditorApplication.delayCall += () =>
        {
            buildQueued = false;
            BuildPrefabs(force: false);
        };
    }

    private static void BuildPrefabs(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        if (!force && AtmosphereNames.All(PrefabExists))
            return;

        EnsureFolder(ResourcesFolder, "Assets", "Resources");
        EnsureFolder(OutputFolder, ResourcesFolder, "AfterlifeAtmospheres");

        Scene sourceScene = SceneManager.GetSceneByPath(SourceScenePath);
        bool openedHere = !sourceScene.isLoaded;

        try
        {
            if (openedHere)
                sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

            GameObject[] roots = sourceScene.GetRootGameObjects();

            foreach (string atmosphereName in AtmosphereNames)
            {
                GameObject sourceObject = Array.Find(
                    roots,
                    root => root.name == atmosphereName);

                if (sourceObject == null)
                    throw new InvalidOperationException(
                        $"{atmosphereName} wurde in {SourceScenePath} nicht gefunden.");

                string prefabPath = $"{OutputFolder}/{atmosphereName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(sourceObject, prefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException($"Prefab konnte nicht erstellt werden: {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Afterlife] Chaos-, Harmonie- und Zerfall-Atmosphären wurden aktualisiert.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (openedHere && sourceScene.IsValid() && sourceScene.isLoaded)
                EditorSceneManager.CloseScene(sourceScene, removeScene: true);
        }
    }

    private static bool PrefabExists(string atmosphereName)
    {
        string path = $"{OutputFolder}/{atmosphereName}.prefab";
        return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
    }

    private static void EnsureFolder(string fullPath, string parentPath, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parentPath, folderName);
    }
}
