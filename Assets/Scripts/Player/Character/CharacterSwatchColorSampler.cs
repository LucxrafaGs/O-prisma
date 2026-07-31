using UnityEngine;

public static class CharacterSwatchColorSampler
{
    private static readonly Color Invalid = new(0f, 0f, 0f, 0f);

    public static Color Sample(Sprite sprite, CharacterLayerType layer)
    {
        if (sprite == null || sprite.texture == null)
            return Invalid;

        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        if (rect.width < 1f || rect.height < 1f)
            return Invalid;

        int x0 = Mathf.Clamp(Mathf.FloorToInt(rect.xMin + rect.width * 0.28f), 0, texture.width - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(rect.xMax - rect.width * 0.28f), x0 + 1, texture.width);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(rect.yMin + rect.height * 0.22f), 0, texture.height - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(rect.yMax - rect.height * 0.18f), y0 + 1, texture.height);

        int sampleWidth = x1 - x0;
        int sampleHeight = y1 - y0;

        RenderTexture renderTarget = RenderTexture.GetTemporary(
            texture.width,
            texture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);

        Graphics.Blit(texture, renderTarget);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTarget;

        Texture2D readable = new Texture2D(sampleWidth, sampleHeight, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(x0, y0, sampleWidth, sampleHeight), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTarget);

        Color sampled = AverageRepresentativeColor(readable, layer);
        if (Application.isPlaying)
            Object.Destroy(readable);
        else
            Object.DestroyImmediate(readable);
        return sampled;
    }

    public static Sprite PickSampleSprite(CharacterSpriteLibrary library, CharacterSpriteLibrary.SheetEntry entry)
    {
        if (entry == null)
            return null;

        if (library != null)
        {
            Sprite[] sprites = library.GetSprites(entry.id);
            if (sprites != null && sprites.Length > 0)
            {
                Sprite idleDown = CharacterSpriteFrames.FindByFrame(sprites, 0);
                if (idleDown != null)
                    return idleDown;

                Sprite walkDown = CharacterSpriteFrames.FindByFrame(sprites, 32);
                if (walkDown != null)
                    return walkDown;
            }
        }

        return entry.referenceSprite;
    }

    private static Color AverageRepresentativeColor(Texture2D texture, CharacterLayerType layer)
    {
        Color sum = Color.black;
        float weightSum = 0f;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Color pixel = texture.GetPixel(x, y);
                if (pixel.a < 0.45f)
                    continue;

                float luminance = pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f;
                if (luminance < 0.08f || luminance > 0.97f)
                    continue;

                float saturation = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b))
                    - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                float weight = 1f + saturation * (layer == CharacterLayerType.Skin ? 2.4f : 1.6f);
                sum += pixel * weight;
                weightSum += weight;
            }
        }

        if (weightSum <= 0f)
            return Invalid;

        Color average = sum / weightSum;
        average.a = 1f;
        return average;
    }
}
