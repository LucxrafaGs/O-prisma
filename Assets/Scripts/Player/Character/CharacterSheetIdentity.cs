public static class CharacterSheetIdentity
{
    public static bool TryParse(string sheetId, out CharacterLayerType layer, out string styleCode, out string variantCode)
    {
        layer = CharacterLayerType.Skin;
        styleCode = string.Empty;
        variantCode = string.Empty;

        if (string.IsNullOrEmpty(sheetId))
            return false;

        string[] parts = sheetId.Split('_');
        if (parts.Length < 6)
            return false;

        CharacterLayerType? mappedLayer = CharacterLayerDefinitions.MapLayerCode(parts[3]);
        if (mappedLayer == null)
            return false;

        layer = mappedLayer.Value;
        styleCode = parts[4];
        variantCode = parts[5];
        return true;
    }

    public static string BuildGroupKey(CharacterLayerType layer, string styleCode)
    {
        return $"{layer}:{styleCode}";
    }

    public static int CompareVariantCodes(string leftId, string rightId)
    {
        return GetVariantSortKey(leftId).CompareTo(GetVariantSortKey(rightId));
    }

    public static int GetVariantSortKey(string sheetId)
    {
        if (!TryParse(sheetId, out _, out string leftStyle, out string leftVariant))
            return int.MaxValue;

        if (!int.TryParse(leftVariant.TrimStart('v'), out int variantNumber))
            variantNumber = 0;

        return variantNumber * 1000 + StableHash(leftStyle);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in value ?? string.Empty)
                hash = hash * 31 + character;
            return hash & 0x3ff;
        }
    }
}
