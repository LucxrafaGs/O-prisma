#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Popula / conserta World/Garden_Interact.
/// Nunca mexe em árvores (SeasonalTree) nem nas posições ao só corrigir materiais.
/// </summary>
public static class GardenInteractSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefKey = "Prisma_GardenInteractSetupVersion";
    private const int SetupVersion = 2;
    private const string GardenName = "Garden_Interact";

    private static readonly Vector2[] DemoPositions =
    {
        new(-1.2f, 0.4f),
        new(-0.4f, 0.9f),
        new(0.5f, 0.3f),
        new(1.3f, 0.8f),
        new(2.1f, 0.2f),
        new(-1.8f, -0.6f),
        new(-0.7f, -1.1f),
        new(0.2f, -0.5f),
        new(1.1f, -1.0f),
        new(2.0f, -0.4f),
        new(-2.4f, 1.2f),
        new(2.8f, 1.0f),
        new(-2.6f, -1.4f),
        new(2.6f, -1.5f),
        new(0.0f, 1.6f),
        new(-1.0f, 1.8f),
        new(1.5f, 1.7f),
        new(-0.2f, -1.8f)
    };

    [InitializeOnLoadMethod]
    private static void AutoFixWhenSampleSceneOpen()
    {
        // Disabled: never auto-save SampleScene on open.
    }

    [MenuItem("Prisma/Fix Garden Materials (keep positions)")]
    public static void FixGardenMenu()
    {
        int fixedCount = FixExistingGardenKeepPositions(save: true);
        Debug.Log($"Prisma: {fixedCount} plantas corrigidas (material + colisão). Posições preservadas.");
    }

    [MenuItem("Prisma/Setup Garden Interact")]
    public static void SetupGardenMenu()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        if (PopulateOrFixGarden(save: true))
        {
            EditorPrefs.SetInt(PrefKey, SetupVersion);
            Debug.Log("Prisma: Garden_Interact pronto.");
        }
    }

    /// <summary>Usado pelo rebuild após perda da cena.</summary>
    public static bool PopulateOrFixGarden(bool save)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
            return false;

        Transform garden = FindGarden();
        if (garden != null && garden.GetComponentsInChildren<ElasticFoliage>(true).Length > 0)
        {
            FixExistingGardenKeepPositions(save);
            return true;
        }

        return PopulateGarden(save);
    }

    /// <summary>
    /// Corrige material rosa e colisões sem mover nada.
    /// </summary>
    public static int FixExistingGardenKeepPositions(bool save)
    {
        ElasticFoliage[] plants = Object.FindObjectsByType<ElasticFoliage>();
        if (plants.Length == 0)
            return 0;

        for (int i = 0; i < plants.Length; i++)
        {
            ElasticFoliage plant = plants[i];
            if (plant == null)
                continue;

            // Infer plant id from name if missing (Plant_mini_tree).
            if (string.IsNullOrEmpty(plant.PlantTypeId) && plant.name.StartsWith("Plant_"))
            {
                string id = plant.name["Plant_".Length..];
                // Serialized field via Configure path — use refresh which reads name.
            }

            plant.RefreshPresentationKeepTransform();
            EditorUtility.SetDirty(plant);
            SpriteRenderer renderer = plant.GetComponent<SpriteRenderer>();
            if (renderer != null)
                EditorUtility.SetDirty(renderer);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (save)
                EditorSceneManager.SaveScene(scene);
        }

        return plants.Length;
    }

    private static bool PopulateGarden(bool save)
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform world = FindOrCreateWorld();
        Transform garden = FindOrCreateGarden(world);
        ClearGardenChildrenOnly(garden);

        Dictionary<int, Sprite> sprites = LoadCampingSprites();
        if (sprites.Count == 0)
        {
            Debug.LogError("Prisma: não foi possível carregar sprites de 11_Camping_16x16.");
            return false;
        }

        GardenInteractCatalog.PlantType[] types = GardenInteractCatalog.Types;
        for (int i = 0; i < DemoPositions.Length; i++)
        {
            GardenInteractCatalog.PlantType type = types[i % types.Length];
            if (!sprites.TryGetValue(type.SpriteIndex, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"Prisma: sprite {type.SpriteIndex} ausente para '{type.Id}'.");
                continue;
            }

            CreatePlant(garden, type, sprite, DemoPositions[i]);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (save)
            EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static Transform FindGarden()
    {
        GameObject existing = GameObject.Find(GardenName);
        return existing != null ? existing.transform : null;
    }

    private static Transform FindOrCreateWorld()
    {
        GameObject worldObject = GameObject.Find("World");
        if (worldObject == null)
            worldObject = new GameObject("World");
        return worldObject.transform;
    }

    private static Transform FindOrCreateGarden(Transform world)
    {
        Transform garden = world.Find(GardenName);
        if (garden != null)
            return garden;

        GameObject existing = GameObject.Find(GardenName);
        if (existing != null)
        {
            existing.transform.SetParent(world, true);
            return existing.transform;
        }

        GameObject gardenObject = new(GardenName);
        gardenObject.transform.SetParent(world, false);
        gardenObject.transform.localPosition = Vector3.zero;
        return gardenObject.transform;
    }

    private static void ClearGardenChildrenOnly(Transform garden)
    {
        for (int i = garden.childCount - 1; i >= 0; i--)
        {
            Transform child = garden.GetChild(i);
            if (child.GetComponent<SeasonalTree>() != null)
                continue;

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Dictionary<int, Sprite> LoadCampingSprites()
    {
        Dictionary<int, Sprite> map = new();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(GardenInteractCatalog.SheetAssetPath);
        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
                continue;

            if (!sprite.name.StartsWith(GardenInteractCatalog.SpritePrefix))
                continue;

            string suffix = sprite.name[GardenInteractCatalog.SpritePrefix.Length..];
            if (int.TryParse(suffix, out int index))
                map[index] = sprite;
        }

        return map;
    }

    private static void CreatePlant(
        Transform garden,
        GardenInteractCatalog.PlantType type,
        Sprite sprite,
        Vector2 localPosition)
    {
        GameObject plantObject = new($"Plant_{type.Id}");
        plantObject.transform.SetParent(garden, false);

        SpriteRenderer renderer = plantObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;

        ElasticFoliage foliage = plantObject.AddComponent<ElasticFoliage>();
        foliage.Configure(type, sprite);

        Bounds bounds = sprite.bounds;
        plantObject.transform.localPosition = new Vector3(
            localPosition.x - bounds.center.x,
            localPosition.y - bounds.min.y,
            0f);

        EditorUtility.SetDirty(plantObject);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(foliage);
    }
}
#endif
