using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Regra geral de profundidade (igual aos postes):
/// Base (até o topo do collider) — Y-sort, player passa por cima.
/// Topo (acima do collider) — sempre na frente, player passa por baixo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(-35)]
public class PropDepthSplit : MonoBehaviour
{
    public const string TopChildName = "PropTop";
    public const int TopSortBoost = 5000;

    private static Shader clipShader;

    private SpriteRenderer baseRenderer;
    private SpriteRenderer topRenderer;
    private Transform topTransform;
    private Collider2D footCollider;
    private Material baseMaterial;
    private Material topMaterial;
    private float clipThreshold = 0.35f;

    private void Awake()
    {
        // Evita conflito com splits específicos já existentes.
        if (GetComponent<StreetLampDepthSplit>() != null ||
            GetComponent<SeasonalTree>() != null ||
            GetComponent<ElasticFoliage>() != null)
        {
            enabled = false;
            return;
        }

        ResolveCollider();
        EnsureRenderers();
        RebuildClip();
    }

    private void OnEnable()
    {
        if (!enabled)
            return;
        ResolveCollider();
        EnsureRenderers();
        RebuildClip();
    }

    private void LateUpdate()
    {
        if (baseRenderer == null)
            return;

        int order = WorldDepth.OrderFromY(transform.position.y);
        baseRenderer.sortingOrder = order;
        if (topRenderer != null && topRenderer.enabled)
            topRenderer.sortingOrder = order + TopSortBoost;
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

        ResolveCollider();
        clipThreshold = ComputeClipThresholdFromCollider(sprite);
        topTransform.localPosition = Vector3.zero;

        baseRenderer.sprite = sprite;
        baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        topRenderer.sprite = sprite;
        topRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        topRenderer.enabled = true;

        ApplyClipMaterials(sprite);
    }

    private void ResolveCollider()
    {
        footCollider = GetComponent<Collider2D>();
        if (footCollider != null && footCollider.enabled && !footCollider.isTrigger)
            return;

        // Empty filho com collider (ex.: casas com colisão em child).
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        footCollider = null;
        float bestBottom = float.PositiveInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;

            float bottom = c.bounds.min.y;
            if (bottom < bestBottom)
            {
                bestBottom = bottom;
                footCollider = c;
            }
        }
    }

    private float ComputeClipThresholdFromCollider(Sprite sprite)
    {
        Bounds spriteLocal = sprite.bounds;
        if (spriteLocal.size.y < 0.0001f)
            return 0.35f;

        if (footCollider == null || !footCollider.enabled)
            return 0.35f;

        float colliderTopLocalY;
        if (footCollider is BoxCollider2D box && footCollider.transform == transform)
        {
            colliderTopLocalY = box.offset.y + box.size.y * 0.5f;
        }
        else
        {
            // Converte o topo do collider (world) para espaço local do sprite/pivot.
            Vector3 topWorld = new(
                footCollider.bounds.center.x,
                footCollider.bounds.max.y,
                transform.position.z);
            colliderTopLocalY = transform.InverseTransformPoint(topWorld).y;
        }

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
                name = "PropBaseClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (topMaterial == null)
        {
            topMaterial = new Material(shader)
            {
                name = "PropTopClip",
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

        SortingGroup group = GetComponent<SortingGroup>();
        if (group != null)
        {
            if (Application.isPlaying)
                Destroy(group);
            else
                DestroyImmediate(group);
        }

        CharacterDepthSort depthSort = GetComponent<CharacterDepthSort>();
        if (depthSort != null)
        {
            if (Application.isPlaying)
                Destroy(depthSort);
            else
                DestroyImmediate(depthSort);
        }

        Transform existing = transform.Find(TopChildName);
        if (existing == null)
        {
            // Compatível com postes antigos.
            existing = transform.Find(StreetLampDepthSplit.TopChildName);
        }

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
