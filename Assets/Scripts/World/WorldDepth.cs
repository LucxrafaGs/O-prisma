using UnityEngine;

/// <summary>
/// Sorting do mundo compatível com Orders baixos da cena (ex.: overlay 20).
/// Y-sort via order compacto (0–19); neblina (~32000) e overlays (≥20) ficam acima.
/// </summary>
public static class WorldDepth
{
    /// <summary>Teto para atores/árvores — objetos com Order ≥ 20 ficam na frente.</summary>
    public const int ActorOrderMax = 19;

    public const int ActorOrderCenter = 10;

    /// <summary>Quanto menor, menos “salto” de order; 2 ≈ bom para mapa top-down.</summary>
    public const float ActorYPrecision = 2f;

    // Legado (construções / sistemas que ainda usam faixa alta).
    public const float Precision = 100f;
    public const int OrderBias = 15000;

    public static int OrderFromY(float worldY)
    {
        return Mathf.RoundToInt(-worldY * Precision) + OrderBias;
    }

    /// <summary>
    /// Order para player/NPC/tronco: sul (Y menor) → order maior → na frente.
    /// Sempre &lt; 20 para respeitar overlays da cena.
    /// </summary>
    public static int ActorOrderFromY(float worldY)
    {
        int order = Mathf.RoundToInt(-worldY * ActorYPrecision) + ActorOrderCenter;
        return Mathf.Clamp(order, 0, ActorOrderMax);
    }

    /// <summary>Folhas: 1 acima da base da mesma árvore, ainda ≤ 19.</summary>
    public static int CanopyOrderFromY(float worldY)
    {
        return Mathf.Min(ActorOrderFromY(worldY) + 1, ActorOrderMax);
    }

    public static int ShadowOrderFromY(float worldY)
    {
        return Mathf.Max(ActorOrderFromY(worldY) - 1, 0);
    }

    // Compat: nomes antigos usados por outros scripts.
    public const int ActorSortOrder = ActorOrderCenter;
    public const int CanopySortOrder = ActorOrderCenter + 1;
    public const int ShadowSortOrder = ActorOrderCenter - 1;
}
