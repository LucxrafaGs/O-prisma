using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chuva top-down: riscos diagonais suaves em toda a visão + respingos no chão.
/// </summary>
[DefaultExecutionOrder(-15)]
public class RainWeatherSystem : MonoBehaviour
{
    public static RainWeatherSystem Instance { get; private set; }

    // Acima de copas (order+5000) e de qualquer SortingGroup do mundo.
    private const int RainSortBase = 32000;

    [Header("Chuva")]
    [SerializeField] private float rainRate = 200f;
    // Queda quase vertical, leve inclinação → inferior-direito.
    [SerializeField] private Vector2 rainWind = new(1.05f, -4.2f);
    [SerializeField] private Color rainColorNear = new(0.9f, 0.95f, 1f, 0.56f);
    [SerializeField] private Color rainColorFar = new(0.58f, 0.66f, 0.74f, 0.4f);

    [Header("Respingo")]
    [SerializeField] private float splashRate = 160f;
    [SerializeField] private float splashPlayerClearRadius = 0.55f;

    [Header("Névoa")]
    [SerializeField] private float mistRate = 22f;
    [SerializeField] private float cloudRate = 6f;
    [SerializeField] private float fogAlpha = 0.32f;

    [Header("Trovão")]
    [SerializeField] private Vector2 thunderInterval = new(8f, 20f);
    [SerializeField] private float thunderFlashPeak = 1.2f;
    [SerializeField] private float openingThunderChance = 0.55f;
    [SerializeField] private float rainFadeSeconds = 9f;
    [SerializeField] private float fogFadeSeconds = 11f;

    [Header("Ambiente")]
    [SerializeField] private float rainAmbientMultiplier = 0.72f;
    [SerializeField] private Color rainAmbientTint = new(0.75f, 0.82f, 0.95f, 1f);

    private bool isRaining;
    private bool isFoggy;
    private bool fogLinkedToRain;
    private float rainIntensity;
    private float rainIntensityTarget;
    private float fogIntensity;
    private float fogIntensityTarget;
    private Transform systemsRoot;
    private ParticleSystem rainParticles;
    private ParticleSystem splashParticles;
    private ParticleSystem mistParticles;
    private ParticleSystem cloudParticles;
    private SpriteRenderer fogRenderer;
    private SpriteRenderer fogDriftRenderer;
    private SpriteRenderer flashRenderer;
    private readonly List<Material> ownedMaterials = new();
    private ParticleSystem.Particle[] splashBuffer;
    private Transform playerTransform;
    private Coroutine thunderRoutine;
    private Coroutine dryThunderRoutine;
    private Coroutine rainIntroRoutine;
    private Coroutine fogIntroRoutine;
    private float fogPulse;

    public bool IsRaining => isRaining || rainIntensityTarget > 0.01f || rainIntensity > 0.01f;
    public bool IsFoggy => isFoggy || fogIntensityTarget > 0.01f || fogIntensity > 0.01f;

    public static void ClearInstanceForDomainReload() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        RainPixelTextures.ClearForDomainReload();
        BuildSystems();
        StopRainImmediate();
        StopFogImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        for (int i = 0; i < ownedMaterials.Count; i++)
        {
            if (ownedMaterials[i] != null)
                Destroy(ownedMaterials[i]);
        }

