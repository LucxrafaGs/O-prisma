using UnityEngine;

/// <summary>
/// Constantes de sorting do mundo.
/// Personagens e bases de árvores compartilham <see cref="ActorSortOrder"/> e usam
/// Transparency Sort (Custom Axis Y) para frente/trás — assim Order 20+ na cena
/// continua acima das árvores (WorldDepth antigo ~15000 furava qualquer order da cena).
/// </summary>
public static class WorldDepth
{
    /// <summary>Order compartilhado: player, NPCs e tronco/sombra das árvores.</summary>
    public const int ActorSortOrder = 10;

    /// <summary>Folhas / topo de props — acima dos atores, abaixo de overlays (20+) e neblina.</summary>
    public const int CanopySortOrder = ActorSortOrder + 1;

    /// <summary>Sombra filha — logo atrás do tronco.</summary>
    public const int ShadowSortOrder = ActorSortOrder - 1;

    // Legado (props/construções que ainda codificam Y no order).
    public const float Precision = 100f;
    public const int OrderBias = 15000;

    public static int OrderFromY(float worldY)
    {
        return Mathf.RoundToInt(-worldY * Precision) + OrderBias;
    }
}
