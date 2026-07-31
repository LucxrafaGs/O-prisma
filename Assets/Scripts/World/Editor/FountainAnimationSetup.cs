#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fatiia Garden_Fountain_5_16x16 (6 frames 64x80) e liga a animação na Fonte.
/// </summary>
public static class FountainAnimationSetup
{
    public const string SheetAssetPath =
        "Assets/Assets/World/Nature/Garden/Garden_Fountain_5_16x16.png";

    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RequestPath = "Library/prisma-setup-fountain-anim.request";
    private const string PrefKey = "Prisma_FountainAnimSetupVersion";
    private const int SetupVersion = 1;
    private const int FrameWidth = 64;
    private const int FrameHeight = 80;
    private const int FrameCount = 6;
    private const float FramesPerSecond = 10f;

    [InitializeOnLoadMethod]
    private static void AutoProcess()
    {
        // Disabled auto-run: only menu / request file should mutate SampleScene.
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!File.Exists(RequestPath))
                return;

            try { File.Delete(RequestPath); }
            catch { return; }

            if (SetupFountain(saveScene: true))
                EditorPrefs.SetInt(PrefKey, SetupVersion);
        };
    }

    [MenuItem("Prisma/Setup Fountain Water Animation")]
    public static void SetupMenu()
    {
        if (SetupFountain(saveScene: true))
        {
            EditorPrefs.SetInt(PrefKey, SetupVersion);
            Debug.Log("Prisma: animação da Fonte configurada (6 frames @ 10 FPS).");
        }
    }

    public static bool SetupFountain(bool saveScene)
    {
        if (!SliceSheet())
            return false;

        Sprite[] frames = LoadFrames();
        if (frames == null || frames.Length == 0)
        {
            Debug.LogError("Prisma: não achei frames da fonte animada após o slice.");
            return false;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != SampleScenePath)
        {
            if (saveScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            active = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        GameObject fonte = GameObject.Find("Fonte");
        if (fonte == null)
        {
            fonte = new GameObject("Fonte");
            fonte.transform.position = new Vector3(0f, -1.5f, 0f);
            SpriteRenderer createdRenderer = fonte.AddComponent<SpriteRenderer>();
            createdRenderer.sortingOrder = 10;
            CircleCollider2D circle = fonte.AddComponent<CircleCollider2D>();
            circle.radius = 0.55f;
            Rigidbody2D body = fonte.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            fonte.AddComponent<CharacterDepthSort>();
            Undo.RegisterCreatedObjectUndo(fonte, "Create Fonte");
            Debug.Log("Prisma: GameObject 'Fonte' criado automaticamente.");
        }

        SpriteRenderer renderer = fonte.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Debug.LogError("Prisma: Fonte sem SpriteRenderer.");
            return false;
        }

        AnimatedSpriteLoop loop = fonte.GetComponent<AnimatedSpriteLoop>();
        if (loop == null)
            loop = fonte.AddComponent<AnimatedSpriteLoop>();

        loop.Frames = frames;
        loop.FramesPerSecond = FramesPerSecond;
        if (frames[0] != null)
            renderer.sprite = frames[0];

        SceneLitMaterial.ApplyTo(renderer);
        EditorUtility.SetDirty(loop);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(fonte);

        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
        }

        return true;
    }

    private static Sprite[] LoadFrames()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SheetAssetPath);
        List<Sprite> sprites = new(FrameCount);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        sprites.Sort((a, b) =>
        {
            int byX = a.rect.x.CompareTo(b.rect.x);
            return byX != 0 ? byX : string.CompareOrdinal(a.name, b.name);
        });
        return sprites.ToArray();
    }

    private static bool SliceSheet()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SheetAssetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Prisma: sheet não encontrada em {SheetAssetPath}");
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetAssetPath);
        if (texture == null)
        {
            Debug.LogError("Prisma: falha ao carregar textura da fonte.");
            return false;
        }

        if (texture.width < FrameWidth * FrameCount || texture.height < FrameHeight)
        {
            Debug.LogError(
                $"Prisma: sheet da fonte tem tamanho inesperado ({texture.width}x{texture.height}). " +
                $"Esperado >= {FrameWidth * FrameCount}x{FrameHeight}.");
            return false;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
        {
            Debug.LogError("Prisma: não abri Sprite Data Provider da fonte.");
            return false;
        }

        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existing = dataProvider.GetSpriteRects();
        Dictionary<string, SpriteRect> existingByName = new();
        if (existing != null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                SpriteRect spriteRect = existing[i];
                if (spriteRect != null && !string.IsNullOrEmpty(spriteRect.name))
                    existingByName[spriteRect.name] = spriteRect;
            }
        }

        bool changed = existing == null || existing.Length != FrameCount;
        SpriteRect[] rects = new SpriteRect[FrameCount];

        for (int i = 0; i < FrameCount; i++)
        {
            string spriteName = $"Garden_Fountain_5_16x16_{i}";
            Rect targetRect = new(i * FrameWidth, 0f, FrameWidth, FrameHeight);

            if (!existingByName.TryGetValue(spriteName, out SpriteRect spriteRect))
            {
                spriteRect = new SpriteRect { name = spriteName };
                changed = true;
            }

            if (spriteRect.rect != targetRect)
            {
                spriteRect.rect = targetRect;
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

        EnsureImporterSettings(importer);

        if (!changed)
            return true;

        dataProvider.SetSpriteRects(rects);
        dataProvider.Apply();
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(SheetAssetPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static void EnsureImporterSettings(TextureImporter importer)
    {
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            dirty = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16f))
        {
            importer.spritePixelsPerUnit = 16f;
            dirty = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            dirty = true;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.Tight)
        {
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            dirty = true;
        }

        if (dirty)
            importer.SaveAndReimport();
    }
}
#endif
