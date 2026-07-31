using UnityEngine;

/// <summary>
/// Snap de coordenadas para a grade de pixels do personagem (PPU 64).
/// Câmera e player DEVEM usar a mesma unidade (SpriteUnit) — misturar com
/// UnitsPerScreenPixel causa shimmer/tremor no tilemap.
/// </summary>
public static class PixelSnap2D
{
    public const float SpritePixelsPerUnit = 64f;
    public static float SpriteUnit => 1f / SpritePixelsPerUnit;

    public static float Snap(float value, float unit)
    {
        if (unit <= 0.0000001f)
            return value;
        return Mathf.Round(value / unit) * unit;
    }

    public static Vector2 Snap(Vector2 value, float unit)
    {
        return new Vector2(Snap(value.x, unit), Snap(value.y, unit));
    }

    public static Vector3 Snap(Vector3 value, float unit)
    {
        return new Vector3(Snap(value.x, unit), Snap(value.y, unit), value.z);
    }

    public static float UnitsPerScreenPixel(Camera camera)
    {
        if (camera == null || !camera.orthographic)
            return SpriteUnit;
        return (camera.orthographicSize * 2f) / Mathf.Max(1, camera.pixelHeight);
    }
}
