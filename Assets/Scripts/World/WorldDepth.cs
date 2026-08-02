using UnityEngine;

/// <summary>
/// Sorting do mundo compatível com Orders baixos da cena (ex.: overlay 20).
/// Faixas: árvores 0–8 · estátuas 9 · player/NPC 10–19 · overlays ≥20.
/// </summary>
public static class WorldDepth
{
    /// <summary>Teto para atores — objetos com Order ≥ 20 ficam na frente.</summary>
    public const int ActorOrderMax = 19;

    /// <summary>Player/NPC sempre acima das estátuas (Order 9).</summary>
    public const int CharacterOrderMin = 10;

    public const int ActorOrderCenter = 14;

    /// <summary>Árvores/folhagem — sempre abaixo das estátuas.</summary>
    public const int TreeOrderMax = 8;

    public const int TreeOrderCenter = 4;

    /// <summary>Tilemap Colider (estátuas): acima das árvores, abaixo do player.</summary>
    public const int StatueOrder = 9;

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
    /// Order para player/NPC: sul (Y menor) → order maior → na frente.
    /// Sempre 10–19 (acima das estátuas em <see cref="StatueOrder"/>).
    /// </summary>
    public static int ActorOrderFromY(float worldY)
    {
        int order = Mathf.RoundToInt(-worldY * ActorYPrecision) + ActorOrderCenter;
        return Mathf.Clamp(order, CharacterOrderMin, ActorOrderMax);
    }

    /// <summary>Árvores/arbustos: 0–8, sempre abaixo das estátuas.</summary>
    public static int TreeOrderFromY(float worldY)
    {
        int order = Mathf.RoundToInt(-worldY * ActorYPrecision) + TreeOrderCenter;
        return Mathf.Clamp(order, 0, TreeOrderMax);
    }

    /// <summary>Copa de props (postes etc.) — acompanha o actor order.</summary>
    public static int CanopyOrderFromY(float worldY) => ActorOrderFromY(worldY);

    public static int ShadowOrderFromY(float worldY) =>
        Mathf.Max(TreeOrderFromY(worldY) - 1, 0);

    public const int ActorSortOrder = ActorOrderCenter;
    public const int CanopySortOrder = TreeOrderCenter;
    public const int ShadowSortOrder = TreeOrderCenter - 1;
}
