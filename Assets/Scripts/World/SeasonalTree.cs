using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Árvore em duas camadas (mesmo sprite, clip por UV):
/// Base = sombra+tronco (colisão). Copa = folhas.
/// Sort no <b>topo do tronco</b>; base e copa no mesmo order — player na frente do tronco
/// fica na frente das folhas; só vai atrás ao passar o tronco para o norte.
/// Orders 0–19 respeitam overlays Order 20+ e a neblina.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SeasonalTree : MonoBehaviour
{
    public const string CanopyChildName = "Canopy";
    public const string FoliageSortingLayer = "Foliage";

    /// <summary>Fim do tronco / início da copa no clip UV.</summary>
    private const float BaseHeightFraction = 0.36f;

    private static Shader clipShader;

    [SerializeField] private string treeTypeId;
    [SerializeField] private Sprite springLight;
    [SerializeField] private Sprite summerDark;
    [SerializeField] private Sprite autumnOrange;
    [SerializeField] private Sprite autumnYellow;
    [SerializeField] private Sprite winterDry;
    [SerializeField] private bool preferDarkInSummer;
    [SerializeField] private bool preferYellowInAutumn;
    [SerializeField] private bool usesDryInWinter;

    private SpriteRenderer baseRenderer;
    private SpriteRenderer canopyRenderer;
    private Transform canopyTransform;
    private BoxCollider2D trunkCollider;
    private Material baseMaterial;
    private Material canopyMaterial;
    private GameTimeClock.Season lastSeason = (GameTimeClock.Season)(-1);
    private Sprite currentSprite;

    public string TreeTypeId => treeTypeId;

    private void Awake()
    {
        SetupTree();
    }

    private void OnEnable()
    {
        GameTimeClock.OnTimeChanged += OnClockChanged;
        GameTimeClock.OnDayStarted += OnClockChanged;
        GameTimeClock.OnSeasonChanged += OnClockChanged;
        SetupTree();
    }

    private void OnDisable()
    {
        GameTimeClock.OnTimeChanged -= OnClockChanged;
        GameTimeClock.OnDayStarted -= OnClockChanged;
        GameTimeClock.OnSeasonChanged -= OnClockChanged;
    }

    public void SetupTree()
    {
        RemoveLegacyWholeTreeSorting();
        EnsureRenderers();
        SeedSpriteFromRendererIfNeeded();
        SyncDryFlagFromCatalog();
        ApplySeason(GetCurrentSeason(), force: true);
        EnsureTrunkCollider();
        ApplySorting();
    }

    private void SeedSpriteFromRendererIfNeeded()
    {
        if (springLight != null)
            return;
        if (baseRenderer != null && baseRenderer.sprite != null)
            springLight = baseRenderer.sprite;
    }

    private void LateUpdate()
    {
        ApplySorting();
    }

    private void ApplySorting()
    {
        if (baseRenderer == null)
            EnsureRenderers();

        // Topo do tronco: player ainda no tronco/sombra fica NA FRENTE da árvore inteira
        // (base + folhas no mesmo order). Só atrás ao cruzar o tronco para o norte.
        float sortY = GetTrunkTopWorldY();
        int treeOrder = WorldDepth.ActorOrderFromY(sortY);
        int shadowOrder = WorldDepth.ShadowOrderFromY(sortY);

        if (baseRenderer != null)
        {
            baseRenderer.sortingLayerID = 0;
            baseRenderer.sortingOrder = treeOrder;
            baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (canopyRenderer != null && canopyRenderer.enabled)
        {
            canopyRenderer.sortingLayerID = 0;
            canopyRenderer.sortingOrder = treeOrder; // igual à base — não cobrir cedo demais
            canopyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        Transform sombra = transform.Find("Sombra");
        if (sombra != null && sombra.TryGetComponent(out SpriteRenderer sombraRenderer))
        {
            sombraRenderer.sortingLayerID = 0;
            sombraRenderer.sortingOrder = shadowOrder;
        }
    }

    /// <summary>Y do fim do tronco (onde a copa “começa” na lógica de profundidade).</summary>
    private float GetTrunkTopWorldY()
    {
        if (trunkCollider != null && trunkCollider.enabled)
            return trunkCollider.bounds.max.y;

        if (baseRenderer != null && baseRenderer.sprite != null)
        {
            Bounds b = baseRenderer.bounds;
            return Mathf.Lerp(b.min.y, b.max.y, BaseHeightFraction);
        }

        return transform.position.y;
    }

    private void OnClockChanged()
    {
        ApplySeason(GetCurrentSeason(), force: false);
    }

    public void Configure(
        NatureTreeCatalog.TreeType type,
        Sprite spring,
        Sprite summerDarkSprite,
        Sprite autumnOrangeSprite,
        Sprite autumnYellowSprite,
        Sprite winterDrySprite,
        bool darkInSummer,
        bool yellowInAutumn)
    {
        treeTypeId = type.Id;
        springLight = spring;
        summerDark = summerDarkSprite;
        autumnOrange = autumnOrangeSprite;
        autumnYellow = autumnYellowSprite;
        winterDry = winterDrySprite;
        preferDarkInSummer = darkInSummer;
        preferYellowInAutumn = yellowInAutumn;
        usesDryInWinter = type.UsesDryInWinter;
        name = $"Tree_{type.Id}";
        SetupTree();
    }

    public void ApplySeason(GameTimeClock.Season season, bool force)
    {
        if (!force && season == lastSeason)
            return;

        lastSeason = season;
        EnsureRenderers();
        ApplyFullSprite(ResolveSprite(season));
        EnsureTrunkCollider();
        ApplySorting();
    }

    private void ApplyFullSprite(Sprite full)
    {
        if (full == null || baseRenderer == null || canopyRenderer == null)
            return;

        currentSprite = full;
        canopyTransform.localPosition = Vector3.zero;

        baseRenderer.sprite = full;
        canopyRenderer.sprite = full;
        ApplyClipMaterials(full);
    }

    private void ApplyClipMaterials(Sprite full)
    {
        Shader shader = GetClipShader();
        if (shader == null)
        {
            Debug.LogError(
                $"Prisma: shader {SceneLitMaterial.TreeClipShaderName} ausente em '{name}'. Copa desativada.",
                this);
            baseRenderer.sharedMaterial = SceneLitMaterial.GetLitMaterial();
            canopyRenderer.enabled = false;
            return;
        }

        canopyRenderer.enabled = true;
        EnsureLayerMaterials(shader);
        GetAtlasV(full, out float vMin, out float vSize);

        ConfigureLayerMaterial(baseMaterial, keepBottom: true, vMin, vSize);
        ConfigureLayerMaterial(canopyMaterial, keepBottom: false, vMin, vSize);

        baseRenderer.sharedMaterial = baseMaterial;
        canopyRenderer.sharedMaterial = canopyMaterial;
        canopyRenderer.color = baseRenderer.color;
        canopyRenderer.flipX = baseRenderer.flipX;
        canopyRenderer.flipY = baseRenderer.flipY;
    }

    private void EnsureLayerMaterials(Shader shader)
    {
        if (baseMaterial == null)
        {
            baseMaterial = new Material(shader)
            {
                name = "TreeBaseClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (canopyMaterial == null)
        {
            canopyMaterial = new Material(shader)
            {
                name = "TreeCanopyClip",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
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
        if (clipShader != null)
            return clipShader;

#if UNITY_EDITOR
        clipShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Scripts/World/SpriteLitVerticalClip.shader");
#endif
        if (clipShader == null)
            clipShader = Shader.Find(SceneLitMaterial.TreeClipShaderName);

        return clipShader;
    }

    private void OnDestroy()
    {
        if (baseMaterial != null)
            Destroy(baseMaterial);
        if (canopyMaterial != null)
            Destroy(canopyMaterial);
    }

    private Sprite ResolveSprite(GameTimeClock.Season season)
    {
        return season switch
        {
            GameTimeClock.Season.Primavera => First(summerDark, springLight),
            GameTimeClock.Season.Verao => First(springLight, summerDark),
            GameTimeClock.Season.Outono => ResolveAutumn(),
            GameTimeClock.Season.Inverno => usesDryInWinter
                ? First(winterDry, ResolveAutumn())
                : ResolveAutumn(),
            _ => springLight
        };
    }

    private Sprite ResolveAutumn()
    {
        return preferYellowInAutumn
            ? First(autumnYellow, autumnOrange)
            : First(autumnOrange, autumnYellow);
    }

    private static Sprite First(Sprite a, Sprite b) => a != null ? a : b;

    private void SyncDryFlagFromCatalog()
    {
        if (!string.IsNullOrEmpty(treeTypeId))
            usesDryInWinter = NatureTreeCatalog.UsesDryInWinter(treeTypeId);
    }

    private void EnsureRenderers()
    {
        baseRenderer = GetComponent<SpriteRenderer>();
        if (baseRenderer == null)
            baseRenderer = gameObject.AddComponent<SpriteRenderer>();

        Transform existing = transform.Find(CanopyChildName);
        if (existing == null)
        {
            GameObject canopyObject = new(CanopyChildName);
            canopyTransform = canopyObject.transform;
            canopyTransform.SetParent(transform, false);
            canopyRenderer = canopyObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            canopyTransform = existing;
            canopyRenderer = existing.GetComponent<SpriteRenderer>();
            if (canopyRenderer == null)
                canopyRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void EnsureTrunkCollider()
    {
        EnsureRenderers();
        Sprite sprite = currentSprite != null ? currentSprite : baseRenderer != null ? baseRenderer.sprite : null;
        if (sprite == null)
            return;

        if (trunkCollider == null)
            trunkCollider = GetComponent<BoxCollider2D>();

        if (trunkCollider == null)
            trunkCollider = gameObject.AddComponent<BoxCollider2D>();

        Bounds bounds = sprite.bounds;
        float width = Mathf.Clamp(bounds.size.x * 0.38f, 0.25f, Mathf.Max(1.1f, bounds.size.x * 0.2f));
        float height = Mathf.Clamp(bounds.size.y * BaseHeightFraction * 0.5f, 0.2f, Mathf.Max(0.55f, bounds.size.y * 0.12f));

        trunkCollider.isTrigger = false;
        trunkCollider.size = new Vector2(width, height);
        trunkCollider.offset = new Vector2(
            bounds.center.x,
            bounds.min.y + height * 0.5f);
    }

    private void RemoveLegacyWholeTreeSorting()
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

    private static GameTimeClock.Season GetCurrentSeason()
    {
        return GameTimeClock.Instance != null
            ? GameTimeClock.Instance.CurrentSeason
            : GameTimeClock.Season.Primavera;
    }
}
