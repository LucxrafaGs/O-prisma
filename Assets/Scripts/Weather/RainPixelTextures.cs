using UnityEngine;

/// <summary>
/// Texturas pixel art geradas em runtime para chuva, respingo e névoa.
/// </summary>
public static class RainPixelTextures
{
    private static Sprite rainStreakSprite;
    private static Sprite splashSprite;
    private static Sprite mistSprite;
    private static Sprite fogSprite;
    private static Sprite cloudSprite;

    public static void ClearForDomainReload()
    {
        rainStreakSprite = null;
        splashSprite = null;
        mistSprite = null;
        fogSprite = null;
        cloudSprite = null;
    }

    public static Sprite RainStreak
    {
        get
        {
            if (rainStreakSprite == null)
                rainStreakSprite = CreateRainStreak();
            return rainStreakSprite;
        }
    }

    public static Sprite Splash
    {
        get
        {
            if (splashSprite == null)
                splashSprite = CreateSplash();
            return splashSprite;
        }
    }

    public static Sprite Mist
    {
        get
        {
            if (mistSprite == null)
                mistSprite = CreateMist();
            return mistSprite;
        }
    }

    public static Sprite Fog
    {
        get
        {
            if (fogSprite == null)
                fogSprite = CreateFog();
            return fogSprite;
        }
    }

    public static Sprite Cloud
    {
        get
        {
            if (cloudSprite == null)
                cloudSprite = CreateCloud();
            return cloudSprite;
        }
    }

    private static Sprite CreateRainStreak()
    {
        const int size = 8;
        Texture2D tex = NewTexture(size, size, FilterMode.Point);
        Color clear = new(0f, 0f, 0f, 0f);
        Color soft = new(0.82f, 0.9f, 1f, 0.35f);
        Color mid = new(0.92f, 0.96f, 1f, 0.85f);
        Color tip = new(1f, 1f, 1f, 1f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            tex.SetPixel(x, y, clear);

        for (int i = 0; i < size; i++)
        {
            int x = i;
            int y = size - 1 - i;
            tex.SetPixel(x, y, i < 2 ? tip : mid);
            if (x + 1 < size)
                tex.SetPixel(x + 1, y, soft);
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateSplash()
    {
        const int size = 12;
        Texture2D tex = NewTexture(size, size, FilterMode.Point);
        Color clear = new(0f, 0f, 0f, 0f);
        Color core = new(0.95f, 0.98f, 1f, 1f);
        Color drop = new(0.88f, 0.94f, 1f, 0.9f);
        Color ring = new(0.78f, 0.88f, 1f, 0.55f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            tex.SetPixel(x, y, clear);

        int c = size / 2;
        tex.SetPixel(c, c, core);
        tex.SetPixel(c, c + 1, drop);
        tex.SetPixel(c - 1, c, drop);
        tex.SetPixel(c + 1, c, drop);
        tex.SetPixel(c - 3, c, ring);
        tex.SetPixel(c + 3, c, ring);
        tex.SetPixel(c - 4, c + 1, ring);
        tex.SetPixel(c + 4, c + 1, ring);
        tex.SetPixel(c - 2, c + 2, ring);
        tex.SetPixel(c + 2, c + 2, ring);
        tex.SetPixel(c, c + 2, ring);
        tex.SetPixel(c - 1, c - 1, ring);
        tex.SetPixel(c + 1, c - 1, ring);

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.25f), size);
    }

    private static Sprite CreateMist()
    {
        const int size = 64;
        Texture2D tex = NewTexture(size, size, FilterMode.Bilinear);
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, 1.6f) * 0.55f;
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                a *= 0.75f + n * 0.35f;
                tex.SetPixel(x, y, new Color(0.52f, 0.55f, 0.58f, Mathf.Clamp01(a)));
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.55f);
    }

    private static Sprite CreateFog()
    {
        const int w = 128;
        const int h = 128;
        Texture2D tex = NewTexture(w, h, FilterMode.Bilinear);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;
                float ny = y / (float)h;
                float n =
                    Mathf.PerlinNoise(nx * 2.4f, ny * 1.8f) * 0.5f +
                    Mathf.PerlinNoise(nx * 5.5f + 8f, ny * 3.8f) * 0.35f +
                    Mathf.PerlinNoise(nx * 11f, ny * 9f + 3f) * 0.2f;
                float veil = 0.45f + n * 0.55f;
                float edge =
                    Mathf.SmoothStep(0f, 0.2f, ny) *
                    Mathf.SmoothStep(0f, 0.2f, 1f - ny) *
                    Mathf.SmoothStep(0f, 0.12f, nx) *
                    Mathf.SmoothStep(0f, 0.12f, 1f - nx);
                float a = Mathf.Clamp01(veil * 0.55f * Mathf.Lerp(0.65f, 1f, edge));
                tex.SetPixel(x, y, new Color(0.5f, 0.53f, 0.56f, a));
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
    }

    private static Sprite CreateCloud()
    {
        const int w = 96;
        const int h = 48;
        Texture2D tex = NewTexture(w, h, FilterMode.Bilinear);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;
                float ny = y / (float)h;
                float n =
                    Mathf.PerlinNoise(nx * 3.2f + 2f, ny * 2.6f) * 0.55f +
                    Mathf.PerlinNoise(nx * 7f, ny * 5f + 4f) * 0.45f;
                float lobe =
                    Mathf.Exp(-Mathf.Pow((nx - 0.35f) / 0.28f, 2f)) * 0.7f +
                    Mathf.Exp(-Mathf.Pow((nx - 0.62f) / 0.24f, 2f)) * 0.85f +
                    Mathf.Exp(-Mathf.Pow((nx - 0.8f) / 0.2f, 2f)) * 0.45f;
                float vertical = Mathf.Exp(-Mathf.Pow((ny - 0.5f) / 0.38f, 2f));
                float a = Mathf.Clamp01(n * lobe * vertical * 0.85f);
                tex.SetPixel(x, y, new Color(0.48f, 0.5f, 0.54f, a));
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 48f);
    }

    private static Texture2D NewTexture(int width, int height, FilterMode filter)
    {
        Texture2D tex = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
            name = "RainGen"
        };
        return tex;
    }
}
