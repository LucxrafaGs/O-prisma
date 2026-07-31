using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterCustomizationCatalog
{
    private static CharacterSpriteLibrary cachedLibrary;
    private static readonly Dictionary<string, List<StyleGroup>> styleGroupsByLayerKey = new();
    private static readonly Dictionary<CharacterCustomizationCategory, List<CharacterSpriteLibrary.SheetEntry>> flatEntriesByCategory = new();

    public sealed class StyleGroup
    {
        public CharacterLayerType layer;
        public string styleCode;
        public string groupKey;
        public string title;
        public Sprite previewSprite;
        public List<CharacterSpriteLibrary.SheetEntry> variants = new();
    }

    public static List<StyleGroup> BuildStyleGroups(
        CharacterSpriteLibrary library,
        IEnumerable<CharacterLayerType> layers)
    {
        if (library == null)
            return new List<StyleGroup>();

        EnsureLibraryCache(library);
        string layerKey = BuildLayerKey(layers);
        if (styleGroupsByLayerKey.TryGetValue(layerKey, out List<StyleGroup> cachedGroups))
            return cachedGroups;

        Dictionary<string, StyleGroup> groups = new();
        foreach (CharacterLayerType layer in layers)
        {
            foreach (CharacterSpriteLibrary.SheetEntry entry in library.GetEntries(layer))
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;

                if (!CharacterSheetIdentity.TryParse(entry.id, out CharacterLayerType parsedLayer, out string styleCode, out _))
                    continue;

                string groupKey = CharacterSheetIdentity.BuildGroupKey(parsedLayer, styleCode);
                if (!groups.TryGetValue(groupKey, out StyleGroup group))
                {
                    group = new StyleGroup
                    {
                        layer = parsedLayer,
                        styleCode = styleCode,
                        groupKey = groupKey,
                        title = CharacterStyleNames.GetStyleTitle(parsedLayer, styleCode),
                        previewSprite = entry.referenceSprite
                    };
                    groups[groupKey] = group;
                }

                group.variants.Add(entry);
            }
        }

        foreach (StyleGroup group in groups.Values)
        {
            group.variants = group.variants
                .OrderBy(entry => entry.displayName)
                .ToList();

            if (group.previewSprite == null && group.variants.Count > 0)
                group.previewSprite = group.variants[0].referenceSprite;
        }

        List<StyleGroup> result = groups.Values
            .OrderBy(group => GetLayerOrder(layers, group.layer))
            .ThenBy(group => group.title)
            .ToList();

        styleGroupsByLayerKey[layerKey] = result;
        return result;
    }

    private static int GetLayerOrder(IEnumerable<CharacterLayerType> layers, CharacterLayerType layer)
    {
        int index = 0;
        foreach (CharacterLayerType entry in layers)
        {
            if (entry == layer)
                return index;

            index++;
        }

        return (int)layer;
    }

    public static List<CharacterSpriteLibrary.SheetEntry> BuildFlatEntries(
        CharacterSpriteLibrary library,
        IEnumerable<CharacterLayerType> layers)
    {
        List<CharacterSpriteLibrary.SheetEntry> entries = new();
        if (library == null)
            return entries;

        if (layers is CharacterLayerType[] layerArray
            && layerArray.Length == 1
            && layerArray[0] == CharacterLayerType.Skin)
        {
            EnsureLibraryCache(library);
            if (flatEntriesByCategory.TryGetValue(CharacterCustomizationCategory.Skin, out List<CharacterSpriteLibrary.SheetEntry> skinEntries))
                return skinEntries;

            List<CharacterSpriteLibrary.SheetEntry> builtSkinEntries = library.GetEntries(CharacterLayerType.Skin).ToList();
            flatEntriesByCategory[CharacterCustomizationCategory.Skin] = builtSkinEntries;
            return builtSkinEntries;
        }

        foreach (CharacterLayerType layer in layers)
            entries.AddRange(library.GetEntries(layer));

        return entries.OrderBy(entry => entry.displayName).ToList();
    }

    public static void WarmUpSwatches(CharacterSpriteLibrary library)
    {
        if (library == null)
            return;

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.CustomizationOrder)
        {
            foreach (CharacterSpriteLibrary.SheetEntry entry in library.GetEntries(layer))
                GetEntrySwatchColor(entry, library);
        }
    }

    public static Color GetEntrySwatchColor(CharacterSpriteLibrary.SheetEntry entry, CharacterSpriteLibrary library = null)
    {
        if (entry == null)
            return new Color(0.72f, 0.72f, 0.76f, 1f);

        if (entry.swatchColor.a > 0.01f)
            return entry.swatchColor;

        if (runtimeSwatchCache.TryGetValue(entry.id, out Color cachedColor))
            return cachedColor;

        Sprite sampleSprite = CharacterSwatchColorSampler.PickSampleSprite(library, entry);
        Color sampledColor = CharacterSwatchColorSampler.Sample(sampleSprite, entry.layer);
        if (sampledColor.a > 0.01f)
        {
            runtimeSwatchCache[entry.id] = sampledColor;
            return sampledColor;
        }

        if (TryParseVariantFallback(entry.id, out Color fallbackColor))
            return fallbackColor;

        return new Color(0.72f, 0.72f, 0.76f, 1f);
    }

    private static readonly Dictionary<string, Color> runtimeSwatchCache = new();

    private static bool TryParseVariantFallback(string sheetId, out Color color)
    {
        color = default;
        if (!CharacterSheetIdentity.TryParse(sheetId, out _, out _, out string variantCode))
            return false;

        color = VariantFallbackColor(variantCode);
        return true;
    }

    private static void EnsureLibraryCache(CharacterSpriteLibrary library)
    {
        if (cachedLibrary == library)
            return;

        cachedLibrary = library;
        styleGroupsByLayerKey.Clear();
        flatEntriesByCategory.Clear();
        runtimeSwatchCache.Clear();
    }

    private static string BuildLayerKey(IEnumerable<CharacterLayerType> layers)
    {
        if (layers is CharacterLayerType[] layerArray)
            return string.Join("|", layerArray);

        return string.Join("|", layers.OrderBy(layer => layer));
    }

    private static Color VariantFallbackColor(string variantCode)
    {
        int index = 0;
        if (!string.IsNullOrEmpty(variantCode) && variantCode.StartsWith("v") && variantCode.Length > 1)
            int.TryParse(variantCode[1..], out index);

        float hue = (index * 0.09f) % 1f;
        return Color.HSVToRGB(hue, 0.42f, 0.92f);
    }
}
