#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PlayerTextureImportSettings
{
    public const int TextureImportVersion = 8;

    private const string CharacterRoot = "Assets/Assets/Player";
    private const int PixelsPerUnit = 64;
    private const int MaxTextureSize = 2048;
    private const int GridColumns = 8;
    private const int GridRows = 8;
    private const int CellSize = 64;

    private static readonly string[] PlatformTargets =
    {
        "Standalone",
        "Android",
        "iPhone",
        "WebGL"
    };

    private static readonly string[] ExcludedPathFragments =
    {
        "/readme",
        "/guides/",
        "/guidelines and requirements",
        "/weapon sprites/"
    };

    [MenuItem("Prisma/Apply Player Texture Import Settings")]
    public static void ApplyAllMenu()
    {
        ApplyAll(forceReimport: true);
    }

    public static void ApplyAll(bool forceReimport = false)
    {
        string[] files = Directory.GetFiles(CharacterRoot, "*.png", SearchOption.AllDirectories);
        int reimported = 0;
        int skipped = 0;
        List<string> pathsToReimport = new();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = files[i].Replace('\\', '/');
                if (ShouldSkip(assetPath) || IsMergedCharFolder(assetPath))
                {
                    skipped++;
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (ApplySettings(importer, fileName) || forceReimport)
                    pathsToReimport.Add(assetPath);

                if (i % 25 == 0)
                    EditorUtility.DisplayProgressBar("Importando sprites do Player", assetPath, (float)i / files.Length);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        if (pathsToReimport.Count == 0)
        {
            Debug.Log(
                $"Player texture import settings OK (v{TextureImportVersion}). " +
                $"Nada a reimportar. Skipped: {skipped}, scanned: {files.Length}");
            return;
        }

        for (int i = 0; i < pathsToReimport.Count; i++)
        {
            string assetPath = pathsToReimport[i];
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            reimported++;

            if (i % 25 == 0)
                EditorUtility.DisplayProgressBar("Reimportando sprites", assetPath, (float)i / pathsToReimport.Count);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Player texture import settings applied (v{TextureImportVersion}). " +
            $"Reimported: {reimported}, skipped: {skipped}, scanned: {files.Length}");
    }

    [MenuItem("Prisma/Setup Player Character Assets")]
    public static void SetupAll()
    {
        ApplyAll(forceReimport: true);
        CharacterSpriteLibraryBuilder.BuildLibrary();
    }

    private static bool ShouldSkip(string assetPath)
    {
        string lower = assetPath.ToLowerInvariant();
        foreach (string fragment in ExcludedPathFragments)
        {
            if (lower.Contains(fragment))
                return true;
        }

        return false;
    }

    private static bool IsMergedCharFolder(string assetPath)
    {
        if (!assetPath.Contains("/Player/char_a_p1/"))
            return false;

        return !assetPath.Contains("20.") && !assetPath.Contains("21.");
    }

    private static bool ApplySettings(TextureImporter importer, string fileName)
    {
        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            changed = true;
        }

        if (ShouldUseManaSeedGrid(fileName))
            changed |= ApplyManaSeedSpriteSheet(importer, fileName);

        changed |= ApplyTextureSettings(importer);
        changed |= ApplyDefaultPlatformSettings(importer);

        foreach (string platform in PlatformTargets)
            changed |= ApplyPlatformSettings(importer, platform);

        return changed;
    }

    private static bool ShouldUseManaSeedGrid(string fileName)
    {
        return fileName.StartsWith("char_a_");
    }

    private static bool ApplyManaSeedSpriteSheet(TextureImporter importer, string baseName)
    {
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            return false;

        dataProvider.InitSpriteEditorDataProvider();
        SpriteRect[] current = dataProvider.GetSpriteRects();
        Dictionary<string, SpriteRect> existingByName = new();

        foreach (SpriteRect spriteRect in current)
        {
            if (spriteRect != null && !string.IsNullOrEmpty(spriteRect.name))
                existingByName[spriteRect.name] = spriteRect;
        }

        bool changed = current.Length != GridColumns * GridRows;
        SpriteRect[] sheet = new SpriteRect[GridColumns * GridRows];

        for (int index = 0; index < sheet.Length; index++)
        {
            int row = index / GridColumns;
            int col = index % GridColumns;
            int y = (GridRows - 1 - row) * CellSize;
            string spriteName = $"{baseName}_{index}";
            Rect targetRect = new(col * CellSize, y, CellSize, CellSize);

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

            if (spriteRect.alignment != SpriteAlignment.BottomCenter)
            {
                spriteRect.alignment = SpriteAlignment.BottomCenter;
                changed = true;
            }

            if (!Mathf.Approximately(spriteRect.pivot.x, 0.5f) || !Mathf.Approximately(spriteRect.pivot.y, 0f))
            {
                spriteRect.pivot = new Vector2(0.5f, 0f);
                changed = true;
            }

            sheet[index] = spriteRect;
        }

        if (!changed)
            return false;

        dataProvider.SetSpriteRects(sheet);
        dataProvider.Apply();
        return true;
    }

    private static bool ApplyTextureSettings(TextureImporter importer)
    {
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);

        bool changed = false;

        if (settings.textureShape != TextureImporterShape.Texture2D)
        {
            settings.textureShape = TextureImporterShape.Texture2D;
            changed = true;
        }

        if (!Mathf.Approximately(settings.spritePixelsPerUnit, PixelsPerUnit))
        {
            settings.spritePixelsPerUnit = PixelsPerUnit;
            changed = true;
        }

        if (settings.spriteMeshType != SpriteMeshType.Tight)
        {
            settings.spriteMeshType = SpriteMeshType.Tight;
            changed = true;
        }

        if (settings.spriteExtrude != 1)
        {
            settings.spriteExtrude = 1;
            changed = true;
        }

        if (!settings.spriteGenerateFallbackPhysicsShape)
        {
            settings.spriteGenerateFallbackPhysicsShape = true;
            changed = true;
        }

        if (settings.wrapMode != TextureWrapMode.Clamp)
        {
            settings.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (settings.wrapModeU != TextureWrapMode.Clamp)
        {
            settings.wrapModeU = TextureWrapMode.Clamp;
            changed = true;
        }

        if (settings.wrapModeV != TextureWrapMode.Clamp)
        {
            settings.wrapModeV = TextureWrapMode.Clamp;
            changed = true;
        }

        if (settings.wrapModeW != TextureWrapMode.Clamp)
        {
            settings.wrapModeW = TextureWrapMode.Clamp;
            changed = true;
        }

        if (settings.filterMode != FilterMode.Point)
        {
            settings.filterMode = FilterMode.Point;
            changed = true;
        }

        if (settings.aniso != 1)
        {
            settings.aniso = 1;
            changed = true;
        }

        if (settings.mipmapEnabled)
        {
            settings.mipmapEnabled = false;
            changed = true;
        }

        if (!settings.alphaIsTransparency)
        {
            settings.alphaIsTransparency = true;
            changed = true;
        }

        if (settings.npotScale != TextureImporterNPOTScale.None)
        {
            settings.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (changed)
            importer.SetTextureSettings(settings);

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        return changed;
    }

    private static bool ApplyDefaultPlatformSettings(TextureImporter importer)
    {
        TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
        return ApplyPlatformValues(importer, settings, overridePlatform: false);
    }

    private static bool ApplyPlatformSettings(TextureImporter importer, string platform)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        return ApplyPlatformValues(importer, settings, overridePlatform: true);
    }

    private static bool ApplyPlatformValues(
        TextureImporter importer,
        TextureImporterPlatformSettings settings,
        bool overridePlatform)
    {
        bool changed = false;

        if (settings.maxTextureSize != MaxTextureSize)
        {
            settings.maxTextureSize = MaxTextureSize;
            changed = true;
        }

        if (settings.textureCompression != TextureImporterCompression.Uncompressed)
        {
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (settings.crunchedCompression)
        {
            settings.crunchedCompression = false;
            changed = true;
        }

        if (overridePlatform && !settings.overridden)
        {
            settings.overridden = true;
            changed = true;
        }

        if (changed)
            importer.SetPlatformTextureSettings(settings);

        return changed;
    }
}
#endif
