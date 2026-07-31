using System.Collections.Generic;

public static class CharacterAppearanceData
{
    private const string KeyPrefix = "prisma_char_";
    private const string LegacyKeyPrefix = "engrenum_char_";
    private const string LegacySkinKey = "engrenum_char_skin";
    private const string LegacyOutfitKey = "engrenum_char_outfit";
    private const string LegacyHairKey = "engrenum_char_hair";
    private const string LegacyHatKey = "engrenum_char_hat";

    public const string DefaultSkinId = "char_a_p1_0bas_humn_v00";

    public static void Save(Dictionary<CharacterLayerType, string> selection)
    {
        foreach (CharacterLayerType layer in CharacterLayerDefinitions.CustomizationOrder)
        {
            string value = selection.TryGetValue(layer, out string id) ? id ?? string.Empty : string.Empty;
            UnityEngine.PlayerPrefs.SetString(GetKey(layer), value);
        }

        UnityEngine.PlayerPrefs.Save();
    }

    public static Dictionary<CharacterLayerType, string> Load()
    {
        Dictionary<CharacterLayerType, string> selection = CharacterLayerDefinitions.CreateDefaultSelection();

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.CustomizationOrder)
        {
            string key = GetKey(layer);
            string legacyKey = LegacyKeyPrefix + layer.ToString().ToLowerInvariant();

            if (UnityEngine.PlayerPrefs.HasKey(key))
                selection[layer] = UnityEngine.PlayerPrefs.GetString(key, string.Empty);
            else if (UnityEngine.PlayerPrefs.HasKey(legacyKey))
                selection[layer] = UnityEngine.PlayerPrefs.GetString(legacyKey, string.Empty);
        }

        MigrateLegacyKeys(selection);
        CharacterCapePairing.EnforcePairedCapes(selection);
        return selection;
    }

    public static void Save(string skinId, string outfitId, string hairId, string hatId)
    {
        Dictionary<CharacterLayerType, string> selection = Load();
        selection[CharacterLayerType.Skin] = skinId;
        selection[CharacterLayerType.Outfit] = outfitId ?? string.Empty;
        selection[CharacterLayerType.Hair] = hairId ?? string.Empty;
        selection[CharacterLayerType.Hat] = hatId ?? string.Empty;
        Save(selection);
    }

    public static (string skinId, string outfitId, string hairId, string hatId) LoadLegacy()
    {
        Dictionary<CharacterLayerType, string> selection = Load();
        return (
            selection[CharacterLayerType.Skin],
            selection[CharacterLayerType.Outfit],
            selection[CharacterLayerType.Hair],
            selection[CharacterLayerType.Hat]
        );
    }

    private static string GetKey(CharacterLayerType layer)
    {
        return KeyPrefix + layer.ToString().ToLowerInvariant();
    }

    private static void MigrateLegacyKeys(Dictionary<CharacterLayerType, string> selection)
    {
        if (UnityEngine.PlayerPrefs.HasKey(LegacySkinKey) && string.IsNullOrEmpty(selection[CharacterLayerType.Skin]))
            selection[CharacterLayerType.Skin] = UnityEngine.PlayerPrefs.GetString(LegacySkinKey, DefaultSkinId);

        if (UnityEngine.PlayerPrefs.HasKey(LegacyOutfitKey) && string.IsNullOrEmpty(selection[CharacterLayerType.Outfit]))
            selection[CharacterLayerType.Outfit] = UnityEngine.PlayerPrefs.GetString(LegacyOutfitKey, string.Empty);

        if (UnityEngine.PlayerPrefs.HasKey(LegacyHairKey) && string.IsNullOrEmpty(selection[CharacterLayerType.Hair]))
            selection[CharacterLayerType.Hair] = UnityEngine.PlayerPrefs.GetString(LegacyHairKey, string.Empty);

        if (UnityEngine.PlayerPrefs.HasKey(LegacyHatKey) && string.IsNullOrEmpty(selection[CharacterLayerType.Hat]))
            selection[CharacterLayerType.Hat] = UnityEngine.PlayerPrefs.GetString(LegacyHatKey, string.Empty);
    }
}
