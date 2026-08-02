using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Silhuetas de sombra de nuvem no chão — formas claras (bolinhas), sem fumaça.
/// Maior parte da tela fica limpa; só as manchas escuras.
/// </summary>
[DefaultExecutionOrder(50)]
public class DappledSunLighting : MonoBehaviour
{
    public static DappledSunLighting Instance { get; private set; }

    private const int TextureSize = 256;
    private const int CloudCount = 3;
    private const int GroundSortOrder = 40;

    private static readonly Color ShadowTintOnActor = new(0.7f, 0.73f, 0.8f, 1f);
    private static readonly Color GroundShadowColor = new(0.1f, 0.12f, 0.18f, 0.5f);

    [Header("Movimento L → R")]
    [SerializeField] private float minSpeed = 0.7f;
    [SerializeField] private float maxSpeed = 1.5f;
    [SerializeField] private float spawnLeftPadding = 12f;
    [SerializeField] private float despawnRightPadding = 14f;
    [SerializeField] private float verticalRange = 7f;

    [Header("Tamanho (pequeno / médio)")]
    [SerializeField] private float minScale = 1.1f;
    [SerializeField] private float maxScale = 2.4f;

    private Transform root;
    private Material shadowMaterial;
    private readonly List<CloudShadow> clouds = new();
    private readonly List<Sprite> cookieVariants = new();
    private readonly Dictionary<CharacterDepthSort, ActorTint> actorTints = new();

