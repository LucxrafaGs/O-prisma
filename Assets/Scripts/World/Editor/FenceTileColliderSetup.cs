#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Gera physics shapes justas (só pixels opacos) nas tiles de cerca/mureta,
/// para o TilemapCollider2D não bloquear "ar" acima/abaixo do sprite.
/// </summary>
public static class FenceTileColliderSetup
{
    private const string FenceSheetPath = "Assets/Assets/World/Nature/1_Terrains_and_Fences_16x16.png";
    private const string RequestPath = "Library/prisma-fix-fence-colliders.request";
    private const float AlphaThreshold = 0.15f;
    private const int InsetPixels = 1;

    [InitializeOnLoadMethod]
    private static void ProcessPendingRequest()
    {
        EditorApplication.delayCall += () =>
        {
            if (!System.IO.File.Exists(RequestPath))
                return;
            try
            {
                System.IO.File.Delete(RequestPath);
            }
            catch
            {
                return;
            }

            FixFenceColliders();
        };
    }

    [MenuItem("Prisma/Fix Fence Wall Colliders (muretas)")]
    public static void FixFenceColliders()
    {
        TextureImporter importer = AssetImporter.GetAtPath(FenceSheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Prisma: não achei a sheet em {FenceSheetPath}");
            return;
        }

        Texture2D readable = LoadReadableCopy(FenceSheetPath);
        if (readable == null)
        {
            Debug.LogError("Prisma: falha ao ler textura das cercas.");
            return;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
        {
            Object.DestroyImmediate(readable);
            Debug.LogError("Prisma: não consegui abrir o Sprite Data Provider da sheet.");
            return;
        }

        dataProvider.InitSpriteEditorDataProvider();
        ISpritePhysicsOutlineDataProvider physicsProvider =
            dataProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        if (physicsProvider == null)
        {
            Object.DestroyImmediate(readable);
            Debug.LogError("Prisma: sheet sem suporte a Physics Outline.");
            return;
        }

        SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
        if (spriteRects == null || spriteRects.Length == 0)
        {
            Object.DestroyImmediate(readable);
            Debug.LogError("Prisma: sheet sem sprites fatiados.");
            return;
        }

        int updated = 0;
        for (int i = 0; i < spriteRects.Length; i++)
        {
            SpriteRect spriteRect = spriteRects[i];
            if (!TryBuildOpaqueShape(readable, spriteRect, out Vector2[] shape))
            {
                physicsProvider.SetOutlines(spriteRect.spriteID, new List<Vector2[]>());
                continue;
            }

            physicsProvider.SetOutlines(spriteRect.spriteID, new List<Vector2[]> { shape });
            updated++;
        }

        dataProvider.Apply();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Object.DestroyImmediate(readable);

        RefreshTilemapCollidersInOpenScenes();
        Debug.Log($"Prisma: physics shapes ajustadas em {updated} sprites de cerca/mureta. Teste de novo a colisão vertical.");
    }

    private static bool TryBuildOpaqueShape(Texture2D texture, SpriteRect spriteRect, out Vector2[] shape)
    {
        shape = null;
        Rect rect = spriteRect.rect;
        int x0 = Mathf.FloorToInt(rect.x);
        int y0 = Mathf.FloorToInt(rect.y);
        int w = Mathf.FloorToInt(rect.width);
        int h = Mathf.FloorToInt(rect.height);
        if (w <= 0 || h <= 0)
            return false;

        int minX = w;
        int minY = h;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = texture.GetPixel(x0 + x, y0 + y);
                if (c.a < AlphaThreshold)
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return false;

        minX = Mathf.Clamp(minX + InsetPixels, 0, w - 1);
        minY = Mathf.Clamp(minY + InsetPixels, 0, h - 1);
        maxX = Mathf.Clamp(maxX - InsetPixels, 0, w - 1);
        maxY = Mathf.Clamp(maxY - InsetPixels, 0, h - 1);
        if (maxX <= minX || maxY <= minY)
            return false;

        // Physics Outline: espaço local do sprite, origem no pivot (pixels).
        Vector2 pivot = spriteRect.pivot;
        float originX = w * pivot.x;
        float originY = h * pivot.y;

        shape = new[]
        {
            new Vector2(minX - originX, minY - originY),
            new Vector2(maxX + 1 - originX, minY - originY),
            new Vector2(maxX + 1 - originX, maxY + 1 - originY),
            new Vector2(minX - originX, maxY + 1 - originY)
        };
        return true;
    }

    private static Texture2D LoadReadableCopy(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return null;

        bool wasReadable = importer.isReadable;
        TextureImporterCompression compression = importer.textureCompression;
        if (!wasReadable || compression != TextureImporterCompression.Uncompressed)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (source == null)
            return null;

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        if (!wasReadable || compression != TextureImporterCompression.Uncompressed)
        {
            importer.isReadable = wasReadable;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        return copy;
    }

    private static void RefreshTilemapCollidersInOpenScenes()
    {
        TilemapCollider2D[] colliders = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include);
        for (int i = 0; i < colliders.Length; i++)
        {
            TilemapCollider2D col = colliders[i];
            if (col == null)
                continue;

            col.enabled = false;
            col.enabled = true;
            EditorUtility.SetDirty(col);
        }
    }
}
#endif