        ownedMaterials.Clear();
    }

    private void LateUpdate()
    {
        FollowCamera();
        ClearSplashesNearPlayer();
        UpdateWeatherIntensity(Time.unscaledDeltaTime);
        UpdateFogVisuals();
    }

    private void UpdateWeatherIntensity(float dt)
    {
        float rainSpeed = rainFadeSeconds > 0.01f ? 1f / rainFadeSeconds : 1f;
        float fogSpeed = fogFadeSeconds > 0.01f ? 1f / fogFadeSeconds : 1f;
        rainIntensity = Mathf.MoveTowards(rainIntensity, rainIntensityTarget, rainSpeed * dt);
        fogIntensity = Mathf.MoveTowards(fogIntensity, fogIntensityTarget, fogSpeed * dt);

        ApplyRainIntensity();
        ApplyFogIntensity();

        if (rainIntensityTarget <= 0f && rainIntensity <= 0.001f && isRaining)
            FinishRainOff();

        if (fogIntensityTarget <= 0f && fogIntensity <= 0.001f && isFoggy)
            FinishFogOff();
    }

    private void UpdateFogVisuals()
    {
        if (fogIntensity <= 0.001f || fogRenderer == null)
            return;

        fogPulse += Time.unscaledDeltaTime * 0.28f;
        float pulse = 0.88f + Mathf.Sin(fogPulse) * 0.07f + Mathf.Sin(fogPulse * 0.41f) * 0.05f;
        Color c = fogRenderer.color;
        c.a = fogAlpha * fogIntensity * pulse;
        fogRenderer.color = c;

        Vector3 fogPos = fogRenderer.transform.localPosition;
        fogPos.x = Mathf.Sin(fogPulse * 0.18f) * 0.55f;
        fogPos.y = Mathf.Cos(fogPulse * 0.11f) * 0.2f;
        fogRenderer.transform.localPosition = fogPos;

        if (fogDriftRenderer != null)
        {
            Color d = fogDriftRenderer.color;
            d.a = fogAlpha * 0.7f * fogIntensity * (0.75f + Mathf.Sin(fogPulse * 0.55f + 1.2f) * 0.25f);
            fogDriftRenderer.color = d;
            Vector3 drift = fogDriftRenderer.transform.localPosition;
            drift.x = Mathf.Repeat(fogPulse * 0.22f, 2.4f) - 1.2f;
            drift.y = Mathf.Sin(fogPulse * 0.15f + 0.8f) * 0.35f;
            fogDriftRenderer.transform.localPosition = drift;
        }
    }

    /// <summary>Dev Mode: liga/desliga chuva com fade; névoa ~80%.</summary>
    public void ToggleRain()
    {
        if (IsRaining)
        {
            StopRainSmooth(clearLinkedFog: true);
            return;
        }

        BeginRainWithFogChance(0.8f, allowOpeningThunder: true);
    }

    public void SetRaining(bool raining)
    {
        if (raining)
            BeginRainImmediate(withFog: false);
        else
            StopRainImmediate();
    }

    public void SetFoggy(bool foggy)
    {
        if (foggy)
            BeginFogSmooth();
        else
            StopFogSmooth();
    }

    /// <summary>Clima automático / Dev: chuva suave + chance de névoa + possível trovão de abertura.</summary>
    public void BeginRainWithFogChance(float fogChance = 0.8f, bool allowOpeningThunder = true)
    {
        if (rainIntroRoutine != null)
            StopCoroutine(rainIntroRoutine);
        rainIntroRoutine = StartCoroutine(BeginRainRoutine(fogChance, allowOpeningThunder));
    }

    public void EndRainKeepAmbientFog()
    {
        StopRainSmooth(clearLinkedFog: true);
    }

    public void BeginFogSmooth()
    {
        if (fogIntroRoutine != null)
            StopCoroutine(fogIntroRoutine);

        isFoggy = true;
        fogIntensityTarget = 1f;
        if (fogIntensity < 0.02f)
            fogIntensity = 0.02f;

        SetEmitterActive(mistParticles, true);
        SetEmitterActive(cloudParticles, true);
        if (fogRenderer != null)
            fogRenderer.enabled = true;
        if (fogDriftRenderer != null)
            fogDriftRenderer.enabled = true;
        ApplyFogIntensity();
    }

    public void StopFogSmooth()
    {
        fogLinkedToRain = false;
        fogIntensityTarget = 0f;
    }

    /// <summary>Restore de save: intensidade cheia na hora.</summary>
    public void SetWeatherImmediate(bool raining, bool foggy)
    {
        if (rainIntroRoutine != null)
        {
            StopCoroutine(rainIntroRoutine);
            rainIntroRoutine = null;
        }

        if (fogIntroRoutine != null)
        {
            StopCoroutine(fogIntroRoutine);
            fogIntroRoutine = null;
        }

        if (raining)
            BeginRainImmediate(withFog: foggy);
        else
        {
            StopRainImmediate();
            if (foggy)
            {
                isFoggy = true;
                fogLinkedToRain = false;
                fogIntensity = 1f;
                fogIntensityTarget = 1f;
                SetEmitterActive(mistParticles, true);
                SetEmitterActive(cloudParticles, true);
                if (fogRenderer != null)
                    fogRenderer.enabled = true;
                if (fogDriftRenderer != null)
                    fogDriftRenderer.enabled = true;
                ApplyFogIntensity();
            }
            else
            {
                StopFogImmediate();
            }
        }
    }

    public void PlayIsolatedThunder()
    {
        if (!isActiveAndEnabled)
            return;
        StartCoroutine(PlayThunderFlash(strong: true));
    }

    public void StartDryNightThunderLoop()
    {
        if (dryThunderRoutine != null)
            return;
        dryThunderRoutine = StartCoroutine(DryThunderLoop());
    }

    public void StopDryNightThunderLoop()
    {
        if (dryThunderRoutine == null)
            return;
        StopCoroutine(dryThunderRoutine);
        dryThunderRoutine = null;
    }

    private IEnumerator BeginRainRoutine(float fogChance, bool allowOpeningThunder)
    {
        if (allowOpeningThunder && Random.value <= openingThunderChance)
        {
            yield return PlayThunderFlash(strong: true);
            yield return new WaitForSecondsRealtime(Random.Range(0.35f, 0.9f));
        }

        isRaining = true;
        rainIntensityTarget = 1f;
        if (rainIntensity < 0.03f)
            rainIntensity = 0.03f;

        DayNightLighting.WeatherAmbientMultiplier = Mathf.Lerp(1f, rainAmbientMultiplier, rainIntensity);
        DayNightLighting.WeatherAmbientTint = Color.Lerp(Color.white, rainAmbientTint, rainIntensity);

        SetEmitterActive(rainParticles, true);
        SetEmitterActive(splashParticles, true);
        ApplyRainIntensity();

        if (thunderRoutine != null)
            StopCoroutine(thunderRoutine);
        thunderRoutine = StartCoroutine(ThunderLoop());

        if (Random.value <= fogChance)
        {
            fogLinkedToRain = true;
            BeginFogSmooth();
        }

        rainIntroRoutine = null;
    }

    private void BeginRainImmediate(bool withFog)
    {
        isRaining = true;
        rainIntensity = 1f;
        rainIntensityTarget = 1f;
        DayNightLighting.WeatherAmbientMultiplier = rainAmbientMultiplier;
        DayNightLighting.WeatherAmbientTint = rainAmbientTint;
        SetEmitterActive(rainParticles, true);
        SetEmitterActive(splashParticles, true);
        ApplyRainIntensity();

        if (thunderRoutine != null)
            StopCoroutine(thunderRoutine);
        thunderRoutine = StartCoroutine(ThunderLoop());

        if (withFog)
        {
            fogLinkedToRain = true;
            isFoggy = true;
            fogIntensity = 1f;
            fogIntensityTarget = 1f;
            SetEmitterActive(mistParticles, true);
            SetEmitterActive(cloudParticles, true);
            if (fogRenderer != null)
                fogRenderer.enabled = true;
            if (fogDriftRenderer != null)
                fogDriftRenderer.enabled = true;
            ApplyFogIntensity();
        }
    }

    private void StopRainSmooth(bool clearLinkedFog)
    {
        if (rainIntroRoutine != null)
        {
            StopCoroutine(rainIntroRoutine);
            rainIntroRoutine = null;
        }

        rainIntensityTarget = 0f;
        if (clearLinkedFog && fogLinkedToRain)
            StopFogSmooth();
    }

    private void StopRainImmediate()
    {
        if (rainIntroRoutine != null)
        {
            StopCoroutine(rainIntroRoutine);
            rainIntroRoutine = null;
        }

        rainIntensity = 0f;
        rainIntensityTarget = 0f;
        FinishRainOff();
    }

    private void FinishRainOff()
    {
        isRaining = false;
        DayNightLighting.WeatherAmbientMultiplier = 1f;
        DayNightLighting.WeatherAmbientTint = Color.white;
        DayNightLighting.ThunderBoost = 0f;
        SetEmitterActive(rainParticles, false);
        SetEmitterActive(splashParticles, false);

        if (thunderRoutine != null)
        {
            StopCoroutine(thunderRoutine);
            thunderRoutine = null;
        }

        if (flashRenderer != null && dryThunderRoutine == null)
            flashRenderer.color = new Color(1f, 1f, 1f, 0f);
    }

    private void StopFogImmediate()
    {
        fogLinkedToRain = false;
        fogIntensity = 0f;
        fogIntensityTarget = 0f;
        FinishFogOff();
    }

    private void FinishFogOff()
    {
        isFoggy = false;
        fogLinkedToRain = false;
        SetEmitterActive(mistParticles, false);
        SetEmitterActive(cloudParticles, false);
        if (fogRenderer != null)
        {
            fogRenderer.enabled = false;
            Color c = fogRenderer.color;
            c.a = 0f;
            fogRenderer.color = c;
        }

        if (fogDriftRenderer != null)
        {
            fogDriftRenderer.enabled = false;
            Color c = fogDriftRenderer.color;
            c.a = 0f;
            fogDriftRenderer.color = c;
        }
    }

    private void ApplyRainIntensity()
    {
        SetEmissionRate(rainParticles, rainRate * rainIntensity);
        SetEmissionRate(splashParticles, splashRate * rainIntensity);

        float ambientMul = Mathf.Lerp(1f, rainAmbientMultiplier, rainIntensity);
        Color ambientTint = Color.Lerp(Color.white, rainAmbientTint, rainIntensity);
        if (isRaining || rainIntensity > 0.01f)
        {
            DayNightLighting.WeatherAmbientMultiplier = ambientMul;
            DayNightLighting.WeatherAmbientTint = ambientTint;
        }
    }

    private void ApplyFogIntensity()
    {
        SetEmissionRate(mistParticles, mistRate * fogIntensity);
        SetEmissionRate(cloudParticles, cloudRate * fogIntensity);
    }

    private static void SetEmissionRate(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.rateOverTime = rate;
    }

    private static void SetEmitterActive(ParticleSystem ps, bool active)
    {
        if (ps == null)
            return;

        var emission = ps.emission;
        emission.enabled = active;
        if (active)
            ps.Play(true);
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void FollowCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || systemsRoot == null)
            return;

        float height = cam.orthographic ? cam.orthographicSize * 2f : 10f;
        float width = height * cam.aspect;

        systemsRoot.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        systemsRoot.rotation = Quaternion.identity;

        // Top-down: emissão cobre toda a visão (não só topo/base).
        ConfigureEmitterSizes(width * 1.15f, height * 1.15f);
        if (fogRenderer != null)
            fogRenderer.transform.localScale = new Vector3(width * 1.35f, height * 1.35f, 1f);
        if (fogDriftRenderer != null)
            fogDriftRenderer.transform.localScale = new Vector3(width * 1.6f, height * 0.95f, 1f);
        if (flashRenderer != null)
            flashRenderer.transform.localScale = new Vector3(width * 1.3f, height * 1.3f, 1f);
    }

    private void ConfigureEmitterSizes(float width, float height)
    {
        if (rainParticles != null)
        {
            var shape = rainParticles.shape;
            shape.scale = new Vector3(width, height, 1f);
            rainParticles.transform.localPosition = Vector3.zero;
        }

        if (splashParticles != null)
        {
            var shape = splashParticles.shape;
            shape.scale = new Vector3(width, height, 1f);
            splashParticles.transform.localPosition = Vector3.zero;
        }

        if (mistParticles != null)
        {
            var shape = mistParticles.shape;
            shape.scale = new Vector3(width * 1.05f, height * 1.05f, 1f);
            mistParticles.transform.localPosition = Vector3.zero;
        }

        if (cloudParticles != null)
        {
            var shape = cloudParticles.shape;
            shape.scale = new Vector3(width * 1.2f, height * 0.85f, 1f);
            cloudParticles.transform.localPosition = Vector3.zero;
        }
    }

    private void ClearSplashesNearPlayer()
    {
        if (!isRaining || splashParticles == null)
            return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                PlayerController controller = FindAnyObjectByType<PlayerController>();
                if (controller != null)
                    playerTransform = controller.transform;
            }
            else
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform == null)
            return;

        int count = splashParticles.particleCount;
        if (count <= 0)
            return;

        if (splashBuffer == null || splashBuffer.Length < count)
            splashBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];

        count = splashParticles.GetParticles(splashBuffer);
        Vector2 playerPos = playerTransform.position;
        float radiusSq = splashPlayerClearRadius * splashPlayerClearRadius;
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            Vector2 p = splashBuffer[i].position;
            if ((p - playerPos).sqrMagnitude <= radiusSq)
            {
                splashBuffer[i].remainingLifetime = -1f;
                changed = true;
            }
        }

        if (changed)
            splashParticles.SetParticles(splashBuffer, count);
    }

    private void BuildSystems()
    {
        systemsRoot = new GameObject("RainSystems").transform;
        systemsRoot.SetParent(transform, false);

        rainParticles = CreateRainParticles(systemsRoot);
        splashParticles = CreateSplashParticles(systemsRoot);
        mistParticles = CreateMistParticles(systemsRoot);
        cloudParticles = CreateCloudParticles(systemsRoot);
        fogRenderer = CreateOverlay(
            systemsRoot,
            "RainFog",
            RainPixelTextures.Fog,
            new Color(0.55f, 0.58f, 0.62f, 0f),
            RainSortBase + 5);
        fogDriftRenderer = CreateOverlay(
            systemsRoot,
            "RainFogDrift",
            RainPixelTextures.Cloud,
            new Color(0.5f, 0.54f, 0.58f, 0f),
            RainSortBase + 8);
        flashRenderer = CreateOverlay(systemsRoot, "ThunderFlash", RainPixelTextures.Mist, new Color(1f, 1f, 1f, 0f), RainSortBase + 40);
    }

    private static void ForceOnTop(ParticleSystemRenderer renderer, int sortingOrder)
    {
        renderer.sortingLayerID = SortingLayer.NameToID("Default");
        renderer.sortingOrder = sortingOrder;
        renderer.sortingFudge = -1000f;
    }

    private Material CreateParticleMaterial(Texture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new(shader)
        {
            name = "RainParticleMat",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            material.mainTexture = texture;
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHABLEND_ON");

        ownedMaterials.Add(material);
        return material;
    }

    /// <summary>
    /// Unity exige o mesmo modo em X/Y/Z das velocity curves.
    /// </summary>
    private static void SetVelocityTwoConstants(
        ParticleSystem.VelocityOverLifetimeModule velocity,
        Vector2 min,
        Vector2 max)
    {
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(min.x, max.x);
        velocity.y = new ParticleSystem.MinMaxCurve(min.y, max.y);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private ParticleSystem CreateRainParticles(Transform parent)
    {
        GameObject go = new("RainDrops");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.1f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.03f, 0.048f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.13f, 0.24f);
        main.startSizeZ = 1f;
        // Parte clara + parte azul-acinzentada (profundidade).
        main.startColor = new ParticleSystem.MinMaxGradient(rainColorNear, rainColorFar);
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        // Menos inclinado — quase para cima, leve tilt.
        main.startRotation = new ParticleSystem.MinMaxCurve(
            Mathf.Deg2Rad * -20f,
            Mathf.Deg2Rad * -12f);

        var emission = ps.emission;
        emission.rateOverTime = rainRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 12f, 1f);
        shape.randomDirectionAmount = 0f;

        SetVelocityTwoConstants(
            ps.velocityOverLifetime,
            new Vector2(rainWind.x * 0.9f, rainWind.y * 0.9f),
            new Vector2(rainWind.x * 1.15f, rainWind.y * 1.15f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial(RainPixelTextures.RainStreak.texture);
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        ForceOnTop(renderer, RainSortBase + 20);

        return ps;
    }

    private ParticleSystem CreateSplashParticles(Transform parent)
    {
        GameObject go = new("RainSplashes");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.38f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
        main.startColor = new Color(0.9f, 0.95f, 1f, 0.92f);
        main.maxParticles = 1100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = splashRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 12f, 1f);

        // Micro-abertura vertical do respingo — mesmo modo em todos os eixos.
        SetVelocityTwoConstants(
            ps.velocityOverLifetime,
            new Vector2(-0.05f, 0.02f),
            new Vector2(0.05f, 0.12f));

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.45f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.8f, 0.88f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.55f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial(RainPixelTextures.Splash.texture);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ForceOnTop(renderer, RainSortBase + 10);

        return ps;
    }

    private ParticleSystem CreateMistParticles(Transform parent)
    {
        GameObject go = new("RainMist");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 6.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.58f, 0.62f, 0.22f),
            new Color(0.48f, 0.52f, 0.58f, 0.34f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = mistRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 10f, 1f);

        SetVelocityTwoConstants(
            ps.velocityOverLifetime,
            new Vector2(-0.55f, -0.12f),
            new Vector2(-0.12f, 0.1f));

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.35f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial(RainPixelTextures.Mist.texture);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ForceOnTop(renderer, RainSortBase + 2);

        return ps;
    }

    private ParticleSystem CreateCloudParticles(Transform parent)
    {
        GameObject go = new("RainClouds");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(5.5f, 9.5f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
        main.startSizeZ = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.53f, 0.57f, 0.28f),
            new Color(0.42f, 0.45f, 0.5f, 0.4f));
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = cloudRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 10f, 1f);

        // Bancos de nuvem atravessando a tela (esquerda → direita / leve drift).
        SetVelocityTwoConstants(
            ps.velocityOverLifetime,
            new Vector2(0.35f, -0.08f),
            new Vector2(0.85f, 0.08f));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial(RainPixelTextures.Cloud.texture);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ForceOnTop(renderer, RainSortBase + 6);

        return ps;
    }

    private SpriteRenderer CreateOverlay(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        int sortingOrder)
    {
        GameObject go = new(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = SortingLayer.NameToID("Default");
        renderer.sortingOrder = sortingOrder;

        Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlit == null)
            unlit = Shader.Find("Sprites/Default");
        if (unlit != null)
        {
            Material mat = new(unlit) { hideFlags = HideFlags.HideAndDontSave };
            ownedMaterials.Add(mat);
            renderer.sharedMaterial = mat;
        }

        return renderer;
    }

    private IEnumerator ThunderLoop()
    {
        while (isRaining || rainIntensityTarget > 0.01f)
        {
            float wait = Random.Range(thunderInterval.x, thunderInterval.y);
            yield return new WaitForSecondsRealtime(wait);
            if (!isRaining && rainIntensityTarget <= 0.01f)
                yield break;

            yield return StartCoroutine(PlayThunderFlash(strong: false));
        }
    }

    private IEnumerator DryThunderLoop()
    {
        while (true)
        {
            float wait = Random.Range(thunderInterval.x * 1.4f, thunderInterval.y * 2.2f);
            yield return new WaitForSecondsRealtime(wait);
            // Só trovões secos se não estiver chovendo.
            if (isRaining || rainIntensity > 0.05f)
                continue;

            yield return StartCoroutine(PlayThunderFlash(strong: Random.value < 0.35f));
        }
    }

    private IEnumerator PlayThunderFlash(bool strong = false)
    {
        int bursts = strong ? Random.Range(2, 5) : Random.Range(1, 4);
        float peakScale = strong ? 1.35f : 1f;
        for (int i = 0; i < bursts; i++)
        {
            float peak = thunderFlashPeak * peakScale * Random.Range(0.8f, 1.1f);
            DayNightLighting.ThunderBoost = peak;
            if (flashRenderer != null)
            {
                flashRenderer.enabled = true;
                flashRenderer.color = new Color(0.92f, 0.95f, 1f, Random.Range(strong ? 0.28f : 0.18f, strong ? 0.55f : 0.38f));
            }

            yield return new WaitForSecondsRealtime(Random.Range(0.04f, 0.09f));

            DayNightLighting.ThunderBoost = Random.Range(0.12f, 0.28f) * peakScale;
            if (flashRenderer != null)
                flashRenderer.color = new Color(1f, 1f, 1f, Random.Range(0.04f, 0.12f));

            yield return new WaitForSecondsRealtime(Random.Range(0.05f, 0.14f));
        }

        DayNightLighting.ThunderBoost = 0f;
        if (flashRenderer != null)
            flashRenderer.color = new Color(1f, 1f, 1f, 0f);
    }
}
