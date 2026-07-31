using System.Collections.Generic;

public enum CharacterLayerType
{
    Skin,
    Outfit,
    Hair,
    Hat,
    Back,
    Cloak,
    Face,
    Tool,
    OffHand
}

public static class CharacterLayerDefinitions
{
    public static readonly CharacterLayerType[] CustomizationOrder =
    {
        CharacterLayerType.Skin,
        CharacterLayerType.Back,
        CharacterLayerType.Outfit,
        CharacterLayerType.Cloak,
        CharacterLayerType.Face,
        CharacterLayerType.Hair,
        CharacterLayerType.Hat
    };

    public static readonly CharacterLayerType[] RenderOrder =
    {
        CharacterLayerType.Back,
        CharacterLayerType.Skin,
        CharacterLayerType.Outfit,
        CharacterLayerType.Cloak,
        CharacterLayerType.Face,
        CharacterLayerType.Hair,
        CharacterLayerType.Hat,
        CharacterLayerType.Tool,
        CharacterLayerType.OffHand
    };

    public static int SortingOrder(CharacterLayerType layer)
    {
        for (int i = 0; i < RenderOrder.Length; i++)
        {
            if (RenderOrder[i] == layer)
                return i - 1;
        }

        return 0;
    }

    public static string RendererName(CharacterLayerType layer)
    {
        return layer switch
        {
            CharacterLayerType.Back => "Back",
            CharacterLayerType.Skin => "Body",
            CharacterLayerType.Outfit => "Outfit",
            CharacterLayerType.Cloak => "Cloak",
            CharacterLayerType.Face => "Face",
            CharacterLayerType.Hair => "Hair",
            CharacterLayerType.Hat => "Hat",
            CharacterLayerType.Tool => "Tool",
            CharacterLayerType.OffHand => "OffHand",
            _ => layer.ToString()
        };
    }

    public static string SectionTitle(CharacterLayerType layer)
    {
        return layer switch
        {
            CharacterLayerType.Skin => "Tom de pele",
            CharacterLayerType.Back => "Capa longa",
            CharacterLayerType.Outfit => "Roupa",
            CharacterLayerType.Cloak => "Manto",
            CharacterLayerType.Face => "Rosto / Oculos",
            CharacterLayerType.Hair => "Cabelo",
            CharacterLayerType.Hat => "Chapeu / Capuz",
            CharacterLayerType.Tool => "Ferramenta / Arma (mao)",
            CharacterLayerType.OffHand => "Escudo / Item (off-hand)",
            _ => layer.ToString()
        };
    }

    public static bool AllowNone(CharacterLayerType layer)
    {
        return layer != CharacterLayerType.Skin;
    }

    public static string SummaryLabel(CharacterLayerType layer)
    {
        return layer switch
        {
            CharacterLayerType.Skin => "Pele",
            CharacterLayerType.Back => "Tras",
            CharacterLayerType.Outfit => "Roupa",
            CharacterLayerType.Cloak => "Capa",
            CharacterLayerType.Face => "Rosto",
            CharacterLayerType.Hair => "Cabelo",
            CharacterLayerType.Hat => "Chapeu",
            CharacterLayerType.Tool => "Ferramenta",
            CharacterLayerType.OffHand => "Off-hand",
            _ => layer.ToString()
        };
    }

    public static CharacterLayerType? MapLayerCode(string layerCode)
    {
        return layerCode switch
        {
            "0bas" => CharacterLayerType.Skin,
            "0bot" => CharacterLayerType.Back,
            "1out" => CharacterLayerType.Outfit,
            "2clo" => CharacterLayerType.Cloak,
            "3fac" => CharacterLayerType.Face,
            "4har" => CharacterLayerType.Hair,
            "5hat" => CharacterLayerType.Hat,
            "6tla" => CharacterLayerType.Tool,
            "7tlb" => CharacterLayerType.OffHand,
            _ => null
        };
    }

    public static Dictionary<CharacterLayerType, string> CreateDefaultSelection()
    {
        Dictionary<CharacterLayerType, string> selection = new();
        foreach (CharacterLayerType layer in CustomizationOrder)
            selection[layer] = layer == CharacterLayerType.Skin ? CharacterAppearanceData.DefaultSkinId : string.Empty;

        return selection;
    }
}
