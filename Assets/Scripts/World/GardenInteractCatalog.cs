using UnityEngine;

/// <summary>
/// Sprites pequenos da sheet 11_Camping para Garden_Interact (prints do usuário).
/// </summary>
public static class GardenInteractCatalog
{
    public const string SheetAssetPath = NatureTreeCatalog.SheetAssetPath;
    public const string SpritePrefix = NatureTreeCatalog.SpritePrefix;

    public sealed class PlantType
    {
        public string Id;
        public string DisplayName;
        public int SpriteIndex;
        /// <summary>Mini árvores / mudas com tronco — colisão sólida na base.</summary>
        public bool HasTrunkCollision;
    }

    public static readonly PlantType[] Types =
    {
        // Chão: gramas / flores — só trigger elástico
        new PlantType { Id = "bush_wide", DisplayName = "Arbusto baixo", SpriteIndex = 147, HasTrunkCollision = false },
        new PlantType { Id = "bush_leafy", DisplayName = "Folhagem baixa", SpriteIndex = 164, HasTrunkCollision = false },
        new PlantType { Id = "grass_tiny", DisplayName = "Grama minúscula", SpriteIndex = 177, HasTrunkCollision = false },
        new PlantType { Id = "leaves_spread", DisplayName = "Folhas abertas", SpriteIndex = 194, HasTrunkCollision = false },
        new PlantType { Id = "flower_red_cluster", DisplayName = "Flores vermelhas", SpriteIndex = 166, HasTrunkCollision = false },
        new PlantType { Id = "flower_berry", DisplayName = "Bagos vermelhos", SpriteIndex = 163, HasTrunkCollision = false },
        new PlantType { Id = "flower_tips", DisplayName = "Brotos coloridos", SpriteIndex = 155, HasTrunkCollision = false },
        new PlantType { Id = "flower_mixed", DisplayName = "Flores mistas", SpriteIndex = 171, HasTrunkCollision = false },
        new PlantType { Id = "flower_white", DisplayName = "Flores brancas", SpriteIndex = 158, HasTrunkCollision = false },
        new PlantType { Id = "flower_meadow", DisplayName = "Campo de flores", SpriteIndex = 172, HasTrunkCollision = false },
        new PlantType { Id = "flower_white_stems", DisplayName = "Flores no caule", SpriteIndex = 167, HasTrunkCollision = false },

        // Mini árvores / mudas — colisão no tronco
        new PlantType { Id = "mini_tree", DisplayName = "Mini árvore", SpriteIndex = 197, HasTrunkCollision = true },
        new PlantType { Id = "mini_tree_fruit", DisplayName = "Mini árvore frutada", SpriteIndex = 198, HasTrunkCollision = true },
        new PlantType { Id = "sapling_teal_a", DisplayName = "Muda teal A", SpriteIndex = 309, HasTrunkCollision = true },
        new PlantType { Id = "sapling_teal_b", DisplayName = "Muda teal B", SpriteIndex = 310, HasTrunkCollision = true }
    };

    public static bool TryGetById(string id, out PlantType type)
    {
        type = null;
        if (string.IsNullOrEmpty(id))
            return false;

        for (int i = 0; i < Types.Length; i++)
        {
            if (Types[i].Id == id)
            {
                type = Types[i];
                return true;
            }
        }

        return false;
    }

    public static bool HasTrunkCollision(string plantIdOrObjectName)
    {
        if (string.IsNullOrEmpty(plantIdOrObjectName))
            return false;

        if (TryGetById(plantIdOrObjectName, out PlantType direct))
            return direct.HasTrunkCollision;

        // Nomes gerados: Plant_mini_tree / Plant_Mini árvore
        string lower = plantIdOrObjectName.ToLowerInvariant();
        if (lower.Contains("mini_tree") || lower.Contains("sapling") || lower.Contains("muda") ||
            lower.Contains("mini árvore") || lower.Contains("mini_arvore"))
            return true;

        if (lower.Contains("flower") || lower.Contains("flor") || lower.Contains("grass") ||
            lower.Contains("grama") || lower.Contains("bush") || lower.Contains("arbusto") ||
            lower.Contains("leaves") || lower.Contains("folha") || lower.Contains("broto"))
            return false;

        return false;
    }

    public static string SpriteName(int index) => $"{SpritePrefix}{index}";
}
