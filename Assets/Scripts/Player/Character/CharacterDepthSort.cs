using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Y-sort do personagem com order 10–19 (acima das estátuas em Order 9).
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

        sortingGroup.sortingLayerID = 0;
    }

    private void LateUpdate()
    {
        if (sortingGroup == null)
            return;

        sortingGroup.sortingLayerID = 0;
        sortingGroup.sortingOrder = WorldDepth.ActorOrderFromY(transform.position.y);
    }
}
