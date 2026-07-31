public static class CharacterHairVisibility
{
    public static bool ShouldShowHair(string hatId)
    {
        if (string.IsNullOrEmpty(hatId))
            return true;

        return TryParseHatStyleCode(hatId, out string styleCode) && styleCode == "hddn";
    }

    public static bool TryParseHatStyleCode(string sheetId, out string styleCode)
    {
        styleCode = string.Empty;
        if (string.IsNullOrEmpty(sheetId))
            return false;

        string[] parts = sheetId.Split('_');
        if (parts.Length < 6 || parts[3] != "5hat")
            return false;

        styleCode = parts[4];
        return true;
    }
}
