using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lanterna na hotbar + E: luz em cone triangular só na direção que o player olha.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 0.55f;
    [SerializeField] private float intensity = 1.45f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] private Vector2 holdOffset = new(0f, 0.35f);
    [SerializeField] [Range(10f, 120f)] private float outerSpotAngle = 70f;
    [SerializeField] [Range(5f, 90f)] private float innerSpotAngle = 28f;
    [SerializeField] private float forwardOffset = 0.25f;

    private Light2D spotLight;
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
        if (spotLight == null)
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

        Vector2 forward = FacingToDirection(facing);
        // Spot 2D aponta no eixo local +Y; Z rotation alinha esse eixo com a direção.
        float z = FacingToZRotation(facing);
        spotLight.transform.localRotation = Quaternion.Euler(0f, 0f, z);
        spotLight.transform.localPosition = new Vector3(
            holdOffset.x + forward.x * forwardOffset,
            holdOffset.y + forward.y * forwardOffset,
            0f);
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

    /// <summary>Rotação Z para o Spot apontar (local +Y) na direção do facing.</summary>
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

        spotLight = lightObject.GetComponent<Light2D>();
        if (spotLight == null)
            spotLight = lightObject.AddComponent<Light2D>();

        // Point/Spot com ângulo &lt; 360 = cone (triângulo de luz à frente).
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
