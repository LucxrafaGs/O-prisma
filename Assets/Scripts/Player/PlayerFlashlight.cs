using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lanterna: cone + halo ancorados no corpo (bounds do sprite).
/// De costas o ponto fica no torso baixo (sob o desenho do player).
/// Shadows + casters nas copas evitam o feixe “por cima” das folhas quando estamos atrás.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    [Header("Cone (feixe)")]
    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 0.2f;
    [SerializeField] private float intensity = 1.4f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] [Range(10f, 120f)] private float outerSpotAngle = 68f;
    [SerializeField] [Range(5f, 90f)] private float innerSpotAngle = 26f;
    [SerializeField] [Range(0f, 1f)] private float shadowIntensity = 0.85f;

    [Header("Halo suave")]
    [SerializeField] private float softOuterRadius = 2.0f;
    [SerializeField] private float softInnerRadius = 0.12f;
    [SerializeField] private float softIntensity = 0.45f;
    [SerializeField] [Range(0.1f, 1f)] private float softFalloff = 0.8f;

    // Fração da altura do sprite (pés=0, cabeça=1).
    private const float HandHeightSide = 0.22f;
    private const float HandHeightDown = 0.18f;
    private const float HandHeightUp = 0.20f; // torso baixo — sob o sprite de costas
    private const float HandSideInset = 0.28f; // 0=centro, 1=borda da mão
    private const float ForwardNudge = 0.06f; // um pouco para frente
    private const float DownNudge = 0.03f; // um pouco para baixo

    private Light2D spotLight;
    private Light2D softLight;
    private Transform lightTransform;
    private Transform softTransform;
    private PlayerController player;
    private PlayerAppearance appearance;
    private SortingGroup sortingGroup;
    private bool isOn;

    public bool IsOn => isOn;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        appearance = GetComponent<PlayerAppearance>();
        sortingGroup = GetComponent<SortingGroup>();
        EnsureLight();
        SetEnabled(false);
        CharacterLitMaterial.ApplyToHierarchy(transform);
    }

    private void Update()
    {
        if (PrismaBackpackMenu.IsOpen || DevModeController.IsOpen)
            return;

        if (GameTimeClock.Instance != null && GameTimeClock.Instance.IsSleepPending)
            return;

        if (!IsHoldingLantern())
        {
            if (isOn)
                SetEnabled(false);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            SetEnabled(!isOn);
    }

    private void LateUpdate()
    {
        if (spotLight == null || lightTransform == null)
            return;

        AimLightAtFacing();
    }

    public void SetEnabled(bool enabled)
    {
        isOn = enabled && IsHoldingLantern();
        if (spotLight != null)
            spotLight.enabled = isOn;
        if (softLight != null)
            softLight.enabled = isOn;
    }

    private void AimLightAtFacing()
    {
        PlayerController.Facing facing = player != null
            ? player.CurrentFacing
            : PlayerController.Facing.Down;

        Vector3 origin = ComputeHandWorldPosition(facing);
        Vector2 forward = FacingToDirection(facing);

        // Um pouco mais para baixo e para frente (pedido).
        origin += (Vector3)(forward * ForwardNudge);
        origin.y -= DownNudge;

        // De costas: mantém o ponto no torso baixo (já em HandHeightUp) e
        // atrás do sorting do player — lightOrder baixo + origem coberta pelo sprite.
        if (facing == PlayerController.Facing.Up)
        {
            // Não sobe para a cabeça: trava Y no terço inferior do corpo.
            if (TryGetBodyBounds(out Bounds body))
            {
                float maxY = Mathf.Lerp(body.min.y, body.max.y, 0.28f);
                if (origin.y > maxY)
                    origin.y = maxY;
                origin.x = body.center.x;
            }
        }

        lightTransform.position = origin;
        lightTransform.rotation = Quaternion.Euler(0f, 0f, FacingToZRotation(facing));

        // Sincroniza “profundidade” do feixe com o Y-sort do player (vs. outras luzes).
        int depth = sortingGroup != null
            ? sortingGroup.sortingOrder
            : WorldDepth.ActorOrderFromY(transform.position.y);
        spotLight.lightOrder = facing == PlayerController.Facing.Up ? depth - 20 : depth;

        if (softTransform != null)
        {
            Vector3 softOrigin = origin;
            if (facing == PlayerController.Facing.Up && TryGetBodyBounds(out Bounds bodySoft))
                softOrigin = new Vector3(bodySoft.center.x, Mathf.Lerp(bodySoft.min.y, bodySoft.max.y, 0.22f), 0f);

            softTransform.position = softOrigin;
            softTransform.rotation = Quaternion.identity;
            if (softLight != null)
                softLight.lightOrder = spotLight.lightOrder - 1;
        }
    }

    private Vector3 ComputeHandWorldPosition(PlayerController.Facing facing)
    {
        if (!TryGetBodyBounds(out Bounds body))
        {
            Vector2 fallback = (Vector2)transform.position + HandOffsetFallback(facing);
            return fallback;
        }

        float heightT = facing switch
        {
            PlayerController.Facing.Up => HandHeightUp,
            PlayerController.Facing.Down => HandHeightDown,
            _ => HandHeightSide
        };

        float y = Mathf.Lerp(body.min.y, body.max.y, heightT);
        float x = body.center.x;

        switch (facing)
        {
            case PlayerController.Facing.Left:
                x = Mathf.Lerp(body.center.x, body.min.x, HandSideInset);
                break;
            case PlayerController.Facing.Right:
                x = Mathf.Lerp(body.center.x, body.max.x, HandSideInset);
                break;
            case PlayerController.Facing.Down:
                x = Mathf.Lerp(body.center.x, body.max.x, 0.15f);
                break;
            case PlayerController.Facing.Up:
                // Centro do corpo, baixo — o sprite cobre o ponto.
                x = body.center.x;
                break;
        }

        return new Vector3(x, y, 0f);
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = default;
        SpriteRenderer body = appearance != null ? appearance.BodyRenderer : null;
        if (body == null || body.sprite == null)
            return false;
        bounds = body.bounds;
        return true;
    }

    private static Vector2 HandOffsetFallback(PlayerController.Facing facing)
    {
        return facing switch
        {
            PlayerController.Facing.Left => new Vector2(-0.15f, 0.35f),
            PlayerController.Facing.Right => new Vector2(0.15f, 0.35f),
            PlayerController.Facing.Up => new Vector2(0f, 0.25f),
            _ => new Vector2(0.05f, 0.3f)
        };
    }

    private static Vector2 FacingToDirection(PlayerController.Facing facing)
    {
        return facing switch
        {
            PlayerController.Facing.Up => Vector2.up,
            PlayerController.Facing.Down => Vector2.down,
            PlayerController.Facing.Left => Vector2.left,
            PlayerController.Facing.Right => Vector2.right,
            _ => Vector2.down
        };
    }

    private static float FacingToZRotation(PlayerController.Facing facing)
    {
        return facing switch
        {
            PlayerController.Facing.Up => 0f,
            PlayerController.Facing.Right => -90f,
            PlayerController.Facing.Down => 180f,
            PlayerController.Facing.Left => 90f,
            _ => 180f
        };
    }

    private static bool IsHoldingLantern()
    {
        PlayerHotbar.EnsureDefaults();
        PlayerHotbar.HeldItem current = PlayerHotbar.Current;
        return current != null && current.Id == LanternItemId;
    }

    private void EnsureLight()
    {
        lightTransform = EnsureChild("Flashlight");
        spotLight = EnsureLight2D(lightTransform.gameObject);
        ConfigureSpot(spotLight);

        softTransform = EnsureChild("FlashlightSoft");
        softLight = EnsureLight2D(softTransform.gameObject);
        ConfigureSoft(softLight);

        AimLightAtFacing();
    }

    private Transform EnsureChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
            return existing;

        GameObject go = new(childName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private static Light2D EnsureLight2D(GameObject host)
    {
        Light2D light = host.GetComponent<Light2D>();
        if (light == null)
            light = host.AddComponent<Light2D>();
        return light;
    }

    private void ConfigureSpot(Light2D light)
    {
        light.lightType = Light2D.LightType.Point;
        light.color = lightColor;
        light.intensity = intensity;
        light.pointLightInnerRadius = innerRadius;
        light.pointLightOuterRadius = outerRadius;
        light.pointLightInnerAngle = Mathf.Clamp(innerSpotAngle, 5f, outerSpotAngle);
        light.pointLightOuterAngle = Mathf.Clamp(outerSpotAngle, 10f, 120f);
        light.falloffIntensity = 0.5f;
        light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        light.shadowsEnabled = true;
        light.shadowIntensity = shadowIntensity;
    }

    private void ConfigureSoft(Light2D light)
    {
        light.lightType = Light2D.LightType.Point;
        light.color = lightColor;
        light.intensity = softIntensity;
        light.pointLightInnerRadius = softInnerRadius;
        light.pointLightOuterRadius = softOuterRadius;
        light.pointLightInnerAngle = 360f;
        light.pointLightOuterAngle = 360f;
        light.falloffIntensity = softFalloff;
        light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        light.shadowsEnabled = true;
        light.shadowIntensity = shadowIntensity * 0.65f;
    }
}

public static class CharacterLitMaterial
{
    public static void ApplyToHierarchy(Transform root)
    {
        SceneLitMaterial.ApplyToHierarchy(root);
    }
}
