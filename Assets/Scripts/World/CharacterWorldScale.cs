using UnityEngine;

/// <summary>
/// Escala uniforme do protagonista e NPCs no mundo (paper-doll cresce junto).
/// </summary>
public static class CharacterWorldScale
{
    public const float Uniform = 2.8f;

    public static Vector3 Vector => new(Uniform, Uniform, 1f);

    public static void Apply(Transform target)
    {
        if (target != null)
            target.localScale = Vector;
    }
}
