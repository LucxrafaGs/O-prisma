#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Restaura tilemaps para Default (evita mapa preto sem luz na layer Ground).
/// </summary>
public static class WorldGroundSortingSetup
{
    private const string RequestPath = "Library/prisma-fix-ground-sorting.request";

    [InitializeOnLoadMethod]
    private static void AutoProcess()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool requested = System.IO.File.Exists(RequestPath);
            if (requested)
            {
                try { System.IO.File.Delete(RequestPath); }
                catch { return; }
            }

            // Sempre corrige mapa preto se tilemaps saíram da Default.
            RestoreTilemapsToDefault();
            EnsureLightsTargetAllLayers();
        };
    }

    [MenuItem("Prisma/Restore Tilemaps To Default Sorting")]
    public static void FixMenu()
    {
        int count = RestoreTilemapsToDefault();
        EnsureLightsTargetAllLayers();
        AssetDatabase.SaveAssets();
        Debug.Log($"Prisma: {count} tilemaps → Default. Luzes atualizadas. Mapa deve voltar a aparecer.");
    }

    private static int RestoreTilemapsToDefault()
    {
        int count = 0;
        TilemapRenderer[] maps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < maps.Length; i++)
        {
            TilemapRenderer renderer = maps[i];
            if (renderer == null || renderer.sortingLayerID == 0)
                continue;

            Undo.RecordObject(renderer, "Restore Default Sorting Layer");
            renderer.sortingLayerName = "Default";
            EditorUtility.SetDirty(renderer);
            count++;
        }

        if (count > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        return count;
    }

    private static void EnsureLightsTargetAllLayers()
    {
        SortingLayer[] layers = SortingLayer.layers;
        int[] ids = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            ids[i] = layers[i].id;

        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
                continue;
            Undo.RecordObject(lights[i], "Light sorting layers");
            lights[i].targetSortingLayers = ids;
            EditorUtility.SetDirty(lights[i]);
        }
    }
}
#endif
