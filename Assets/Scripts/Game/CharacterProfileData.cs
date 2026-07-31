public static class CharacterProfileData
{
    private const string NameKey = "prisma_profile_name";
    private const string GenderKey = "prisma_profile_gender";
    private const string LegacyNameKey = "engrenum_profile_name";
    private const string LegacyGenderKey = "engrenum_profile_gender";

    public static void Save(string characterName, CharacterGender gender)
    {
        UnityEngine.PlayerPrefs.SetString(NameKey, characterName ?? string.Empty);
        UnityEngine.PlayerPrefs.SetInt(GenderKey, (int)gender);
        UnityEngine.PlayerPrefs.Save();
    }

    public static string LoadName()
    {
        if (UnityEngine.PlayerPrefs.HasKey(NameKey))
            return UnityEngine.PlayerPrefs.GetString(NameKey, "Aventureiro");

        return UnityEngine.PlayerPrefs.GetString(LegacyNameKey, "Aventureiro");
    }

    public static CharacterGender LoadGender()
    {
        if (UnityEngine.PlayerPrefs.HasKey(GenderKey))
            return (CharacterGender)UnityEngine.PlayerPrefs.GetInt(GenderKey, (int)CharacterGender.Male);

        return (CharacterGender)UnityEngine.PlayerPrefs.GetInt(LegacyGenderKey, (int)CharacterGender.Male);
    }
}
