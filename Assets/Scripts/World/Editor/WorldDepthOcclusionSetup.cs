#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Aplica PropDepthSplit nos props e ConstructionTilemapDepthSplit nas Construções.
/// </summary>
public static class WorldDepthOcclusionSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Library/prisma-setup-depth-occlusion.request";
    private const string PrefKey = "Prisma_DepthOcclusionSetupVersion";
    private const int SetupVersion = 1;

    [InitializeOnLoadMethod]
    private static void AutoProcess()
    {
        // Only run when explicitly requested via Library/*.request — never auto-save SampleScene.
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!File.Exists(RequestPath))
                return;

            try { File.Delete(RequestPath); }
            catch { return; }

            if (Setup(saveScene: true))
                EditorPrefs.SetInt(PrefKey, SetupVersion);
        };
    }

    [MenuItem("Prisma/Setup Depth Occlusion (props + Construções)")]
    public static void SetupMenu()
    {
        if (Setup(saveScene: true))
        {
            EditorPrefs.SetInt(PrefKey, SetupVersion);
            Debug.Log("Prisma: profundidade (props + Construções) configurada.");
        }
    }

    public static bool Setup(bool saveScene)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (saveScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            active = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        int props = PropDepthSplitBootstrap.ApplyAll();
        int maps = ApplyConstructionSplits();

        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
        }

        Debug.Log($"Prisma: Depth occlusion — props={props}, construções={maps}.");
        return true;
    }

    private static int ApplyConstructionSplits()
    {
        int count = 0;
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null || !ConstructionTilemapDepthSplit.IsConstructionLayer(map.gameObject.name))
                continue;

            ConstructionTilemapDepthSplit split = map.GetComponent<ConstructionTilemapDepthSplit>();
            if (split == null)
                split = map.gameObject.AddComponent<ConstructionTilemapDepthSplit>();

            split.EnsureSplit();
            EditorUtility.SetDirty(map.gameObject);
            count++;
        }

        return count;
    }
}
#endif
