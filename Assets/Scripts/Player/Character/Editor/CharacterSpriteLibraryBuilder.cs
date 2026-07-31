#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Menu: Prisma > Build Character Sprite Library
public static class CharacterSpriteLibraryBuilder
{
    private const string LibraryAssetPath = "Assets/Resources/CharacterSpriteLibrary.asset";
    private const string CharacterRoot = "Assets/Assets/Player/Player";
    private const string CanonicalPage = "p1";

    private static readonly string[] ExcludedPathFragments =
    {
        "/guides/",
        "/weapon sprites/",
        "/guidelines and requirements",
        "/readme"
    };

    private static readonly Regex SheetPattern = new(
        @"^char_a_(?<page>p[^_]+)_(?<layer>\d\w{3})_(?<style>\w+)_(?<variant>v\d+\w*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [MenuItem("Prisma/Build Character Sprite Library")]
    public static void BuildLibrary()
    {
        Directory.CreateDirectory("Assets/Resources");

        CharacterSpriteLibrary library = AssetDatabase.LoadAssetAtPath<CharacterSpriteLibrary>(LibraryAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<CharacterSpriteLibrary>();
            AssetDatabase.CreateAsset(library, LibraryAssetPath);
        }

        Dictionary<string, (string path, CharacterSpriteLibrary.SheetEntry entry)> uniqueEntries = new();

        string[] files = Directory.GetFiles(CharacterRoot, "char_a_*.png", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            string assetPath = file.Replace('\\', '/');
            if (ShouldSkip(assetPath))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            Match match = SheetPattern.Match(fileName);
            if (!match.Success)
                continue;

            string page = match.Groups["page"].Value;
            string layerCode = match.Groups["layer"].Value;
            string styleCode = match.Groups["style"].Value;
            string variant = match.Groups["variant"].Value;

            CharacterLayerType? layer = CharacterLayerDefinitions.MapLayerCode(layerCode);
            if (layer == null)
                continue;

            string canonicalId = $"char_a_{CanonicalPage}_{layerCode}_{styleCode}_{variant}";

            Sprite referenceSprite = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
            if (referenceSprite == null)
                continue;

            CharacterSpriteLibrary.SheetEntry sheetEntry = new()
            {
                id = canonicalId,
                displayName = BuildDisplayName(canonicalId, layer.Value, styleCode, variant),
                layer = layer.Value,
                referenceSprite = referenceSprite,
                sourceAssetPath = assetPath
            };

            if (!uniqueEntries.TryGetValue(canonicalId, out var existing) ||
                PreferPath(assetPath, page, existing.path))
            {
                uniqueEntries[canonicalId] = (assetPath, sheetEntry);
            }
        }

        List<CharacterSpriteLibrary.SheetEntry> entries = uniqueEntries.Values
            .Select(item => item.entry)
            .ToList();

        AddPage4Entries(files, uniqueEntries, entries);

        entries = entries
            .OrderBy(entry => entry.layer)
            .ThenBy(entry => entry.displayName)
            .ToList();

        library.SetEntries(entries);

        Object previousSelection = Selection.activeObject;
        if (previousSelection == library)
            Selection.activeObject = null;

        try
        {
            AssetDatabase.StartAssetEditing();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (previousSelection == library)
            Selection.activeObject = library;

        AssetDatabase.Refresh();

        LogLayerBreakdown(entries);
        LogCanonicalCoverage(uniqueEntries.Values.Select(item => item.path));
        Debug.Log($"Character sprite library built with {entries.Count} sheets at {LibraryAssetPath}.");
    }

    private static void LogCanonicalCoverage(IEnumerable<string> selectedPaths)
    {
        int canonical = 0;
        int legacy = 0;

        foreach (string path in selectedPaths)
        {
            if (IsCanonicalSheet(path))
                canonical++;
            else
            {
                legacy++;
                Debug.LogWarning($"Prisma: folha sem grid 64x64 canonico (sera desalinhada): {path}");
            }
        }

        Debug.Log($"Prisma: folhas canonicas={canonical}, legadas={legacy}");
    }

    private static void AddPage4Entries(
        string[] files,
        Dictionary<string, (string path, CharacterSpriteLibrary.SheetEntry entry)> uniqueEntries,
        List<CharacterSpriteLibrary.SheetEntry> entries)
    {
        HashSet<string> existingIds = new(entries.Select(entry => entry.id));

        foreach (string file in files)
        {
            string assetPath = file.Replace('\\', '/');
            if (ShouldSkip(assetPath))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            Match match = SheetPattern.Match(fileName);
            if (!match.Success || match.Groups["page"].Value != "p4")
                continue;

            if (existingIds.Contains(fileName))
                continue;

            CharacterLayerType? layer = CharacterLayerDefinitions.MapLayerCode(match.Groups["layer"].Value);
            if (layer == null)
                continue;

            Sprite referenceSprite = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
            if (referenceSprite == null)
                continue;

            CharacterSpriteLibrary.SheetEntry sheetEntry = new()
            {
                id = fileName,
                displayName = BuildDisplayName(fileName, layer.Value, match.Groups["style"].Value, match.Groups["variant"].Value),
                layer = layer.Value,
                referenceSprite = referenceSprite,
                sourceAssetPath = assetPath
            };

            entries.Add(sheetEntry);
            existingIds.Add(fileName);
        }
    }

    private static void LogLayerBreakdown(List<CharacterSpriteLibrary.SheetEntry> entries)
    {
        foreach (IGrouping<CharacterLayerType, CharacterSpriteLibrary.SheetEntry> group in entries.GroupBy(entry => entry.layer).OrderBy(g => g.Key))
            Debug.Log($"  {CharacterLayerDefinitions.SectionTitle(group.Key)}: {group.Count()}");
    }

    private static bool ShouldSkip(string assetPath)
    {
        string lower = assetPath.ToLowerInvariant();
        foreach (string fragment in ExcludedPathFragments)
        {
            if (lower.Contains(fragment))
                return true;
        }

        if (IsMergedCharFolder(assetPath))
            return true;

        return false;
    }

    private static bool IsMergedCharFolder(string assetPath)
    {
        if (!assetPath.Contains("/Player/char_a_p1/"))
            return false;

        return !assetPath.Contains("20.") && !assetPath.Contains("21.");
    }

    private static bool PreferPath(string candidate, string candidatePage, string current)
    {
        bool candidateCanonical = IsCanonicalSheet(candidate);
        bool currentCanonical = IsCanonicalSheet(current);
        if (candidateCanonical != currentCanonical)
            return candidateCanonical;

        string currentPage = ExtractPage(Path.GetFileNameWithoutExtension(current));

        if (candidatePage == CanonicalPage && currentPage != CanonicalPage)
            return true;

        if (candidatePage != CanonicalPage && currentPage == CanonicalPage)
            return false;

        bool candidateInPack = IsPackPath(candidate);
        bool currentInPack = IsPackPath(current);
        if (candidateInPack != currentInPack)
            return candidateInPack;

        if (candidatePage != currentPage)
            return PagePriority(candidatePage) > PagePriority(currentPage);

        return candidate.Length > current.Length;
    }

    private static bool IsCanonicalSheet(string assetPath)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
            return false;

        Sprite walkFrame = sprites.FirstOrDefault(sprite => CharacterSpriteFrames.ParseFrameIndex(sprite.name) == 32);
        if (walkFrame == null)
            walkFrame = sprites.FirstOrDefault(sprite => CharacterSpriteFrames.ParseFrameIndex(sprite.name) == 0);

        return walkFrame != null && CharacterSpriteAlignment.IsCanonicalManaSeedSprite(walkFrame);
    }

    private static int PagePriority(string page)
    {
        return page switch
        {
            "p1" => 100,
            "p1B" => 90,
            "p1C" => 80,
            "p2" => 70,
            "p3" => 60,
            "p4" => 50,
            _ => 10
        };
    }

    private static string ExtractPage(string fileName)
    {
        Match match = SheetPattern.Match(fileName);
        return match.Success ? match.Groups["page"].Value : string.Empty;
    }

    private static bool IsPackPath(string assetPath)
    {
        return assetPath.Contains("20.") || assetPath.Contains("21.");
    }

    private static string BuildDisplayName(string id, CharacterLayerType layer, string styleCode, string variant)
    {
        if (layer == CharacterLayerType.Skin)
        {
            string tone = styleCode switch
            {
                "humn" => "Humano",
                "demn" => "Demonio",
                "gbln" => "Goblin",
                _ => styleCode
            };

            return $"{tone} {variant.Replace("v", "")}";
        }

        string styleName = StyleNames.TryGetValue(styleCode, out string mapped) ? mapped : styleCode.ToUpperInvariant();
        return string.IsNullOrEmpty(variant) ? styleName : $"{styleName} {variant.Replace("v", "")}";
    }

    private static readonly Dictionary<string, string> StyleNames = new()
    {
        { "boxr", "Boxer" },
        { "undi", "Roupa intima" },
        { "fstr", "Tunica fazendeiro" },
        { "pfpn", "Calca camponesa" },
        { "pfdr", "Vestido campones" },
        { "pfht", "Chapeu campones" },
        { "pfbn", "Chapeu bonnet" },
        { "pnty", "Chapeu pontudo" },
        { "rnht", "Chapeu chuva" },
        { "band", "Bandana" },
        { "angl", "Calca pescador" },
        { "bksm", "Avental ferreiro" },
        { "alch", "Jaleco alquimista" },
        { "bob1", "Cabelo Bob" },
        { "bob2", "Cabelo Bob 2" },
        { "dap1", "Cabelo Dapper" },
        { "flat", "Cabelo Flat" },
        { "fro1", "Cabelo Afro" },
        { "pon1", "Cabelo Rabo" },
        { "spk2", "Cabelo Spiky" },
        { "lnpl", "Capa longa" },
        { "mnpl", "Manto" },
        { "hdpl", "Capuz cima" },
        { "hddn", "Capuz baixo" },
        { "gogl", "Oculos" },
        { "sw01", "Espada" },
        { "sh01", "Escudo" },
        { "bo01", "Arco" },
        { "bo02", "Arco 2" },
        { "bo03", "Arco 3" },
        { "qv01", "Aljava" },
        { "sp01", "Lanca" },
        { "sp02", "Lanca 2" },
        { "farm", "Enxada" },
        { "mine", "Picareta" },
        { "wood", "Machado" },
        { "bnet", "Peneira" },
        { "hb01", "Machado grande" },
        { "ax01", "Machado combate" },
        { "mc01", "Cajado" },
        { "sh02", "Escudo 2" },
        { "sh03", "Escudo 3" },
        { "roda", "Roda" },
        { "smth", "Martelo" }
    };
}
#endif
