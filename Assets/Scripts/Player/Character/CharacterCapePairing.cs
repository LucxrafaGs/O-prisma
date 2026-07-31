using System.Collections.Generic;

public static class CharacterCapePairing
{
    public static bool TryGetPairedCloakId(string backId, out string cloakId)
    {
        cloakId = string.Empty;
        if (string.IsNullOrEmpty(backId) || !backId.Contains("_0bot_lnpl_"))
            return false;

        cloakId = backId.Replace("_0bot_", "_2clo_");
        return true;
    }

    public static bool TryGetPairedBackId(string cloakId, out string backId)
    {
        backId = string.Empty;
        if (!IsBackPairedCloak(cloakId))
            return false;

        backId = cloakId.Replace("_2clo_", "_0bot_");
        return true;
    }

    public static bool IsBackPairedCloak(string cloakId)
    {
        return !string.IsNullOrEmpty(cloakId) && cloakId.Contains("_2clo_lnpl_");
    }

    public static void EnforcePairedCapes(Dictionary<CharacterLayerType, string> selection)
    {
        if (selection == null)
            return;

        if (!selection.TryGetValue(CharacterLayerType.Back, out string backId))
            backId = string.Empty;

        if (!selection.TryGetValue(CharacterLayerType.Cloak, out string cloakId))
            cloakId = string.Empty;

        if (!string.IsNullOrEmpty(backId) && TryGetPairedCloakId(backId, out string pairedCloak))
            selection[CharacterLayerType.Cloak] = pairedCloak;
        else if (string.IsNullOrEmpty(backId) && IsBackPairedCloak(cloakId))
            selection[CharacterLayerType.Cloak] = string.Empty;
    }
}
