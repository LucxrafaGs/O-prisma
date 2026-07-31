#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Cria um Tilemap de água 16x16 animada atrás do mundo (milhares de tiles).
/// Não altera tilemaps/props existentes — só adiciona OceanBackground.
/// </summary>
public static class WorldOceanBackgroundSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Library/prisma-setup-ocean-background.request";
    private const string PrefKey = "Prisma_OceanBackgroundSetupVersion";
    private const int SetupVersion = 2;
    private const int FrameCount = 8;
    private const int FrameSize = 16;
    private const float FramesPerSecond = 10f;
    private const int PaddingCells = 40;

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

    [MenuItem("Prisma/Setup Ocean Background Water")]
    public static void SetupMenu()
    {
        if (Setup(saveScene: true))
        {
            EditorPrefs.SetInt(PrefKey, SetupVersion);
            Debug.Log("Prisma: Tilemap de água 16x16 animada criado atrás do mundo.");
        }
    }

    public static bool Setup(bool saveScene)
    {
        if (!SliceFillSheet())
            return false;

        Sprite[] frames = LoadFrames();
        if (frames == null || frames.Length < FrameCount)
        {
            Debug.LogError("Prisma: frames da água infinita não encontrados após o slice.");
            return false;
        }

        AnimatedTile waterTile = EnsureAnimatedTile(frames);
        if (waterTile == null)
            return false;

        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (saveScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            active = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        GameObject gridObject = GameObject.Find("Grid");
        if (gridObject == null)
        {
            Debug.LogError("Prisma: Grid não encontrado na cena.");
            return false;
        }

        // Remove versão antiga (SpriteRenderer gigante) se existir em qualquer lugar.
        RemoveLegacyOceanObjects();

        GameObject oceanObject = FindOrCreateOceanTilemap(gridObject.transform);
        Tilemap tilemap = oceanObject.GetComponent<Tilemap>();
        TilemapRenderer renderer = oceanObject.GetComponent<TilemapRenderer>();

        renderer.sortingOrder = WorldOceanBackground.SortingOrderBehindWorld;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        Material lit = SceneLitMaterial.GetLitMaterial();
        if (lit != null)
            renderer.sharedMaterial = lit;

        BoundsInt fillBounds = ComputeFillBounds(gridObject.transform, tilemap);
        FillAnimatedWater(tilemap, waterTile, fillBounds);

        if (oceanObject.GetComponent<WorldOceanBackground>() == null)
            oceanObject.AddComponent<WorldOceanBackground>();

        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(oceanObject);

        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
        }

        Debug.Log(
            $"Prisma: OceanBackground preenchido com tiles 16x16 animados " +
            $"({fillBounds.size.x}×{fillBounds.size.y} = {fillBounds.size.x * fillBounds.size.y} cells).");
        return true;
    }

    private static void RemoveLegacyOceanObjects()
    {
        WorldOceanBackground[] markers =
            Object.FindObjectsByType<WorldOceanBackground>(FindObjectsInactive.Include);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] == null)
                continue;

            GameObject go = markers[i].gameObject;
            // Mantém se já for Tilemap sob o Grid; senão apaga o legado SpriteRenderer.
            if (go.GetComponent<Tilemap>() != null && go.GetComponentInParent<Grid>() != null)
                continue;

            Object.DestroyImmediate(go);
        }

        // Caso tenha ficado um OceanBackground sem o marker.
        GameObject orphan = GameObject.Find(WorldOceanBackground.ObjectName);
        if (orphan != null && orphan.GetComponent<Tilemap>() == null)
            Object.DestroyImmediate(orphan);
    }

    private static GameObject FindOrCreateOceanTilemap(Transform gridTransform)
    {
        Transform existing = gridTransform.Find(WorldOceanBackground.ObjectName);
        if (existing != null)
        {
            GameObject go = existing.gameObject;
            if (go.GetComponent<Tilemap>() == null)
                go.AddComponent<Tilemap>();
            if (go.GetComponent<TilemapRenderer>() == null)
                go.AddComponent<TilemapRenderer>();
            // Remove componentes da abordagem antiga.
            AnimatedSpriteLoop loop = go.GetComponent<AnimatedSpriteLoop>();
            if (loop != null)
                Object.DestroyImmediate(loop);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
                Object.DestroyImmediate(sr);
            go.transform.SetAsFirstSibling();
            return go;
        }

        GameObject created = new(WorldOceanBackground.ObjectName);
        Undo.RegisterCreatedObjectUndo(created, "Create Ocean Background Tilemap");
        created.transform.SetParent(gridTransform, false);
        created.transform.localPosition = Vector3.zero;
        created.transform.localRotation = Quaternion.identity;
        created.transform.localScale = Vector3.one;
        created.transform.SetAsFirstSibling();
        created.AddComponent<Tilemap>();
        created.AddComponent<TilemapRenderer>();
        return created;
    }

    private static BoundsInt ComputeFillBounds(Transform gridTransform, Tilemap oceanTilemap)
    {
        bool any = false;
        int minX = 0, minY = 0, maxX = 0, maxY = 0;

        Tilemap[] maps = gridTransform.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null || map == oceanTilemap)
                continue;
            if (map.gameObject.name == WorldOceanBackground.ObjectName)
                continue;

            BoundsInt b = map.cellBounds;
            if (b.size.x <= 0 || b.size.y <= 0)
                continue;

            // Ignora tilemaps vazios.
            if (map.GetUsedTilesCount() == 0)
                continue;

            if (!any)
            {
                minX = b.xMin;
                minY = b.yMin;
                maxX = b.xMax;
                maxY = b.yMax;
                any = true;
            }
            else
            {
                minX = Mathf.Min(minX, b.xMin);
                minY = Mathf.Min(minY, b.yMin);
                maxX = Mathf.Max(maxX, b.xMax);
                maxY = Mathf.Max(maxY, b.yMax);
            }
        }

        if (!any)
        {
            // Fallback generoso ao redor da origem.
            return new BoundsInt(-80, -80, 0, 160, 160, 1);
        }

        minX -= PaddingCells;
        minY -= PaddingCells;
        maxX += PaddingCells;
        maxY += PaddingCells;

        return new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
    }

    private static void FillAnimatedWater(Tilemap tilemap, AnimatedTile waterTile, BoundsInt bounds)
    {
        tilemap.ClearAllTiles();
        tilemap.animationFrameRate = 1f;

        // Sincroniza animação de todos os tiles (oceano “respirando” junto).
        TileBase[] tiles = new TileBase[bounds.size.x * bounds.size.y];
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = waterTile;

        tilemap.SetTilesBlock(bounds, tiles);
        tilemap.CompressBounds();
    }

    private static AnimatedTile EnsureAnimatedTile(Sprite[] frames)
    {
        string path = WorldOceanBackground.AnimatedTilePath;
        AnimatedTile tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<AnimatedTile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.m_AnimatedSprites = frames;
        tile.m_MinSpeed = FramesPerSecond;
        tile.m_MaxSpeed = FramesPerSecond;
        tile.m_AnimationStartTime = 0f;
        tile.m_AnimationStartFrame = 0;
        tile.m_TileColliderType = Tile.ColliderType.None;
        // Todos os tiles do oceano no mesmo frame (sem dessincronizar).
        tile.m_TileAnimationFlags = TileAnimationFlags.SyncAnimation;

        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }

    private static Sprite[] LoadFrames()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(WorldOceanBackground.FillSheetPath);
        List<Sprite> sprites = new(FrameCount);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        sprites.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));
        return sprites.ToArray();
    }

    private static bool SliceFillSheet()
    {
        string path = WorldOceanBackground.FillSheetPath;
        if (!File.Exists(path))
        {
            Debug.LogError($"Prisma: sheet não encontrada: {path}");
            return false;
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            return false;

        dataProvider.InitSpriteEditorDataProvider();
        SpriteRect[] existing = dataProvider.GetSpriteRects();
        Dictionary<string, SpriteRect> byName = new();
        if (existing != null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && !string.IsNullOrEmpty(existing[i].name))
                    byName[existing[i].name] = existing[i];
            }
        }

        bool changed = existing == null || existing.Length != FrameCount;
        SpriteRect[] rects = new SpriteRect[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            string name = $"Sea_Water_Infinite_Fill_16x16_{i}";
            Rect target = new(i * FrameSize, 0f, FrameSize, FrameSize);
            if (!byName.TryGetValue(name, out SpriteRect spriteRect))
            {
                spriteRect = new SpriteRect { name = name };
                changed = true;
            }

            if (spriteRect.rect != target)
            {
                spriteRect.rect = target;
                changed = true;
            }

            if (spriteRect.alignment != SpriteAlignment.Center)
            {
                spriteRect.alignment = SpriteAlignment.Center;
                changed = true;
            }

            if (!Mathf.Approximately(spriteRect.pivot.x, 0.5f) ||
                !Mathf.Approximately(spriteRect.pivot.y, 0.5f))
            {
                spriteRect.pivot = new Vector2(0.5f, 0.5f);
                changed = true;
            }

            rects[i] = spriteRect;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        bool settingsChanged = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            settingsChanged = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            settingsChanged = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16f))
        {
            importer.spritePixelsPerUnit = 16f;
            settingsChanged = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            settingsChanged = true;
        }

        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            settingsChanged = true;
        }

        if (changed)
        {
            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
        }

        if (changed || settingsChanged)
        {
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        return true;
    }
}
#endif
