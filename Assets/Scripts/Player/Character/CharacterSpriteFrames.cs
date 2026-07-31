using UnityEngine;

public static class CharacterSpriteFrames
{
    public static int ParseFrameIndex(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return -1;

        int underscoreIndex = spriteName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= spriteName.Length - 1)
            return -1;

        string suffix = spriteName[(underscoreIndex + 1)..];
        int digitCount = 0;
        while (digitCount < suffix.Length && char.IsDigit(suffix[digitCount]))
            digitCount++;

        if (digitCount == 0)
            return -1;

        return int.TryParse(suffix[..digitCount], out int frameIndex) ? frameIndex : -1;
    }

    public static Sprite FindByFrame(Sprite[] sprites, int frameIndex)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite != null && ParseFrameIndex(sprite.name) == frameIndex)
                return sprite;
        }

        return null;
    }
}
