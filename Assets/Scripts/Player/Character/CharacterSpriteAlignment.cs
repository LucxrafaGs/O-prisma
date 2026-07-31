using UnityEngine;

public static class CharacterSpriteAlignment
{
    public const float ManaSeedCellSize = 64f;

    public static Vector2 GetTextureAnchor(Sprite sprite)
    {
        if (sprite == null)
            return Vector2.zero;

        return (Vector2)sprite.rect.position + sprite.pivot;
    }

    public static Vector2 GetLayerOffsetPixels(Sprite bodySprite, Sprite layerSprite)
    {
        return GetTextureAnchor(layerSprite) - GetTextureAnchor(bodySprite);
    }

    public static Vector3 GetLayerLocalPosition(Sprite bodySprite, Sprite layerSprite, Vector3 bodyLocalPosition)
    {
        if (bodySprite == null || layerSprite == null)
            return bodyLocalPosition;

        float pixelsPerUnit = bodySprite.pixelsPerUnit;
        Vector2 offsetPixels = GetLayerOffsetPixels(bodySprite, layerSprite);

        return new Vector3(
            bodyLocalPosition.x + offsetPixels.x / pixelsPerUnit,
            bodyLocalPosition.y + offsetPixels.y / pixelsPerUnit,
            bodyLocalPosition.z);
    }

    public static bool IsCanonicalManaSeedSprite(Sprite sprite)
    {
        if (sprite == null)
            return false;

        Rect rect = sprite.rect;
        return rect.width >= ManaSeedCellSize - 0.5f
            && rect.height >= ManaSeedCellSize - 0.5f;
    }

    public static Vector2 GetFrameGridAnchor(int frameIndex)
    {
        int col = frameIndex % 8;
        int row = frameIndex / 8;
        float cellX = col * ManaSeedCellSize;
        float cellY = (7 - row) * ManaSeedCellSize;
        return new Vector2(cellX + ManaSeedCellSize * 0.5f, cellY);
    }
}
