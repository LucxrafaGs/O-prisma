using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Árvore em duas camadas (mesmo sprite, clip por UV):
/// Base (sombra + tronco) — colisão + Y-sort.
/// Copa (folhas) — sem colisão; na frente do personagem só quando ele está atrás do pé da árvore.
/// Na frente do tronco o personagem desenha na frente das folhas também.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SeasonalTree : MonoBehaviour
{
    private const float BaseHeightFraction = 0.34f;
    /// <summary>
    /// +1 mantém a copa acima do tronco no mesmo pé; NÃO usar +5000 —
    /// isso fazia as folhas cobrirem o player mesmo na frente da árvore.
    /// </summary>
    private const int CanopySortBoost = 1;

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
        RemoveLegacyWholeTreeSorting();
        EnsureRenderers();
        SeedSpriteFromRendererIfNeeded();
        SyncDryFlagFromCatalog();
        ApplySeason(GetCurrentSeason(), force: true);
        EnsureTrunkCollider();
    }

    /// <summary>
    /// Árvores já colocadas na cena (World/Arvores): usa o sprite atual sem trocar arte nem posição.
    /// </summary>
    private void SeedSpriteFromRendererIfNeeded()
    {
        if (springLight != null)
            return;
        if (baseRenderer != null && baseRenderer.sprite != null)
            springLight = baseRenderer.sprite;
    }

    private void OnEnable()
    {
        GameTimeClock.OnTimeChanged += OnClockChanged;
        GameTimeClock.OnDayStarted += OnClockChanged;
        GameTimeClock.OnSeasonChanged += OnClockChanged;
        ApplySeason(GetCurrentSeason(), force: true);
    }

    private void OnDisable()
    {
        GameTimeClock.OnTimeChanged -= OnClockChanged;
        GameTimeClock.OnDayStarted -= OnClockChanged;
        GameTimeClock.OnSeasonChanged -= OnClockChanged;
    }

    private void LateUpdate()
    {
        int order = WorldDepth.OrderFromY(transform.position.y);
        if (baseRenderer != null)
            baseRenderer.sortingOrder = order;

        if (canopyRenderer != null)
            canopyRenderer.sortingOrder = order + CanopySortBoost;

        // Sombra do artist fica logo atrás do tronco no mesmo pé da árvore.
        Transform sombra = transform.Find("Sombra");
        if (sombra != null && sombra.TryGetComponent(out SpriteRenderer sombraRenderer))
            sombraRenderer.sortingOrder = order - 1;
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

        RemoveLegacyWholeTreeSorting();
        EnsureRenderers();
        ApplySeason(GetCurrentSeason(), force: true);
        EnsureTrunkCollider();
    }

    public void ApplySeason(GameTimeClock.Season season, bool force)
    {
        if (!force && season == lastSeason)
            return;

        lastSeason = season;
        EnsureRenderers();
        ApplyFullSprite(ResolveSprite(season));
        EnsureTrunkCollider();
    }

    private void ApplyFullSprite(Sprite full)
    {
        if (full == null || baseRenderer == null || canopyRenderer == null)
            return;

        currentSprite = full;
        canopyTransform.localPosition = Vector3.zero;

        baseRenderer.sprite = full;
        baseRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        canopyRenderer.sprite = full;
        canopyRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        ApplyClipMaterials(full);
    }

    private void ApplyClipMaterials(Sprite full)
    {
        Shader shader = GetClipShader();
        if (shader == null)
        {
            baseRenderer.sharedMaterial = null;
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
        // Primavera = verde mais escuro; Verão = verde mais claro (todas iguais).
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
        // Escala com o sprite (árvores grandes da cena ~20u) sem engolir a área das folhas.
        float width = Mathf.Clamp(bounds.size.x * 0.22f, 0.18f, Mathf.Max(0.85f, bounds.size.x * 0.12f));
        float height = Mathf.Clamp(bounds.size.y * BaseHeightFraction * 0.28f, 0.12f, Mathf.Max(0.35f, bounds.size.y * 0.07f));

        trunkCollider.isTrigger = false;
        trunkCollider.size = new Vector2(width, height);
        trunkCollider.offset = new Vector2(
            bounds.center.x,
            bounds.min.y + height * 0.55f);
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
