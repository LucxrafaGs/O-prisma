using UnityEngine;

/// <summary>
/// Sorting do mundo: player e árvores compartilham Y-sort (0–19) para
/// o personagem passar atrás das copas. Estátuas ficam em Order baixo.
/// Overlays da cena usam Order ≥ 20.
/// </summary>
public static class WorldDepth
{
    public const int ActorOrderMax = 19;
    public const int ActorOrderCenter = 10;
    public const float ActorYPrecision = 4f;

    /// <summary>Tilemap Colider (estátuas) — sempre atrás do player/NPC (0–19).</summary>
    public const int StatueOrder = -1;

    public const float Precision = 100f;
    public const int OrderBias = 15000;

    public static int OrderFromY(float worldY)
    {
        return Mathf.RoundToInt(-worldY * Precision) + OrderBias;
    }

    /// <summary>
    /// Player/NPC/árvore: sul (Y menor) → order maior → na frente.
    /// Mesma faixa para o personagem passar sob as copas.
    /// </summary>
    public static int ActorOrderFromY(float worldY)
    {
        int order = Mathf.RoundToInt(-worldY * ActorYPrecision) + ActorOrderCenter;
        return Mathf.Clamp(order, 0, ActorOrderMax);
    }

    public static int TreeOrderFromY(float worldY) => ActorOrderFromY(worldY);

    public static int CanopyOrderFromY(float worldY) => ActorOrderFromY(worldY);

    public static int ShadowOrderFromY(float worldY) =>
        Mathf.Max(ActorOrderFromY(worldY) - 1, 0);

    public const int ActorSortOrder = ActorOrderCenter;
    public const int CanopySortOrder = ActorOrderCenter;
    public const int ShadowSortOrder = ActorOrderCenter - 1;
}
