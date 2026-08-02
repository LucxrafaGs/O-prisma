using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Poste em duas camadas (mesmo sprite, clip por UV na linha do BoxCollider2D):
/// Base (até o collider) — Y-sort, player passa por cima.
/// Topo (acima do collider) — sempre na frente, player passa por baixo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[DefaultExecutionOrder(-35)]
public class StreetLampDepthSplit : MonoBehaviour
{
    public const string TopChildName = "LampTop";

    private static Shader clipShader;

    private SpriteRenderer baseRenderer;
    private SpriteRenderer topRenderer;
    private Transform topTransform;
    private BoxCollider2D footCollider;
    private Material baseMaterial;
    private Material topMaterial;
    private float clipThreshold = 0.35f;

    private void Awake()
    {
        footCollider = GetComponent<BoxCollider2D>();
        EnsureRenderers();
        RebuildClip();
    }

    private void OnEnable()
    {
        EnsureRenderers();
        RebuildClip();
    }

    private void LateUpdate()
    {
        if (baseRenderer != null)
        {
            baseRenderer.sortingLayerID = 0;
            baseRenderer.sortingOrder = WorldDepth.ActorSortOrder;
            baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (topRenderer != null)
        {
            topRenderer.sortingLayerID = 0;
            topRenderer.sortingOrder = WorldDepth.CanopySortOrder;
            topRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }

    private void OnDestroy()
    {
        if (baseMaterial != null)
            Destroy(baseMaterial);
        if (topMaterial != null)
            Destroy(topMaterial);
    }

    public void RebuildClip()
    {
        EnsureRenderers();
        if (baseRenderer == null || topRenderer == null)
            return;

        Sprite sprite = baseRenderer.sprite;
        if (sprite == null)
            return;

        clipThreshold = ComputeClipThresholdFromCollider(sprite);
        topTransform.localPosition = Vector3.zero;

        baseRenderer.sprite = sprite;
        baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        topRenderer.sprite = sprite;
        topRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        topRenderer.enabled = true;

        ApplyClipMaterials(sprite);
    }

    private float ComputeClipThresholdFromCollider(Sprite sprite)
    {
        if (footCollider == null)
            footCollider = GetComponent<BoxCollider2D>();

        Bounds spriteLocal = sprite.bounds;
        if (spriteLocal.size.y < 0.0001f)
            return 0.35f;

        if (footCollider == null || !footCollider.enabled)
            return 0.35f;

        // Topo do box dos pés, em espaço local do sprite/pivot.
        float colliderTopLocalY = footCollider.offset.y + footCollider.size.y * 0.5f;
        float t = Mathf.InverseLerp(spriteLocal.min.y, spriteLocal.max.y, colliderTopLocalY);
        return Mathf.Clamp(t, 0.05f, 0.95f);
    }

    private void ApplyClipMaterials(Sprite sprite)
    {
        Shader shader = GetClipShader();
        if (shader == null)
        {
            topRenderer.enabled = false;
            return;
        }

        EnsureLayerMaterials(shader);
        GetAtlasV(sprite, out float vMin, out float vSize);

        ConfigureLayerMaterial(baseMaterial, keepBottom: true, vMin, vSize, clipThreshold);
        ConfigureLayerMaterial(topMaterial, keepBottom: false, vMin, vSize, clipThreshold);

        baseRenderer.sharedMaterial = baseMaterial;
        topRenderer.sharedMaterial = topMaterial;
    }

    private void EnsureLayerMaterials(Shader shader)
    {
        if (baseMaterial == null)
        {
            baseMaterial = new Material(shader)
            {
                name = "StreetLampBaseClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (topMaterial == null)
        {
            topMaterial = new Material(shader)
            {
                name = "StreetLampTopClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private static void ConfigureLayerMaterial(
        Material material,
        bool keepBottom,
        float vMin,
        float vSize,
        float threshold)
    {
        material.SetFloat("_ClipThreshold", threshold);
        material.SetFloat("_KeepBottom", keepBottom ? 1f : 0f);
        material.SetFloat("_AtlasVMin", vMin);
        material.SetFloat("_AtlasVSize", vSize);
    }

    private static void GetAtlasV(Sprite sprite, out float vMin, out float vSize)
    {
        Texture2D texture = sprite.texture;
        if (texture == null)
        {
            vMin = 0f;
            vSize = 1f;
            return;
        }

        Rect rect = sprite.textureRect;
        vMin = rect.y / texture.height;
        vSize = rect.height / texture.height;
    }

    private static Shader GetClipShader()
    {
        if (clipShader == null)
            clipShader = Shader.Find(SceneLitMaterial.TreeClipShaderName);
        return clipShader;
    }

    private void EnsureRenderers()
    {
        baseRenderer = GetComponent<SpriteRenderer>();
        if (baseRenderer == null)
            return;

        // Não usar SortingGroup no root — quebraria base vs topo.
        SortingGroup group = GetComponent<SortingGroup>();
        if (group != null)
        {
            if (Application.isPlaying)
                Destroy(group);
            else
                DestroyImmediate(group);
        }

        Transform existing = transform.Find(TopChildName);
        if (existing == null)
        {
            GameObject topObject = new(TopChildName);
            topTransform = topObject.transform;
            topTransform.SetParent(transform, false);
            topRenderer = topObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            topTransform = existing;
            topRenderer = existing.GetComponent<SpriteRenderer>();
            if (topRenderer == null)
                topRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }

        topRenderer.color = baseRenderer.color;
        topRenderer.flipX = baseRenderer.flipX;
        topRenderer.flipY = baseRenderer.flipY;
        topRenderer.sortingLayerID = baseRenderer.sortingLayerID;
    }
}
