using UnityEngine;

/// <summary>
/// Árvores únicas (um sprite = uma árvore completa) da sheet 11_Camping_16x16.
/// Tronco grosso → seca no inverno. Tronco fino → mantém cores de outono no inverno.
/// </summary>
public static class NatureTreeCatalog
{
    public const string SheetAssetPath = "Assets/Assets/World/Nature/11_Camping_16x16.png";
    public const string SpritePrefix = "11_Camping_16x16_";

    public sealed class TreeType
    {
        public string Id;
        public string DisplayName;
        public int SpringLight;
        public int SummerDark;
        public int AutumnOrange;
        public int AutumnYellow;
        public int WinterDry;
        /// <summary>True = tronco grosso / árvore grande → sprite seca no inverno.</summary>
        public bool UsesDryInWinter;
    }

    public static readonly TreeType[] Types =
    {
        new TreeType
        {
            Id = "round_small",
            DisplayName = "Redonda pequena",
            SpringLight = 291,
            SummerDark = 151,
            AutumnOrange = 254,
            AutumnYellow = 327,
            WinterDry = 228,
            UsesDryInWinter = false
        },
        new TreeType
        {
            Id = "wide_bush",
            DisplayName = "Copa larga",
            SpringLight = 289,
            SummerDark = 149,
            AutumnOrange = 252,
            AutumnYellow = 325,
            WinterDry = 229,
            UsesDryInWinter = false
        },
        new TreeType
        {
            Id = "round_plain",
            DisplayName = "Redonda lisa",
            SpringLight = 296,
            SummerDark = 169,
            AutumnOrange = 260,
            AutumnYellow = 332,
            WinterDry = 233,
            UsesDryInWinter = false
        },
        // Tronco grosso + frutos — seca no inverno
        new TreeType
        {
            Id = "thick_fruit",
            DisplayName = "Tronco grosso",
            SpringLight = 295,
            SummerDark = 168,
            AutumnOrange = 259,
            AutumnYellow = 331,
            WinterDry = 231,
            UsesDryInWinter = true
        },
        // Copa alta / maior
        new TreeType
        {
            Id = "tall_wild",
            DisplayName = "Copa alta",
            SpringLight = 320,
            SummerDark = 216,
            AutumnOrange = 284,
            AutumnYellow = 284,
            WinterDry = 228,
            UsesDryInWinter = true
        }
    };

    public static bool UsesDryInWinter(string treeTypeId)
    {
        if (string.IsNullOrEmpty(treeTypeId))
            return false;

        for (int i = 0; i < Types.Length; i++)
        {
            if (Types[i].Id == treeTypeId)
                return Types[i].UsesDryInWinter;
        }

        // Legado: round_fruit era tronco grosso
        return treeTypeId is "round_fruit" or "tall_wild" or "thick_fruit";
    }

    public static string SpriteName(int index) => $"{SpritePrefix}{index}";
}
