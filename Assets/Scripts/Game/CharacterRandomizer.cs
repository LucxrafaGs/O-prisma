using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterRandomizer
{
    public static void ApplyNewCharacterDefaults(
        Dictionary<CharacterLayerType, string> selection,
        CharacterSpriteLibrary library,
        CharacterGender gender)
    {
        if (selection == null || library == null)
            return;

        selection[CharacterLayerType.Skin] = CharacterGenderUtility.GetSkinId(gender);
        selection[CharacterLayerType.Back] = string.Empty;
        selection[CharacterLayerType.Outfit] = PickRandom(library, CharacterLayerType.Outfit);
        selection[CharacterLayerType.Cloak] = string.Empty;
        selection[CharacterLayerType.Face] = string.Empty;
        selection[CharacterLayerType.Hair] = PickRandom(library, CharacterLayerType.Hair);
        selection[CharacterLayerType.Hat] = string.Empty;
    }

    public static Dictionary<CharacterLayerType, string> CreateRandomNpcLook(CharacterSpriteLibrary library)
    {
        Dictionary<CharacterLayerType, string> selection = CharacterLayerDefinitions.CreateDefaultSelection();
        if (library == null)
            return selection;

        CharacterGender gender = Random.value < 0.5f ? CharacterGender.Male : CharacterGender.Female;
        selection[CharacterLayerType.Skin] = CharacterGenderUtility.GetSkinId(gender);
        selection[CharacterLayerType.Outfit] = PickRandom(library, CharacterLayerType.Outfit);
        selection[CharacterLayerType.Hair] = PickRandom(library, CharacterLayerType.Hair);
        selection[CharacterLayerType.Face] = Random.value < 0.25f
            ? PickRandom(library, CharacterLayerType.Face)
            : string.Empty;
        selection[CharacterLayerType.Hat] = Random.value < 0.2f
            ? PickRandom(library, CharacterLayerType.Hat)
            : string.Empty;
        selection[CharacterLayerType.Back] = string.Empty;
        selection[CharacterLayerType.Cloak] = string.Empty;
        CharacterCapePairing.EnforcePairedCapes(selection);
        return selection;
    }

    private static string PickRandom(CharacterSpriteLibrary library, CharacterLayerType layer)
    {
        List<CharacterSpriteLibrary.SheetEntry> entries = library.GetEntries(layer).ToList();
        if (entries.Count == 0)
            return string.Empty;

        int index = Random.Range(0, entries.Count);
        return entries[index].id;
    }
}
