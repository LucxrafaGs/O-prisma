using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lanterna: cone direcionado + halo suave ao redor do player (luz se dispersando).
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    [Header("Cone (feixe)")]
    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 0.35f;
    [SerializeField] private float intensity = 1.45f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] [Range(10f, 120f)] private float outerSpotAngle = 70f;
    [SerializeField] [Range(5f, 90f)] private float innerSpotAngle = 28f;

    [Header("Halo suave (dispersão no player)")]
    [SerializeField] private float softOuterRadius = 2.4f;
    [SerializeField] private float softInnerRadius = 0.2f;
    [SerializeField] private float softIntensity = 0.55f;
    [SerializeField] [Range(0.1f, 1f)] private float softFalloff = 0.75f;

    [Header("Mão (local, pivot nos pés)")]
    [SerializeField] private Vector2 handOffsetLeft = new(-0.12f, 0.05f);
    [SerializeField] private Vector2 handOffsetRight = new(0.12f, 0.05f);
    [SerializeField] private Vector2 handOffsetDown = new(0.04f, 0.04f);
    [SerializeField] private Vector2 handOffsetUp = new(0f, 0.03f);

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

        Vector2 hand = HandOffset(facing);
        float z = facing == PlayerController.Facing.Up ? 0.15f : 0f;
        Vector3 origin = new(hand.x, hand.y, z);

        lightTransform.localRotation = Quaternion.Euler(0f, 0f, FacingToZRotation(facing));
        lightTransform.localPosition = origin;
        spotLight.lightOrder = facing == PlayerController.Facing.Up ? -1 : 0;

        // Halo no mesmo ponto de saída — ilumina o player e o chão ao redor com suavidade.
        if (softTransform != null)
        {
            softTransform.localPosition = origin;
            softTransform.localRotation = Quaternion.identity;
        }
    }

    private Vector2 HandOffset(PlayerController.Facing facing)
    {
        return facing switch
        {
            PlayerController.Facing.Left => handOffsetLeft,
            PlayerController.Facing.Right => handOffsetRight,
            PlayerController.Facing.Up => handOffsetUp,
            PlayerController.Facing.Down => handOffsetDown,
            _ => handOffsetDown
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
        // 360° — dispersão suave em volta do ponto de saída / player.
        light.lightType = Light2D.LightType.Point;
        light.color = lightColor;
        light.intensity = softIntensity;
        light.pointLightInnerRadius = softInnerRadius;
        light.pointLightOuterRadius = softOuterRadius;
        light.pointLightInnerAngle = 360f;
        light.pointLightOuterAngle = 360f;
        light.falloffIntensity = softFalloff;
        light.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        light.lightOrder = -2;
    }
}

/// <summary>
/// Ensures SpriteRenderers use URP 2D lit material so Point Light2D and Global Light affect characters/props.
/// </summary>
public static class CharacterLitMaterial
{
    public static void ApplyToHierarchy(Transform root)
    {
        SceneLitMaterial.ApplyToHierarchy(root);
    }
}
