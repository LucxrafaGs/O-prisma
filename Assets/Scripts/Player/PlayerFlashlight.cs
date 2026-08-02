using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lantern held in hotbar + E toggles a Point Light2D attached to the player.
/// Iluminação circular simples.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    public const string LanternItemId = "lanterna";

    [SerializeField] private float outerRadius = 6.5f;
    [SerializeField] private float innerRadius = 1.2f;
    [SerializeField] private float intensity = 1.35f;
    [SerializeField] private Color lightColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] private Vector2 lightOffset = new(0f, 0.55f);

    private Light2D pointLight;
    private bool isOn;

    public bool IsOn => isOn;

    private void Awake()
    {
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
        if (pointLight == null)
            return;

        pointLight.transform.localPosition = new Vector3(lightOffset.x, lightOffset.y, 0f);
        pointLight.transform.localRotation = Quaternion.identity;
    }

    public void SetEnabled(bool enabled)
    {
        isOn = enabled && IsHoldingLantern();
        if (pointLight != null)
            pointLight.enabled = isOn;
    }

    private static bool IsHoldingLantern()
    {
        PlayerHotbar.EnsureDefaults();
        PlayerHotbar.HeldItem current = PlayerHotbar.Current;
        return current != null && current.Id == LanternItemId;
    }

    private void EnsureLight()
    {
        // Remove halo extra de versões antigas.
        Transform soft = transform.Find("FlashlightSoft");
        if (soft != null)
            Destroy(soft.gameObject);

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

        pointLight = lightObject.GetComponent<Light2D>();
        if (pointLight == null)
            pointLight = lightObject.AddComponent<Light2D>();

        pointLight.lightType = Light2D.LightType.Point;
        pointLight.color = lightColor;
        pointLight.intensity = intensity;
        pointLight.pointLightInnerRadius = innerRadius;
        pointLight.pointLightOuterRadius = outerRadius;
        pointLight.pointLightInnerAngle = 360f;
        pointLight.pointLightOuterAngle = 360f;
        pointLight.falloffIntensity = 0.55f;
        pointLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        pointLight.shadowsEnabled = false;
        pointLight.shadowIntensity = 0f;
        pointLight.lightOrder = 0;
        lightObject.transform.localPosition = new Vector3(lightOffset.x, lightOffset.y, 0f);
        lightObject.transform.localRotation = Quaternion.identity;
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
