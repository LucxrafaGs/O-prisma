#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cria o empty World (se faltar) e instancia árvores sazonais como filhos separados.
/// Menu: Prisma → Setup World Trees
/// </summary>
public static class WorldTreesSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefKey = "Prisma_WorldTreesSetupVersion";
    private const int SetupVersion = 4;

    private static readonly Vector2[] DemoPositions =
    {
        new(-4.2f, 2.4f),
        new(-2.4f, 3.2f),
        new(-0.4f, 2.6f),
        new(1.6f, 3.4f),
        new(3.6f, 2.2f),
        new(5.0f, 0.6f),
        new(-5.0f, -0.6f),
        new(-3.0f, -2.2f),
        new(-0.6f, -3.0f),
        new(2.4f, -2.6f)
    };

    [InitializeOnLoadMethod]
    private static void AutoSetupWhenSampleSceneOpen()
    {
        // Disabled: never auto-populate / SaveScene on SampleScene open.
    }

    [MenuItem("Prisma/Setup World Trees")]
    public static void SetupWorldTreesMenu()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        if (PopulateActiveSampleScene(save: true))
        {
            EditorPrefs.SetInt(PrefKey, SetupVersion);
            Debug.Log("Prisma: árvores atualizadas (inverno fino vs grosso + colisão na sombra).");
        }
    }

    /// <summary>Usado pelo rebuild após perda da cena.</summary>
    public static bool PopulateActiveSampleScene(bool save)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SampleScenePath)
            return false;

        Transform world = FindOrCreateWorld();
        ClearExistingTrees(world);

        Dictionary<int, Sprite> sprites = LoadCampingSprites();
        if (sprites.Count == 0)
        {
            Debug.LogError("Prisma: não foi possível carregar sprites de 11_Camping_16x16.");
            return false;
        }

        NatureTreeCatalog.TreeType[] types = NatureTreeCatalog.Types;
        for (int i = 0; i < DemoPositions.Length; i++)
        {
            NatureTreeCatalog.TreeType type = types[i % types.Length];
            bool darkSummer = i % 2 == 0;
            bool yellowAutumn = i % 3 != 0;

            CreateTree(world, type, sprites, DemoPositions[i], darkSummer, yellowAutumn);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (save)
            EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static Transform FindOrCreateWorld()
    {
        GameObject worldObject = GameObject.Find("World");
        if (worldObject == null)
            worldObject = new GameObject("World");

        return worldObject.transform;
    }

    private static void ClearExistingTrees(Transform world)
    {
        for (int i = world.childCount - 1; i >= 0; i--)
        {
            Transform child = world.GetChild(i);
            if (child.GetComponent<SeasonalTree>() == null && !child.name.StartsWith("Tree_"))
                continue;

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Dictionary<int, Sprite> LoadCampingSprites()
    {
        Dictionary<int, Sprite> map = new();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(NatureTreeCatalog.SheetAssetPath);
        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
                continue;

            if (!sprite.name.StartsWith(NatureTreeCatalog.SpritePrefix))
                continue;

            string suffix = sprite.name[NatureTreeCatalog.SpritePrefix.Length..];
            if (int.TryParse(suffix, out int index))
                map[index] = sprite;
        }

        return map;
    }

    private static void CreateTree(
        Transform world,
        NatureTreeCatalog.TreeType type,
        Dictionary<int, Sprite> sprites,
        Vector2 position,
        bool darkSummer,
        bool yellowAutumn)
    {
        if (!TryGet(sprites, type.SpringLight, out Sprite spring) ||
            !TryGet(sprites, type.SummerDark, out Sprite summerDark) ||
            !TryGet(sprites, type.AutumnOrange, out Sprite autumnOrange) ||
            !TryGet(sprites, type.AutumnYellow, out Sprite autumnYellow) ||
            !TryGet(sprites, type.WinterDry, out Sprite winterDry))
        {
            Debug.LogWarning($"Prisma: sprites incompletos para árvore '{type.Id}', pulando.");
            return;
        }

        GameObject treeObject = new($"Tree_{type.DisplayName}");
        treeObject.transform.SetParent(world, false);
        treeObject.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = treeObject.AddComponent<SpriteRenderer>();
        renderer.sprite = spring;
        renderer.sortingOrder = 0;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        CharacterLitMaterial.ApplyToHierarchy(treeObject.transform);

        SeasonalTree seasonal = treeObject.AddComponent<SeasonalTree>();
        seasonal.Configure(
            type,
            spring,
            summerDark,
            autumnOrange,
            autumnYellow,
            winterDry,
            darkSummer,
            yellowAutumn);

        if (renderer.sprite != null)
        {
            Bounds bounds = renderer.sprite.bounds;
            treeObject.transform.position = new Vector3(
                position.x - bounds.center.x,
                position.y - bounds.min.y,
                0f);
        }

        EditorUtility.SetDirty(treeObject);
        EditorUtility.SetDirty(seasonal);
        EditorUtility.SetDirty(renderer);
    }

    private static bool TryGet(Dictionary<int, Sprite> sprites, int index, out Sprite sprite)
    {
        return sprites.TryGetValue(index, out sprite) && sprite != null;
    }
}
#endif