    private struct CloudShadow
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public float Speed;
        public float BaseScale;
        public float Phase;
    }

    private sealed class ActorTint
    {
        public SpriteRenderer[] Renderers;
        public Color[] Originals;
        public bool InShadow;
    }

    public static void ClearInstanceForDomainReload() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DayNightLighting.DappledAmbientFloor = 1f;
        CleanupLeftoverCloudShadows();
        // Sombras de nuvem desativadas — não ficaram legais.
        enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DayNightLighting.DappledAmbientFloor = 1f;
        RestoreAllActorTints();

        if (shadowMaterial != null)
            Destroy(shadowMaterial);

        for (int i = 0; i < cookieVariants.Count; i++)
        {
            if (cookieVariants[i] != null && cookieVariants[i].texture != null)
                Destroy(cookieVariants[i].texture);
        }

        cookieVariants.Clear();

        if (root != null)
            Destroy(root.gameObject);
    }

    private void LateUpdate()
    {
        // Desativado: sem movimento de nuvens.
    }

    private static void CleanupLeftoverCloudShadows()
    {
        GameObject[] leftovers = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < leftovers.Length; i++)
        {
            GameObject go = leftovers[i];
            if (go == null)
                continue;
            string n = go.name;
            if (n == "CloudShadows_World" || n == "CloudShadows_Ground" || n == "DappledSunRig" || n == "SunPatches_World"
                || n.StartsWith("CloudSilhouette_"))
                Destroy(go);
        }
    }

    private void Build()
    {
        CleanupLeftoverCloudShadows();
    }

    private void SpawnCloud(Vector2 focus, int index, float yOffset)
    {
        GameObject go = new GameObject($"CloudSilhouette_{index}");
        go.transform.SetParent(root, true);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = cookieVariants[index % cookieVariants.Count];
        renderer.sortingOrder = GroundSortOrder;
        renderer.sharedMaterial = shadowMaterial;
        renderer.color = GroundShadowColor;

        float scale = index == 0
            ? Random.Range(minScale, minScale + 0.5f)
            : index == 1
                ? Random.Range(1.6f, 2.0f)
                : Random.Range(2.0f, maxScale);

        float speed = Random.Range(minSpeed, maxSpeed);
        speed *= Mathf.Lerp(1.2f, 0.8f, Mathf.InverseLerp(minScale, maxScale, scale));

        // X espalhado mas com buracos grandes entre elas.
        float x = focus.x + (index - 1) * 9f + Random.Range(-1.5f, 1.5f);
        float y = focus.y + yOffset;

        go.transform.position = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(scale * 1.25f, scale * 0.65f, 1f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));

        clouds.Add(new CloudShadow
        {
            Transform = go.transform,
            Renderer = renderer,
            Speed = speed,
            BaseScale = scale,
            Phase = Random.Range(0f, 50f)
        });
    }

    private void RespawnOnLeft(ref CloudShadow cloud, Vector2 focus)
    {
        cloud.BaseScale = Random.Range(minScale, maxScale);
        cloud.Speed = Random.Range(minSpeed, maxSpeed)
            * Mathf.Lerp(1.2f, 0.8f, Mathf.InverseLerp(minScale, maxScale, cloud.BaseScale));

        float y = focus.y + Random.Range(-verticalRange, verticalRange);
        cloud.Transform.position = new Vector3(focus.x - spawnLeftPadding - Random.Range(0f, 4f), y, 0f);
        cloud.Transform.localScale = new Vector3(cloud.BaseScale * 1.25f, cloud.BaseScale * 0.65f, 1f);
        cloud.Transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));

        if (cloud.Renderer != null)
        {
            cloud.Renderer.sprite = cookieVariants[Random.Range(0, cookieVariants.Count)];
            cloud.Renderer.color = GroundShadowColor;
            cloud.Renderer.sortingOrder = GroundSortOrder;
            cloud.Renderer.sharedMaterial = shadowMaterial;
        }
    }

    private void UpdateActorShadowTints()
    {
        CharacterDepthSort[] actors = FindObjectsByType<CharacterDepthSort>(FindObjectsInactive.Exclude);
        HashSet<CharacterDepthSort> seen = new();

        for (int a = 0; a < actors.Length; a++)
        {
            CharacterDepthSort actor = actors[a];
            if (actor == null)
                continue;

            seen.Add(actor);
            bool under = IsUnderAnyCloud(actor.transform.position);

            if (!actorTints.TryGetValue(actor, out ActorTint tint)
                || tint.Renderers == null
                || tint.Renderers.Length == 0)
            {
                tint = CaptureActor(actor.transform);
                actorTints[actor] = tint;
            }

            if (under == tint.InShadow)
            {
                if (under)
                    ApplyTint(tint, ShadowTintOnActor);
                continue;
            }

            tint.InShadow = under;
            if (under)
                ApplyTint(tint, ShadowTintOnActor);
            else
                RestoreTint(tint);
        }

        List<CharacterDepthSort> remove = null;
        foreach (var pair in actorTints)
        {
            if (pair.Key != null && seen.Contains(pair.Key))
                continue;
            remove ??= new List<CharacterDepthSort>();
            remove.Add(pair.Key);
            RestoreTint(pair.Value);
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
            actorTints.Remove(remove[i]);
    }

    private bool IsUnderAnyCloud(Vector3 worldPos)
    {
        Vector2 p = worldPos;
        for (int i = 0; i < clouds.Count; i++)
        {
            CloudShadow cloud = clouds[i];
            if (cloud.Transform == null)
                continue;

            Vector2 c = cloud.Transform.position;
            Vector3 lossy = cloud.Transform.lossyScale;
            float rx = Mathf.Abs(lossy.x) * 0.72f;
            float ry = Mathf.Abs(lossy.y) * 0.72f;
            if (rx < 0.05f || ry < 0.05f)
                continue;

            float dx = (p.x - c.x) / rx;
            float dy = (p.y - c.y) / ry;
            if (dx * dx + dy * dy <= 1f)
                return true;
        }

        return false;
    }

    private static ActorTint CaptureActor(Transform actorRoot)
    {
        SpriteRenderer[] renderers = actorRoot.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] originals = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originals[i] = renderers[i] != null ? renderers[i].color : Color.white;

        return new ActorTint { Renderers = renderers, Originals = originals, InShadow = false };
    }

    private static void ApplyTint(ActorTint tint, Color mul)
    {
        if (tint?.Renderers == null)
            return;

        for (int i = 0; i < tint.Renderers.Length; i++)
        {
            SpriteRenderer r = tint.Renderers[i];
            if (r == null)
                continue;
            Color o = tint.Originals[i];
            r.color = new Color(o.r * mul.r, o.g * mul.g, o.b * mul.b, o.a);
        }
    }

    private static void RestoreTint(ActorTint tint)
    {
        if (tint?.Renderers == null)
            return;

        for (int i = 0; i < tint.Renderers.Length; i++)
        {
            if (tint.Renderers[i] != null)
                tint.Renderers[i].color = tint.Originals[i];
        }
    }

    private void RestoreAllActorTints()
    {
        foreach (var pair in actorTints)
            RestoreTint(pair.Value);
        actorTints.Clear();
    }

    private static Vector2 GetFocus()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
            return player.transform.position;

        Camera cam = Camera.main;
        return cam != null ? (Vector2)cam.transform.position : Vector2.zero;
    }

    private static Material CreateSilhouetteMaterial()
    {
        // Unlit alpha = silhueta sólida, sem multiply “fumacento”.
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        return new Material(shader)
        {
            name = "CloudSilhouette_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// <summary>
    /// Nuvem caricata bem legível: bolinhas unidas, borda SECA (quase sem fade).
    /// </summary>
    private static Sprite GenerateHardCloudSilhouette(int size, int variant)
    {
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false)
        {
            name = $"CloudHardSilhouette_{variant}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        float inv = 1f / size;

        // Centros/radius em UV — formato de nuvem clássico bem marcado.
        Vector2[] blobs;
        float[] radii;

        switch (variant)
        {
            case 1:
                blobs = new[]
                {
                    new Vector2(0.40f, 0.48f),
                    new Vector2(0.52f, 0.50f),
                    new Vector2(0.62f, 0.46f),
                    new Vector2(0.30f, 0.44f),
                    new Vector2(0.46f, 0.60f),
                    new Vector2(0.56f, 0.58f)
                };
                radii = new[] { 0.18f, 0.17f, 0.14f, 0.13f, 0.13f, 0.12f };
                break;
            case 2:
                blobs = new[]
                {
                    new Vector2(0.50f, 0.48f),
                    new Vector2(0.38f, 0.46f),
                    new Vector2(0.62f, 0.46f),
                    new Vector2(0.44f, 0.58f),
                    new Vector2(0.56f, 0.58f),
                    new Vector2(0.28f, 0.40f),
                    new Vector2(0.70f, 0.40f)
                };
                radii = new[] { 0.16f, 0.15f, 0.15f, 0.13f, 0.12f, 0.11f, 0.11f };
                break;
            default:
                blobs = new[]
                {
                    new Vector2(0.44f, 0.48f),
                    new Vector2(0.56f, 0.48f),
                    new Vector2(0.34f, 0.44f),
                    new Vector2(0.64f, 0.44f),
                    new Vector2(0.50f, 0.58f)
                };
                radii = new[] { 0.17f, 0.17f, 0.14f, 0.14f, 0.14f };
                break;
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) * inv;
                float v = (y + 0.5f) * inv;

                bool inside = false;
                for (int b = 0; b < blobs.Length; b++)
                {
                    float dx = u - blobs[b].x;
                    float dy = (v - blobs[b].y) * 1.05f;
                    float ang = Mathf.Atan2(dy, dx);
                    // Contorno ondulado leve, ainda nítido.
                    float wave = 1f + 0.08f * Mathf.Sin(ang * 6f + b * 1.3f);
                    float r = radii[b] * wave;
                    if (dx * dx + dy * dy <= r * r)
                    {
                        inside = true;
                        break;
                    }
                }

                // Preenchimento sólido: ou sombra ou nada. Sem fumaça.
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        // Anti-alias mínimo só 1px na borda (silhueta limpa, não smoke).
        Color32[] edged = new Color32[pixels.Length];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (pixels[i].a == 255)
                {
                    edged[i] = pixels[i];
                    continue;
                }

                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                            continue;
                        if (pixels[ny * size + nx].a == 255)
                        {
                            near = true;
                            break;
                        }
                    }
                }

                edged[i] = near ? new Color32(255, 255, 255, 90) : pixels[i];
            }
        }

        tex.SetPixels32(edged);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
