using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSpriteLibrary", menuName = "Prisma/Character Sprite Library")]
public class CharacterSpriteLibrary : ScriptableObject
{
    [System.Serializable]
    public class SheetEntry
    {
        public string id;
        public string displayName;
        public CharacterLayerType layer;
        public Sprite referenceSprite;
        public string sourceAssetPath;
        public Color swatchColor;
    }

    [SerializeField] private List<SheetEntry> entries = new();

    [SerializeField, HideInInspector]
    private List<Sprite> buildSpritePool = new();

    private Dictionary<string, Sprite[]> spriteCache;
    private Dictionary<CharacterLayerType, List<SheetEntry>> customizationEntriesByLayer;

    public IReadOnlyList<SheetEntry> Entries => entries;

    public Sprite[] GetSprites(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        EnsureCache();
        return spriteCache.TryGetValue(id, out Sprite[] sprites) ? sprites : null;
    }

    public IEnumerable<SheetEntry> GetEntries(CharacterLayerType layer)
    {
        EnsureCustomizationLayerIndex();
        return customizationEntriesByLayer.TryGetValue(layer, out List<SheetEntry> layerEntries)
            ? layerEntries
            : System.Array.Empty<SheetEntry>();
    }

    private void EnsureCustomizationLayerIndex()
    {
        if (customizationEntriesByLayer != null)
            return;

        customizationEntriesByLayer = new Dictionary<CharacterLayerType, List<SheetEntry>>();
        foreach (SheetEntry entry in entries)
        {
            if (entry == null || !IsCustomizationSheet(entry.id) || ShouldHideFromCustomization(entry))
                continue;

            if (!customizationEntriesByLayer.TryGetValue(entry.layer, out List<SheetEntry> layerEntries))
            {
                layerEntries = new List<SheetEntry>();
                customizationEntriesByLayer[entry.layer] = layerEntries;
            }

            layerEntries.Add(entry);
        }

        foreach (List<SheetEntry> layerEntries in customizationEntriesByLayer.Values)
            layerEntries.Sort(CompareCustomizationEntries);
    }

    private static int CompareCustomizationEntries(SheetEntry left, SheetEntry right)
    {
        if (left == null && right == null)
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int layerCompare = left.layer.CompareTo(right.layer);
        if (layerCompare != 0)
            return layerCompare;

        int variantCompare = CharacterSheetIdentity.CompareVariantCodes(left.id, right.id);
        if (variantCompare != 0)
            return variantCompare;

        return string.Compare(left.displayName, right.displayName, System.StringComparison.Ordinal);
    }

    private static bool ShouldHideFromCustomization(SheetEntry entry)
    {
        return entry.layer == CharacterLayerType.Cloak
            && CharacterCapePairing.IsBackPairedCloak(entry.id);
    }

    private static bool IsCustomizationSheet(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        return !id.Contains("_p4_");
    }

    public void WarmUp()
    {
        EnsureCache();
    }

    public void RebuildCache()
    {
        spriteCache = null;
        customizationEntriesByLayer = null;
        EnsureCache();
    }

    private void EnsureCache()
    {
        if (spriteCache != null)
            return;

        spriteCache = BuildSpriteCacheFromPool();

        foreach (SheetEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                continue;

            if (spriteCache.ContainsKey(entry.id))
                continue;

            Sprite[] fallbackSprites = ResolveSpritesFallback(entry);
            if (fallbackSprites != null && fallbackSprites.Length > 0)
                spriteCache[entry.id] = fallbackSprites;
        }
    }

    private Dictionary<string, Sprite[]> BuildSpriteCacheFromPool()
    {
        Dictionary<string, List<Sprite>> groupedSprites = new();

        foreach (Sprite sprite in buildSpritePool)
        {
            if (sprite == null)
                continue;

            string spriteName = sprite.name;
            if (CharacterSpriteFrames.ParseFrameIndex(spriteName) < 0)
                continue;

            int underscoreIndex = spriteName.LastIndexOf('_');
            if (underscoreIndex <= 0)
                continue;

            string sheetId = spriteName[..underscoreIndex];
            if (!groupedSprites.TryGetValue(sheetId, out List<Sprite> sprites))
            {
                sprites = new List<Sprite>();
                groupedSprites[sheetId] = sprites;
            }

            sprites.Add(sprite);
        }

        Dictionary<string, Sprite[]> cache = new(groupedSprites.Count);
        foreach (KeyValuePair<string, List<Sprite>> pair in groupedSprites)
        {
            List<Sprite> sprites = pair.Value;
            sprites.Sort((left, right) =>
                CharacterSpriteFrames.ParseFrameIndex(left.name)
                    .CompareTo(CharacterSpriteFrames.ParseFrameIndex(right.name)));
            cache[pair.Key] = sprites.ToArray();
        }

        return cache;
    }

    private Sprite[] ResolveSpritesFallback(SheetEntry entry)
    {
#if UNITY_EDITOR
        string sheetPrefix = entry.id + "_";
        string assetPath = entry.sourceAssetPath;
        if (string.IsNullOrEmpty(assetPath) && entry.referenceSprite != null)
            assetPath = UnityEditor.AssetDatabase.GetAssetPath(entry.referenceSprite);

        if (!string.IsNullOrEmpty(assetPath))
        {
            Sprite[] loadedSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .Where(sprite => sprite != null && sprite.name.StartsWith(sheetPrefix))
                .OrderBy(sprite => CharacterSpriteFrames.ParseFrameIndex(sprite.name))
                .ToArray();

            if (loadedSprites.Length > 0)
                return loadedSprites;
        }
#endif

        return entry.referenceSprite != null
            ? new[] { entry.referenceSprite }
            : null;
    }

#if UNITY_EDITOR
    public void SetEntries(List<SheetEntry> newEntries)
    {
        entries = newEntries ?? new List<SheetEntry>();
        entries.RemoveAll(entry => entry == null);

        List<Sprite> spritePool = new();
        foreach (SheetEntry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.sourceAssetPath) && entry.referenceSprite != null)
                entry.sourceAssetPath = UnityEditor.AssetDatabase.GetAssetPath(entry.referenceSprite);

            if (string.IsNullOrEmpty(entry.sourceAssetPath))
            {
                if (entry.referenceSprite != null)
                    spritePool.Add(entry.referenceSprite);
                continue;
            }

            Sprite[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(entry.sourceAssetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => CharacterSpriteFrames.ParseFrameIndex(sprite.name))
                .ToArray();

            foreach (Sprite sprite in sprites)
            {
                if (sprite != null)
                    spritePool.Add(sprite);
            }
        }

        buildSpritePool = spritePool;
        RebuildCache();
        BakeSwatchColors();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void BakeSwatchColors()
    {
        foreach (SheetEntry entry in entries)
        {
            if (entry == null)
                continue;

            Sprite sampleSprite = CharacterSwatchColorSampler.PickSampleSprite(this, entry);
            entry.swatchColor = CharacterSwatchColorSampler.Sample(sampleSprite, entry.layer);
        }
    }

    public void BakeSwatchColorsForEditor()
    {
        BakeSwatchColors();
    }
#endif
}
