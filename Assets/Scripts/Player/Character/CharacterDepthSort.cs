using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Profundidade por eixo Y (Transparency Sort Custom Axis).
/// Mantém sortingOrder fixo em <see cref="WorldDepth.ActorSortOrder"/> para não
/// passar por cima de objetos da cena com Order maior (ex.: 20).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SortingGroup))]
public class CharacterDepthSort : MonoBehaviour
{
    private SortingGroup sortingGroup;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null)
            sortingGroup = gameObject.AddComponent<SortingGroup>();

        ApplyFixedOrder();
    }

    private void OnEnable()
    {
        ApplyFixedOrder();
    }

    private void LateUpdate()
    {
        // Garante que nada (save/outro script) empurre o order para ~15000 de novo.
        ApplyFixedOrder();
    }

    private void ApplyFixedOrder()
    {
        if (sortingGroup == null)
            return;

        sortingGroup.sortingLayerID = 0; // Default
        sortingGroup.sortingOrder = WorldDepth.ActorSortOrder;
    }
}
