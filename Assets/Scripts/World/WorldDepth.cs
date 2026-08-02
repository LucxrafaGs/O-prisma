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

    /// <summary>Sul/norte: quanto maior, mais sensível o Y-sort.</summary>
    public const float ActorYPrecision = 4f;

    // Legado (construções / sistemas que ainda usam faixa alta).
    public const float Precision = 100f;
    public const int OrderBias = 15000;

    public static int OrderFromY(float worldY)
    {
        return Mathf.RoundToInt(-worldY * Precision) + OrderBias;
    }

    /// <summary>
    /// Order para player/NPC/árvore: sul (Y menor) → order maior → na frente.
    /// Sempre &lt; 20 para respeitar overlays da cena.
    /// </summary>
    public static int ActorOrderFromY(float worldY)
    {
        int order = Mathf.RoundToInt(-worldY * ActorYPrecision) + ActorOrderCenter;
        return Mathf.Clamp(order, 0, ActorOrderMax);
    }

    /// <summary>
    /// Copa no mesmo order da base — na frente do tronco = na frente das folhas.
    /// (Order+1 fazia o player sumir nas folhas ainda no tronco.)
    /// </summary>
    public static int CanopyOrderFromY(float worldY) => ActorOrderFromY(worldY);

    public static int ShadowOrderFromY(float worldY) =>
        Mathf.Max(ActorOrderFromY(worldY) - 1, 0);

    public const int ActorSortOrder = ActorOrderCenter;
    public const int CanopySortOrder = ActorOrderCenter;
    public const int ShadowSortOrder = ActorOrderCenter - 1;
}
