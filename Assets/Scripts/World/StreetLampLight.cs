using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Luz de poste antigo: liga 18:00, desliga 06:30.
/// Glitches raros e independentes por poste (não sincronizam).
/// </summary>
[DisallowMultipleComponent]
public class StreetLampLight : MonoBehaviour
{
    public const string LightChildName = "StreetLampLight2D";

    private const int OnMinutes = 18 * 60;       // 18:00
    private const int OffMinutes = 6 * 60 + 30;  // 06:30

    [Header("Luz")]
    [SerializeField] private Color lampColor = new(1f, 0.72f, 0.28f, 1f);
    [SerializeField] private float intensity = 1.2f;
    [SerializeField] private float innerRadius = 0.35f;
    [SerializeField] private float outerRadius = 4.8f;
    [SerializeField] private float falloff = 0.5f;
    [Tooltip("Empurra a luz um pouco abaixo do topo do sprite.")]
    [SerializeField] private float topInset = 0.08f;

    [Header("Glitch")]
    [SerializeField] private float glitchMinGapSeconds = 55f;
    [SerializeField] private float glitchMaxGapSeconds = 420f;
    [SerializeField] private float glitchExtraGapChance = 0.42f;
    [SerializeField] private float glitchSkipChance = 0.5f;

    private Light2D pointLight;
    private SpriteRenderer lampRenderer;
    private bool scheduleOn;
    private float displayIntensity = 1f;

    private float nextGlitchRollTime;
    private int glitchBurstsLeft;
    private float glitchPhaseEndTime;
    private bool inGlitchPhase;
    private bool glitchDimPhase;

    private void Awake()
    {
        lampRenderer = GetComponent<SpriteRenderer>();
        EnsureLight();
        // DepthSplit cuida do material clipado; senão usa lit padrão.
        if (GetComponent<StreetLampDepthSplit>() == null)
            SceneLitMaterial.ApplyTo(lampRenderer);
        RollNextGlitchTime(initial: true);
        RefreshSchedule(force: true);
    }

    private void OnEnable()
    {
        GameTimeClock.OnTimeChanged += OnClockChanged;
        GameTimeClock.OnDayStarted += OnDayStarted;
    }

    private void OnDisable()
    {
        GameTimeClock.OnTimeChanged -= OnClockChanged;
        GameTimeClock.OnDayStarted -= OnDayStarted;
    }

    private void Start()
    {
        RefreshSchedule(force: true);
    }

    private void Update()
    {
        RefreshSchedule(force: false);
        UpdateGlitch();
        ApplyIntensity();
    }

    private void LateUpdate()
    {
        PlaceLightAtTop();
    }

    private void OnClockChanged() => RefreshSchedule(force: true);

    private void OnDayStarted()
    {
        RollNextGlitchTime(initial: true);
        RefreshSchedule(force: true);
    }

    private static bool ShouldBeOn(int minutesSinceMidnight)
    {
        // Ligado de 18:00 até 06:30 (atravessa a meia-noite).
        return minutesSinceMidnight >= OnMinutes || minutesSinceMidnight < OffMinutes;
    }

    private void RefreshSchedule(bool force)
    {
        GameTimeClock clock = GameTimeClock.Instance;
        bool wantOn = clock != null && ShouldBeOn(clock.MinutesSinceMidnight);

        if (!force && wantOn == scheduleOn)
            return;

        scheduleOn = wantOn;
        if (pointLight != null)
            pointLight.enabled = scheduleOn;

        if (!scheduleOn)
        {
            inGlitchPhase = false;
            glitchBurstsLeft = 0;
            displayIntensity = intensity;
        }
    }

    private void UpdateGlitch()
    {
        if (!scheduleOn || pointLight == null)
            return;

        if (inGlitchPhase)
        {
            if (Time.time < glitchPhaseEndTime)
                return;

            if (glitchDimPhase)
            {
                // Volta a brilhar um instante (ou encerra o burst).
                glitchDimPhase = false;
                displayIntensity = intensity;
                glitchBurstsLeft--;
                if (glitchBurstsLeft <= 0)
                {
                    inGlitchPhase = false;
                    RollNextGlitchTime(initial: false);
                }
                else
                {
                    // Pausa curta acesa antes do próximo apagão.
                    glitchPhaseEndTime = Time.time + Random.Range(0.04f, 0.16f);
                }
            }
            else
            {
                // Começa apagão/falha.
                glitchDimPhase = true;
                displayIntensity = Random.value < 0.35f
                    ? 0f
                    : intensity * Random.Range(0.04f, 0.28f);
                glitchPhaseEndTime = Time.time + Random.Range(0.05f, 0.22f);
            }

            return;
        }

        if (Time.time < nextGlitchRollTime)
            return;

        // Timer estourou: muitas vezes não acontece nada.
        if (Random.value < glitchSkipChance)
        {
            RollNextGlitchTime(initial: false);
            return;
        }

        BeginGlitchBurst();
    }

    private void BeginGlitchBurst()
    {
        inGlitchPhase = true;
        glitchDimPhase = false;
        glitchBurstsLeft = Random.Range(1, 4); // 1–3 falhas
        displayIntensity = intensity;
        // Primeiro intervalo aceso curto, depois apaga.
        glitchPhaseEndTime = Time.time + Random.Range(0.02f, 0.1f);
    }

    private void RollNextGlitchTime(bool initial)
    {
        float gap = Random.Range(glitchMinGapSeconds, glitchMaxGapSeconds);
        if (Random.value < glitchExtraGapChance)
            gap += Random.Range(glitchMinGapSeconds * 1.5f, glitchMaxGapSeconds * 1.8f);

        // Cada poste começa em fase diferente.
        if (initial)
            gap *= Random.Range(0.15f, 1.35f);

        nextGlitchRollTime = Time.time + gap;
    }

    private void ApplyIntensity()
    {
        if (pointLight == null || !scheduleOn)
            return;

        pointLight.intensity = inGlitchPhase ? displayIntensity : intensity;
        pointLight.color = lampColor;
    }

    private void EnsureLight()
    {
        Transform existing = transform.Find(LightChildName);
        GameObject lightObject;
        if (existing == null)
        {
            lightObject = new GameObject(LightChildName);
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
        pointLight.color = lampColor;
        pointLight.intensity = intensity;
        pointLight.pointLightInnerRadius = innerRadius;
        pointLight.pointLightOuterRadius = outerRadius;
        pointLight.pointLightInnerAngle = 360f;
        pointLight.pointLightOuterAngle = 360f;
        pointLight.falloffIntensity = falloff;
        pointLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        pointLight.enabled = false;

        PlaceLightAtTop();
    }

    private void PlaceLightAtTop()
    {
        if (pointLight == null)
            return;

        Vector3 local = Vector3.zero;
        if (lampRenderer != null && lampRenderer.sprite != null)
        {
            Bounds b = lampRenderer.sprite.bounds;
            local = new Vector3(b.center.x, b.max.y - topInset, 0f);
        }
        else
        {
            local = new Vector3(0f, 0.85f, 0f);
        }

        pointLight.transform.localPosition = local;
    }
}
