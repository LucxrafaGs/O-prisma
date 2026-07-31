using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives Global Light 2D from the game clock.
/// 17:00 golden hour, 18:00 dusk, 19:15 night; 06:00 dawn, 07:00 day.
/// Works with <see cref="DappledSunLighting"/> for cloud/sun patches.
/// </summary>
[DefaultExecutionOrder(-20)]
public class DayNightLighting : MonoBehaviour
{
    [SerializeField] private Light2D globalLight;
    [SerializeField] private Color dayColor = new(1f, 0.98f, 0.9f, 1f);
    [SerializeField] private Color goldenColor = new(1f, 0.62f, 0.32f, 1f);
    [SerializeField] private Color duskColor = new(0.78f, 0.38f, 0.28f, 1f);
    [SerializeField] private Color nightColor = new(0.18f, 0.26f, 0.58f, 1f);
    [SerializeField] private float dayIntensity = 1f;
    [SerializeField] private float goldenIntensity = 0.92f;
    [SerializeField] private float nightIntensity = 0.2f;

    /// <summary>Multiplicador de intensidade (chuva escurece o ambiente).</summary>
    public static float WeatherAmbientMultiplier { get; set; } = 1f;

    /// <summary>Tint aplicado por clima (chuva = azulado).</summary>
    public static Color WeatherAmbientTint { get; set; } = Color.white;

    /// <summary>Clarões de trovão somados à intensidade global.</summary>
    public static float ThunderBoost { get; set; }

    /// <summary>
    /// Piso de ambiente para contraste das manchas de sol (nuvens).
    /// Controlado por <see cref="DappledSunLighting"/>.
    /// </summary>
    public static float DappledAmbientFloor { get; set; } = 1f;

    public static void ClearWeatherForDomainReload()
    {
        WeatherAmbientMultiplier = 1f;
        WeatherAmbientTint = Color.white;
        ThunderBoost = 0f;
        DappledAmbientFloor = 1f;
    }

    private void Awake()
    {
        if (globalLight == null)
            globalLight = FindGlobalLight();
    }

    private void OnEnable()
    {
        GameTimeClock.OnTimeChanged += ApplyLighting;
        GameTimeClock.OnDayStarted += ApplyLighting;
    }

    private void OnDisable()
    {
        GameTimeClock.OnTimeChanged -= ApplyLighting;
        GameTimeClock.OnDayStarted -= ApplyLighting;
    }

    private void Start()
    {
        ApplyLighting();
    }

    private void LateUpdate()
    {
        if (GameTimeClock.Instance != null)
            ApplyLighting();
    }

    private void ApplyLighting()
    {
        if (globalLight == null)
            globalLight = FindGlobalLight();

        if (globalLight == null || GameTimeClock.Instance == null)
            return;

        float minutes = GameTimeClock.Instance.MinutesSinceMidnight;
        EvaluateCycle(minutes, out float intensity, out Color color);
        intensity = intensity
            * Mathf.Max(0.05f, WeatherAmbientMultiplier)
            * Mathf.Clamp(DappledAmbientFloor, 0.2f, 1.2f)
            + Mathf.Max(0f, ThunderBoost);
        color *= WeatherAmbientTint;
        globalLight.intensity = intensity;
        globalLight.color = color;
    }

    private static Light2D FindGlobalLight()
    {
        Light2D[] lights = FindObjectsByType<Light2D>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
                return lights[i];
        }

        return lights.Length > 0 ? lights[0] : null;
    }

    private void EvaluateCycle(float minutes, out float intensity, out Color color)
    {
        const float dawnStart = 6f * 60f;
        const float dayStart = 7f * 60f;
        const float goldenStart = 17f * 60f; // 17:00 — sol começando a se pôr
        const float duskStart = 18f * 60f;
        const float nightStart = 19.25f * 60f;

        if (minutes >= dayStart && minutes < goldenStart)
        {
            intensity = dayIntensity;
            color = dayColor;
            return;
        }

        if (minutes >= goldenStart && minutes < duskStart)
        {
            float t = Mathf.InverseLerp(goldenStart, duskStart, minutes);
            intensity = Mathf.Lerp(dayIntensity, goldenIntensity, t);
            color = Color.Lerp(dayColor, goldenColor, t);
            return;
        }

        if (minutes >= duskStart && minutes < nightStart)
        {
            float t = Mathf.InverseLerp(duskStart, nightStart, minutes);
            intensity = Mathf.Lerp(goldenIntensity, nightIntensity, t);
            color = Color.Lerp(
                Color.Lerp(goldenColor, duskColor, Mathf.Clamp01(t * 1.6f)),
                nightColor,
                Mathf.Clamp01(t * 1.4f - 0.2f));
            return;
        }

        if (minutes >= dawnStart && minutes < dayStart)
        {
            float t = Mathf.InverseLerp(dawnStart, dayStart, minutes);
            intensity = Mathf.Lerp(nightIntensity, dayIntensity, t);
            color = Color.Lerp(nightColor, dayColor, t);
            return;
        }

        intensity = nightIntensity;
        color = nightColor;
    }
}
