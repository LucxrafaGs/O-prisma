using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sprites UI procedurais (nítidos, 9-slice) para botões e painéis.
/// </summary>
public static class PrismaUISprites
{
    private static Sprite roundedSoft;
    private static Sprite roundedHard;
    private static Sprite circle;
    private static Sprite white;

    public static Sprite White
    {
        get
        {
            if (white == null)
                white = CreateSolid(8, Color.white);
            return white;
        }
    }

    public static Sprite RoundedSoft
    {
        get
        {
            if (roundedSoft == null)
                roundedSoft = CreateRounded(128, 128, 28f, 1.6f);
            return roundedSoft;
        }
    }

    public static Sprite RoundedHard
    {
        get
        {
            if (roundedHard == null)
                roundedHard = CreateRounded(128, 128, 18f, 1.1f);
            return roundedHard;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
                circle = CreateRounded(64, 64, 32f, 1.2f);
            return circle;
        }
    }

    public static void ApplyRounded(Image image, bool soft = true)
    {
        if (image == null)
            return;

        image.sprite = soft ? RoundedSoft : RoundedHard;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.15f;
        image.useSpriteMesh = true;
    }

    private static Sprite CreateSolid(int size, Color color)
    {
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "PrismaUI_White",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        Color32 c = color;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c;

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateRounded(int width, int height, float radius, float feather)
    {
        Texture2D tex = new(width, height, TextureFormat.RGBA32, false)
        {
            name = $"PrismaUI_Round_{width}x{height}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];
        float r = Mathf.Max(1f, radius);
        float f = Mathf.Max(0.5f, feather);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = RoundedAlpha(x + 0.5f, y + 0.5f, width, height, r, f);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        float border = r + f + 1f;
        Vector4 slice = new(border, border, border, border);
        return Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            slice);
    }

    private static float RoundedAlpha(float x, float y, int width, int height, float radius, float feather)
    {
        float left = radius;
        float right = width - radius;
        float bottom = radius;
        float top = height - radius;

        float cx = Mathf.Clamp(x, left, right);
        float cy = Mathf.Clamp(y, bottom, top);
        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        return 1f - Mathf.SmoothStep(radius - feather, radius + feather * 0.25f, dist);
    }
}
