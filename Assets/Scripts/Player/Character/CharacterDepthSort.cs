using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lower on screen (smaller Y) draws in front — feet-based depth for paper-doll characters.
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
    }

    private void LateUpdate()
    {
        if (sortingGroup == null)
            return;

        sortingGroup.sortingOrder = WorldDepth.OrderFromY(transform.position.y);
    }
}
