using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lanterna na hotbar + E: cone na direção do looking.
/// Origem na mão (offsets locais; player escala ~2.8).
/// Ao andar para cima (costas), origem fica baixa sob o torso.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 0.35f;
    [SerializeField] private float intensity = 1.45f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] [Range(10f, 120f)] private float outerSpotAngle = 70f;
    [SerializeField] [Range(5f, 90f)] private float innerSpotAngle = 28f;

    // Pivot nos pés; Y ~0.12 ≈ mão/cintura com escala 2.8 do personagem.
    [Header("Mão (local, pivot nos pés)")]
    [SerializeField] private Vector2 handOffsetLeft = new(-0.22f, 0.12f);
    [SerializeField] private Vector2 handOffsetRight = new(0.22f, 0.12f);
    [SerializeField] private Vector2 handOffsetDown = new(0.08f, 0.1f);
    [Tooltip("Costas: origem baixa sob o corpo para o feixe sair por baixo do sprite.")]
    [SerializeField] private Vector2 handOffsetUp = new(0f, 0.06f);

    private Light2D spotLight;
    private Transform lightTransform;
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
    }

    private void AimLightAtFacing()
    {
        PlayerController.Facing facing = player != null
            ? player.CurrentFacing
            : PlayerController.Facing.Down;

        Vector2 hand = HandOffset(facing);
        lightTransform.localRotation = Quaternion.Euler(0f, 0f, FacingToZRotation(facing));

        // Costas: Z positivo coloca a origem da luz “atrás” do sprite no depth 2D,
        // para o ponto de saída ficar visualmente sob o player.
        float z = facing == PlayerController.Facing.Up ? 0.15f : 0f;
        lightTransform.localPosition = new Vector3(hand.x, hand.y, z);

        spotLight.lightOrder = facing == PlayerController.Facing.Up ? -1 : 0;
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
        Transform existing = transform.Find("Flashlight");
        GameObject lightObject;
        if (existing == null)
        {
            lightObject = new GameObject("Flashlight");
            lightObject.transform.SetParent(transform, false);
        }
        else
        {
            lightObject = existing.gameObject;
        }

        lightTransform = lightObject.transform;
        spotLight = lightObject.GetComponent<Light2D>();
        if (spotLight == null)
            spotLight = lightObject.AddComponent<Light2D>();

        spotLight.lightType = Light2D.LightType.Point;
        spotLight.color = lightColor;
        spotLight.intensity = intensity;
        spotLight.pointLightInnerRadius = innerRadius;
        spotLight.pointLightOuterRadius = outerRadius;
        spotLight.pointLightInnerAngle = Mathf.Clamp(innerSpotAngle, 5f, outerSpotAngle);
        spotLight.pointLightOuterAngle = Mathf.Clamp(outerSpotAngle, 10f, 120f);
        spotLight.falloffIntensity = 0.45f;
        spotLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;

        AimLightAtFacing();
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
