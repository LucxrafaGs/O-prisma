using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Planta decorativa com balanço elástico.
/// Mini árvores: base (tronco) na frente do personagem, copa sempre atrás.
/// Gramas/flores: Y-sort simples, sem colisão sólida.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ElasticFoliage : MonoBehaviour
{
    private const float BaseHeightFraction = 0.48f;
    private const int CanopySortBoost = 1;

    private static Shader clipShader;

    [SerializeField] private string plantTypeId;
    [SerializeField] private bool hasTrunkCollision;
    [SerializeField] private float maxAngle = 22f;
    [SerializeField] private float spring = 95f;
    [SerializeField] private float damping = 9f;
    [SerializeField] private float enterImpulse = 55f;
    [SerializeField] private float stayImpulse = 18f;

    private float angle;
    private float angularVelocity;
    private int overlappingActors;
    private CircleCollider2D swayTrigger;
    private BoxCollider2D trunkCollider;

    private SpriteRenderer baseRenderer;
    private SpriteRenderer canopyRenderer;
    private Transform canopyTransform;
    private Material baseClipMaterial;
    private Material canopyClipMaterial;
    private Sprite currentSprite;

    public string PlantTypeId => plantTypeId;
    public bool HasTrunkCollision => hasTrunkCollision;

    private void Awake()
    {
        ResolveTrunkFlag();
        EnsureRenderers();
        ApplyPresentation();
        EnsureColliders();
    }

    private void OnEnable()
    {
        angle = 0f;
        angularVelocity = 0f;
        overlappingActors = 0;
        transform.localRotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        if (baseClipMaterial != null)
            Destroy(baseClipMaterial);
        if (canopyClipMaterial != null)
            Destroy(canopyClipMaterial);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float accel = (-spring * angle) - (damping * angularVelocity);
        angularVelocity += accel * dt;
        angle += angularVelocity * dt;
        angle = Mathf.Clamp(angle, -maxAngle, maxAngle);

        if (overlappingActors > 0 && Mathf.Abs(angularVelocity) < 8f && Mathf.Abs(angle) < 3f)
            angularVelocity += (Random.value < 0.5f ? -1f : 1f) * stayImpulse * dt;

        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void LateUpdate()
    {
        if (!hasTrunkCollision || baseRenderer == null)
            return;

        float y = transform.position.y;
        baseRenderer.sortingLayerID = 0;
        baseRenderer.sortingOrder = WorldDepth.ActorOrderFromY(y);
        if (canopyRenderer != null && canopyRenderer.enabled)
        {
            canopyRenderer.sortingLayerID = 0;
            canopyRenderer.sortingOrder = WorldDepth.ActorOrderFromY(y);

            ShadowCaster2D leftover = canopyRenderer.GetComponent<ShadowCaster2D>();
            if (leftover != null)
                Destroy(leftover);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetActorBody(other, out Rigidbody2D body))
            return;

        overlappingActors++;
        float dir = ResolvePushDirection(body, other.transform);
        angularVelocity += dir * enterImpulse;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetActorBody(other, out _))
            return;

        overlappingActors = Mathf.Max(0, overlappingActors - 1);
    }

    public void Configure(GardenInteractCatalog.PlantType type, Sprite sprite)
    {
        plantTypeId = type.Id;
        hasTrunkCollision = type.HasTrunkCollision;
        name = $"Plant_{type.Id}";
        currentSprite = sprite;

        EnsureRenderers();
        if (baseRenderer != null)
        {
            baseRenderer.sprite = sprite;
            baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        ApplyPresentation();
        EnsureColliders();
    }

    public void RefreshPresentationKeepTransform()
    {
        ResolveTrunkFlag();
        EnsureRenderers();
        if (baseRenderer != null && baseRenderer.sprite != null)
            currentSprite = baseRenderer.sprite;

        ApplyPresentation();
        EnsureColliders();
    }

    private void ResolveTrunkFlag()
    {
        if (!string.IsNullOrEmpty(plantTypeId) &&
            GardenInteractCatalog.TryGetById(plantTypeId, out GardenInteractCatalog.PlantType type))
        {
            hasTrunkCollision = type.HasTrunkCollision;
            return;
        }

        hasTrunkCollision = GardenInteractCatalog.HasTrunkCollision(plantTypeId) ||
                            GardenInteractCatalog.HasTrunkCollision(name);
    }

    private void ApplyPresentation()
    {
        EnsureRenderers();
        Sprite sprite = currentSprite != null
            ? currentSprite
            : baseRenderer != null ? baseRenderer.sprite : null;

        if (sprite == null)
            return;

        currentSprite = sprite;
        baseRenderer.sprite = sprite;
        baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        if (hasTrunkCollision)
        {
            RemoveLegacyWholePlantSorting();
            EnsureCanopy();
            canopyRenderer.enabled = true;
            canopyRenderer.sprite = sprite;
            canopyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            canopyTransform.localPosition = Vector3.zero;
            ApplyClipMaterials(sprite);
        }
        else
        {
            DisableCanopy();
            SceneLitMaterial.ApplyTo(baseRenderer);
            EnsureFlatDepthSort();
        }
    }

    private void ApplyClipMaterials(Sprite full)
    {
        Shader shader = GetClipShader();
        if (shader == null)
        {
            SceneLitMaterial.ApplyTo(baseRenderer);
            if (canopyRenderer != null)
                canopyRenderer.enabled = false;
            return;
        }

        if (baseClipMaterial == null)
        {
            baseClipMaterial = new Material(shader)
            {
                name = "MiniTreeBaseClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (canopyClipMaterial == null)
        {
            canopyClipMaterial = new Material(shader)
            {
                name = "MiniTreeCanopyClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        GetAtlasV(full, out float vMin, out float vSize);
        ConfigureLayerMaterial(baseClipMaterial, keepBottom: true, vMin, vSize);
        ConfigureLayerMaterial(canopyClipMaterial, keepBottom: false, vMin, vSize);
        baseRenderer.sharedMaterial = baseClipMaterial;
        canopyRenderer.sharedMaterial = canopyClipMaterial;
    }

    private static void ConfigureLayerMaterial(Material material, bool keepBottom, float vMin, float vSize)
    {
        material.SetFloat("_ClipThreshold", BaseHeightFraction);
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
            baseRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    private void EnsureCanopy()
    {
        Transform existing = transform.Find("Canopy");
        if (existing == null)
        {
            GameObject canopyObject = new("Canopy");
            canopyTransform = canopyObject.transform;
            canopyTransform.SetParent(transform, false);
            canopyRenderer = canopyObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            existing.gameObject.SetActive(true);
            canopyTransform = existing;
            canopyRenderer = existing.GetComponent<SpriteRenderer>();
            if (canopyRenderer == null)
                canopyRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void DisableCanopy()
    {
        Transform existing = transform.Find("Canopy");
        if (existing != null)
            existing.gameObject.SetActive(false);

        canopyRenderer = null;
        canopyTransform = null;
    }

    private void EnsureColliders()
    {
        Sprite sprite = currentSprite != null
            ? currentSprite
            : baseRenderer != null ? baseRenderer.sprite : null;

        swayTrigger = GetComponent<CircleCollider2D>();
        if (swayTrigger == null)
            swayTrigger = gameObject.AddComponent<CircleCollider2D>();

        swayTrigger.isTrigger = true;
        if (sprite != null)
        {
            Bounds bounds = sprite.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * 0.7f;
            swayTrigger.radius = Mathf.Clamp(radius, 0.12f, 0.5f);
            swayTrigger.offset = new Vector2(bounds.center.x, bounds.min.y + swayTrigger.radius * 0.4f);
        }

        trunkCollider = GetComponent<BoxCollider2D>();
        if (hasTrunkCollision)
        {
            if (trunkCollider == null)
                trunkCollider = gameObject.AddComponent<BoxCollider2D>();

            trunkCollider.isTrigger = false;
            if (sprite != null)
            {
                Bounds bounds = sprite.bounds;
                float width = Mathf.Clamp(bounds.size.x * 0.35f, 0.1f, 0.35f);
                float height = Mathf.Clamp(bounds.size.y * 0.22f, 0.08f, 0.28f);
                trunkCollider.size = new Vector2(width, height);
                trunkCollider.offset = new Vector2(bounds.center.x, bounds.min.y + height * 0.55f);
            }
        }
        else if (trunkCollider != null)
        {
            if (Application.isPlaying)
                Destroy(trunkCollider);
            else
                DestroyImmediate(trunkCollider);
            trunkCollider = null;
        }
    }

    private void EnsureFlatDepthSort()
    {
        if (GetComponent<SortingGroup>() == null)
            gameObject.AddComponent<SortingGroup>();

        if (GetComponent<CharacterDepthSort>() == null)
            gameObject.AddComponent<CharacterDepthSort>();
    }

    private void RemoveLegacyWholePlantSorting()
    {
        CharacterDepthSort depthSort = GetComponent<CharacterDepthSort>();
        if (depthSort != null)
        {
            if (Application.isPlaying)
                Destroy(depthSort);
            else
                DestroyImmediate(depthSort);
        }

        SortingGroup sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup != null)
        {
            if (Application.isPlaying)
                Destroy(sortingGroup);
            else
                DestroyImmediate(sortingGroup);
        }
    }

    private float ResolvePushDirection(Rigidbody2D body, Transform actor)
    {
        float vx = body != null ? body.linearVelocity.x : 0f;
        if (Mathf.Abs(vx) > 0.05f)
            return Mathf.Sign(vx);

        float dx = actor.position.x - transform.position.x;
        return Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : 1f;
    }

    private static bool TryGetActorBody(Collider2D other, out Rigidbody2D body)
    {
        body = null;
        if (other == null || other.isTrigger)
            return false;

        body = other.attachedRigidbody;
        if (body == null)
            return false;

        return body.GetComponent<PlayerController>() != null ||
               body.GetComponent<NpcController>() != null;
    }
}
