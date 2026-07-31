using UnityEngine;

/// <summary>
/// Y-sort compartilhado. Bias evita sortingOrder negativo enorme no norte do mapa,
/// que fazia personagens sumirem atrás do chão/prédios estáticos (order ~0).
/// </summary>
public static class WorldDepth
{
    public const float Precision = 100f;

    /// <summary>
    /// Suficiente para Y até ~150 com margem acima de props estáticos (order &lt; 20).
    /// </summary>
    public const int OrderBias = 15000;

    public static int OrderFromY(float worldY)
    {
        return Mathf.RoundToInt(-worldY * Precision) + OrderBias;
    }
}
