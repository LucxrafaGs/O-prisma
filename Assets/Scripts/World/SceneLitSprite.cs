using UnityEngine;

/// <summary>
/// Applies URP 2D Sprite-Lit material so Global Light (day/night) and Point lights (lantern)
/// affect any scenery sprite — trees, props, tiles, etc.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SceneLitSprite : MonoBehaviour
{
    private void Awake()
    {
        SceneLitMaterial.ApplyToHierarchy(transform);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            SceneLitMaterial.ApplyToHierarchy(transform);
    }
#endif
}

/// <summary>
/// Shared lit material for world sprites (not the tree clip shader).
/// Uses a persistent project material so sprites never go magenta/pink in the editor.
/// </summary>
public static class SceneLitMaterial
{
    public const string LitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    public const string TreeClipShaderName = "Prisma/SpriteLitVerticalClip";
    public const string ProjectMaterialPath = "Assets/Materials/PrismaSceneSpriteLit.mat";

    private static Material litMaterial;

    public static Material GetLitMaterial()
    {
        if (litMaterial != null)
            return litMaterial;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            litMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ProjectMaterialPath);
#endif
        if (litMaterial == null)
            litMaterial = Resources.Load<Material>("PrismaSceneSpriteLit");

        if (litMaterial != null)
            return litMaterial;

        Shader shader = Shader.Find(LitShaderName);
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        litMaterial = new Material(shader)
        {
            name = "PrismaSceneSpriteLit_Runtime"
        };
        return litMaterial;
    }

    public static void ApplyTo(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Material current = renderer.sharedMaterial;
        if (current != null &&
            current.shader != null &&
            current.shader.name == TreeClipShaderName)
            return;

        Material material = GetLitMaterial();
        if (material != null)
            renderer.sharedMaterial = material;
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            ApplyTo(renderers[i]);
    }
}
