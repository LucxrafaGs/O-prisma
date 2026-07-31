using UnityEngine;

public static class CharacterLibraryAccess
{
    private const string ResourcePath = "CharacterSpriteLibrary";

    private static CharacterSpriteLibrary cached;

    public static CharacterSpriteLibrary Get()
    {
        if (cached == null)
            cached = Resources.Load<CharacterSpriteLibrary>(ResourcePath);

        return cached;
    }

    public static void WarmUp()
    {
        CharacterSpriteLibrary library = Get();
        library?.WarmUp();
    }
}
