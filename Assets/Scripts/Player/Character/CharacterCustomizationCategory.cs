public enum CharacterCustomizationCategory
{
    Skin,
    Hair,
    Clothes,
    Accessories
}

public static class CharacterCustomizationCategoryUtility
{
    public static readonly CharacterCustomizationCategory[] Order =
    {
        CharacterCustomizationCategory.Skin,
        CharacterCustomizationCategory.Hair,
        CharacterCustomizationCategory.Clothes,
        CharacterCustomizationCategory.Accessories
    };

    public static string Label(CharacterCustomizationCategory category)
    {
        return category switch
        {
            CharacterCustomizationCategory.Skin => "Pele",
            CharacterCustomizationCategory.Hair => "Cabelo",
            CharacterCustomizationCategory.Clothes => "Roupas",
            CharacterCustomizationCategory.Accessories => "Acessorios",
            _ => category.ToString()
        };
    }

    public static CharacterLayerType[] Layers(CharacterCustomizationCategory category)
    {
        return category switch
        {
            CharacterCustomizationCategory.Skin => new[] { CharacterLayerType.Skin },
            CharacterCustomizationCategory.Hair => new[] { CharacterLayerType.Hair },
            CharacterCustomizationCategory.Clothes => new[] { CharacterLayerType.Outfit },
            CharacterCustomizationCategory.Accessories => new[]
            {
                CharacterLayerType.Back,
                CharacterLayerType.Cloak,
                CharacterLayerType.Face,
                CharacterLayerType.Hat
            },
            _ => System.Array.Empty<CharacterLayerType>()
        };
    }

    public static bool UsesDirectColorGrid(CharacterCustomizationCategory category)
    {
        return category == CharacterCustomizationCategory.Skin;
    }
}
