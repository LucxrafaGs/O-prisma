using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Garante que tilemaps de chão fiquem na Default (com luz 2D) e que
/// as luzes iluminem todas as Sorting Layers. O sumiço no norte é
/// resolvido pelo bias em <see cref="WorldDepth"/>, não por layer de chão.
/// </summary>
public static class WorldGroundSortingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply()
    {
        // Qualquer tilemap que tenha ficado numa layer inválida / Ground volta pra Default.
        TilemapRenderer[] maps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include);
        int restored = 0;
        for (int i = 0; i < maps.Length; i++)
        {
            TilemapRenderer renderer = maps[i];
            if (renderer == null)
                continue;

            if (renderer.sortingLayerID != 0)
            {
                renderer.sortingLayerName = "Default";
                restored++;
            }
        }

        EnsureLightsTargetAllSortingLayers();

        if (restored > 0)
            Debug.Log($"Prisma: {restored} tilemaps restaurados para Sorting Layer Default (mapa visível + lit).");
    }

    private static void EnsureLightsTargetAllSortingLayers()
    {
        SortingLayer[] layers = SortingLayer.layers;
        int[] ids = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            ids[i] = layers[i].id;

        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].targetSortingLayers = ids;
        }
    }
}
