using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lanterna: cone + halo. Origem forçada na mão (ignora offsets serializados antigos).
/// De costas, o ponto fica baixo/atrás do torso para não ficar por cima da cabeça.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    // Locais (pivot nos pés). Valores baixos e colados no corpo — não usar SerializeField
    // para o Unity não manter offsets antigos altos no componente da cena.
    private static readonly Vector2 HandLeft = new(-0.04f, 0.035f);
    private static readonly Vector2 HandRight = new(0.04f, 0.035f);
    private static readonly Vector2 HandDown = new(0.02f, 0.03f);
    private static readonly Vector2 HandUp = new(0f, 0.05f);

    /// <summary>Puxa a origem para dentro do corpo (mais perto / para trás).</summary>
    private const float BackInset = 0.06f;

    [Header("Cone (feixe)")]
    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 0.25f;
    [SerializeField] private float intensity = 1.45f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] [Range(10f, 120f)] private float outerSpotAngle = 70f;
    [SerializeField] [Range(5f, 90f)] private float innerSpotAngle = 28f;

    [Header("Halo suave")]
    [SerializeField] private float softOuterRadius = 2.2f;
    [SerializeField] private float softInnerRadius = 0.15f;
    [SerializeField] private float softIntensity = 0.5f;
    [SerializeField] [Range(0.1f, 1f)] private float softFalloff = 0.78f;

    private Light2D spotLight;
    private Light2D softLight;
    private Transform lightTransform;
    private Transform softTransform;
    private PlayerController player;
    private bool isOn;

    public bool IsOn => isOn;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
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

        Vector2 forward = FacingToDirection(facing);
        Vector2 hand = HandOffset(facing);
        // Empurra a origem para trás / para dentro do sprite (mais perto do player).
        Vector2 origin2 = hand - forward * BackInset;

        // De costas: origem bem baixa no torso para o apex ficar sob o sprite, não na cabeça.
        if (facing == PlayerController.Facing.Up)
            origin2 = new Vector2(0f, 0.02f);

        lightTransform.localRotation = Quaternion.Euler(0f, 0f, FacingToZRotation(facing));
        lightTransform.localPosition = new Vector3(origin2.x, origin2.y, 0f);
        spotLight.lightOrder = facing == PlayerController.Facing.Up ? -2 : 0;

        if (softTransform != null)
        {
            // Halo um pouco mais no centro do corpo (ilumina o player).
            Vector2 softPos = facing == PlayerController.Facing.Up
                ? new Vector2(0f, 0.04f)
                : origin2 * 0.5f;
            softTransform.localPosition = new Vector3(softPos.x, softPos.y, 0f);
            softTransform.localRotation = Quaternion.identity;
            if (softLight != null)
                softLight.lightOrder = facing == PlayerController.Facing.Up ? -3 : -1;
        }
    }

    private static Vector2 HandOffset(PlayerController.Facing facing)
    {
        return facing switch
        {
            PlayerController.Facing.Left => HandLeft,
            PlayerController.Facing.Right => HandRight,
            PlayerController.Facing.Up => HandUp,
            PlayerController.Facing.Down => HandDown,
            _ => HandDown
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
        light.falloffIntensity = 0.45f;
        light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
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
        light.lightOrder = -1;
    }
}

public static class CharacterLitMaterial
{
    public static void ApplyToHierarchy(Transform root)
    {
        SceneLitMaterial.ApplyToHierarchy(root);
    }
}
