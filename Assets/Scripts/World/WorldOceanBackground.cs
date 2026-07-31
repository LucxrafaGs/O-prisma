using UnityEngine;

/// <summary>
/// Marca o Tilemap de água infinita (16x16 animados) atrás do mundo.
/// A criação/preenchimento é feita pelo setup de editor — não altera outros tilemaps.
/// </summary>
[DisallowMultipleComponent]
public class WorldOceanBackground : MonoBehaviour
{
    public const string ObjectName = "OceanBackground";
    public const string FillSheetPath =
        "Assets/Assets/World/Nature/Natureza/Animated_Terrains_16x16/Sea_Water_Infinite_Fill_16x16.png";
    public const string AnimatedTilePath =
        "Assets/Assets/World/Nature/Natureza/Animated_Terrains_16x16/Sea_Water_Infinite_Fill.asset";

    public const int SortingOrderBehindWorld = -100;
}
