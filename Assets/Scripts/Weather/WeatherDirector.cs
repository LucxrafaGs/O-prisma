using UnityEngine;

/// <summary>
/// Clima automático: chuva e névoa independentes, com chances por estação e horário.
/// </summary>
[DefaultExecutionOrder(-14)]
public class WeatherDirector : MonoBehaviour
{
    public static WeatherDirector Instance { get; private set; }

    private const float MinRainRealSeconds = 5f * 60f;
    private const float FogWithRainChance = 0.8f;
    private const float RainEndsMiddayChance = 0.45f;

    private const int MorningFogStartMinutes = 6 * 60;
    private const int MorningFogEndMinutes = 7 * 60 + 30;
    private const int MorningFogClearMinutes = 8 * 60 + 30;
    private const int NightStartMinutes = 19 * 60;
    private const int MiddayMinutes = 12 * 60;

    private RainWeatherSystem weather;
    private bool manualOverride;
    private bool fogManualOverride;
    private bool rainActive;
    private bool rainEndsAtMidday;
    private float rainStartedUnscaled;
    private float rainMinEndUnscaled;

    private bool morningFogRolled;
    private bool nightFogRolled;
    private bool daytimeFogRolled;
    private bool ambientFogActive;
    private int ambientFogClearMinutes = -1;
    private int lastDayStamp = -1;
    private bool nightThunderActive;

    public static void ClearInstanceForDomainReload() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        weather = GetComponent<RainWeatherSystem>();
        if (weather == null)
            weather = gameObject.AddComponent<RainWeatherSystem>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        GameTimeClock.OnDayStarted += OnDayStarted;
        GameTimeClock.OnTimeChanged += OnTimeChanged;
        GameTimeClock.OnSeasonChanged += OnSeasonChanged;
    }

    private void OnDisable()
    {
        GameTimeClock.OnDayStarted -= OnDayStarted;
        GameTimeClock.OnTimeChanged -= OnTimeChanged;
        GameTimeClock.OnSeasonChanged -= OnSeasonChanged;
    }

    private void Start()
    {
        // Se há load pendente, GameSessionSave restaura clima/tempo.
        if (GameFlowState.PendingLoad != null)
            return;

        EvaluateNewDay();
        OnTimeChanged();
    }

    private void Update()
    {
        if (manualOverride || weather == null)
            return;

        UpdateRainDuration();
        UpdateAmbientFogClear();
        UpdateNightDryThunder();
    }

    /// <summary>Dev Mode: força chuva e pausa o clima automático enquanto estiver forçado.</summary>
    public void DevToggleRain()
    {
        EnsureWeather();
        if (weather.IsRaining)
        {
            weather.EndRainKeepAmbientFog();
            if (!fogManualOverride && !ambientFogActive)
                weather.StopFogSmooth();
            manualOverride = false;
            rainActive = false;
            return;
        }

        manualOverride = true;
        rainActive = true;
        weather.BeginRainWithFogChance(FogWithRainChance, allowOpeningThunder: true);
    }

    /// <summary>Dev Mode: força só a neblina (independente da chuva).</summary>
    public void DevToggleFog()
    {
        EnsureWeather();
        fogManualOverride = true;
        if (weather.IsFoggy)
        {
            weather.StopFogSmooth();
            ambientFogActive = false;
        }
        else
        {
            weather.BeginFogSmooth();
            ambientFogActive = !weather.IsRaining;
        }
    }

    /// <summary>Dev Mode: dispara um trovão (funciona sem chuva).</summary>
    public void DevTriggerThunder()
    {
        EnsureWeather();
        weather.PlayIsolatedThunder();
    }

    /// <summary>Restaura chuva/névoa exatamente como no save.</summary>
    public void RestoreFromSave(bool raining, bool foggy)
    {
        EnsureWeather();
        manualOverride = false;
        fogManualOverride = false;
        rainActive = raining;
        ambientFogActive = foggy && !raining;
        StopNightThunder();

        GameTimeClock clock = GameTimeClock.Instance;
        int minutes = clock != null ? clock.MinutesSinceMidnight : MorningFogStartMinutes;
        if (clock != null)
            lastDayStamp = clock.DayOfMonth + ((int)clock.CurrentSeason * 100) + clock.Year * 10000;

        morningFogRolled = raining || foggy || minutes > MorningFogEndMinutes;
        nightFogRolled = raining || foggy || (minutes >= NightStartMinutes || minutes < MorningFogStartMinutes);
        daytimeFogRolled = raining || foggy || minutes < MorningFogClearMinutes || minutes >= NightStartMinutes;

        weather.SetWeatherImmediate(raining, foggy);
        RefreshNightThunderState(minutes);
    }

    private void OnDayStarted() => EvaluateNewDay();

    private void OnSeasonChanged()
    {
        // Nova estação: reavalia só se ainda não chove.
        if (!rainActive && !manualOverride)
            TryStartDailyRain();
    }

    private void OnTimeChanged()
    {
        if (manualOverride || weather == null || GameTimeClock.Instance == null)
            return;

        int minutes = GameTimeClock.Instance.MinutesSinceMidnight;
        TryMorningFog(minutes);
        TryNightFog(minutes);
        TryDaytimeFog(minutes);
        RefreshNightThunderState(minutes);
    }

    private void EvaluateNewDay()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        int stamp = clock.DayOfMonth + ((int)clock.CurrentSeason * 100) + clock.Year * 10000;
        if (stamp == lastDayStamp)
            return;

        lastDayStamp = stamp;
        morningFogRolled = false;
        nightFogRolled = false;
        daytimeFogRolled = false;
        ambientFogClearMinutes = -1;
        fogManualOverride = false;
        StopNightThunder();

        if (!manualOverride)
        {
            StopRainInternal(keepAmbientFog: false);
            ambientFogActive = false;
            weather?.StopFogSmooth();
            TryStartDailyRain();
        }
    }

    private void TryStartDailyRain()
    {
        EnsureWeather();
        if (manualOverride || rainActive || weather == null)
            return;

        float chance = GetRainChance(GameTimeClock.Instance.CurrentSeason);
        if (Random.value > chance)
            return;

        BeginRainInternal();
    }

    private void BeginRainInternal()
    {
        rainActive = true;
        rainStartedUnscaled = Time.unscaledTime;
        rainMinEndUnscaled = rainStartedUnscaled + MinRainRealSeconds;
        rainEndsAtMidday = Random.value < RainEndsMiddayChance;
        weather.BeginRainWithFogChance(FogWithRainChance, allowOpeningThunder: true);
    }

    private void UpdateRainDuration()
    {
        if (!rainActive || weather == null || !weather.IsRaining)
            return;

        if (Time.unscaledTime < rainMinEndUnscaled)
            return;

        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        if (rainEndsAtMidday && clock.MinutesSinceMidnight >= MiddayMinutes)
        {
            StopRainInternal(keepAmbientFog: false);
            return;
        }

        // Dia inteiro: encerra ao chegar a hora de dormir / novo ciclo.
        if (!rainEndsAtMidday && clock.Hour == GameTimeClock.SleepHour && clock.Minute == 0)
            StopRainInternal(keepAmbientFog: false);
    }

    private void StopRainInternal(bool keepAmbientFog)
    {
        rainActive = false;
        if (weather == null)
            return;

        if (keepAmbientFog)
            weather.EndRainKeepAmbientFog();
        else
        {
            weather.EndRainKeepAmbientFog();
            if (!ambientFogActive)
                weather.StopFogSmooth();
        }
    }

    private void TryMorningFog(int minutes)
    {
        if (fogManualOverride)
            return;

        if (morningFogRolled)
        {
            if (ambientFogActive && ambientFogClearMinutes > 0 && minutes >= ambientFogClearMinutes && !weather.IsRaining)
            {
                ambientFogActive = false;
                ambientFogClearMinutes = -1;
                weather.StopFogSmooth();
            }
            return;
        }

        if (minutes < MorningFogStartMinutes || minutes > MorningFogEndMinutes)
            return;

        morningFogRolled = true;
        if (weather.IsRaining || weather.IsFoggy)
            return;

        float chance = GetMorningFogChance(GameTimeClock.Instance.CurrentSeason);
        if (Random.value > chance)
            return;

        ambientFogActive = true;
        ambientFogClearMinutes = MorningFogClearMinutes;
        weather.BeginFogSmooth();
    }

    private void TryNightFog(int minutes)
    {
        if (fogManualOverride)
            return;

        bool isNight = minutes >= NightStartMinutes || minutes < MorningFogStartMinutes;
        if (!isNight)
            return;

        if (nightFogRolled)
            return;

        // Só rola uma vez por noite, depois das 19h (ou madrugada se já passou).
        if (minutes < NightStartMinutes && minutes >= MorningFogStartMinutes)
            return;

        nightFogRolled = true;
        if (weather.IsRaining || weather.IsFoggy)
            return;

        float chance = GetNightFogChance(GameTimeClock.Instance.CurrentSeason);
        if (Random.value > chance)
            return;

        ambientFogActive = true;
        // Limpa no amanhecer.
        ambientFogClearMinutes = MorningFogStartMinutes;
        weather.BeginFogSmooth();
    }

    private void TryDaytimeFog(int minutes)
    {
        if (fogManualOverride)
            return;

        if (daytimeFogRolled)
            return;

        // Janela diurna fora de manhã/noite.
        if (minutes < MorningFogClearMinutes || minutes >= NightStartMinutes)
            return;

        daytimeFogRolled = true;
        if (weather.IsRaining || weather.IsFoggy)
            return;

        float chance = GetDaytimeFogChance(GameTimeClock.Instance.CurrentSeason);
        if (Random.value > chance)
            return;

        ambientFogActive = true;
        ambientFogClearMinutes = Mathf.Min(minutes + 90, NightStartMinutes - 1);
        weather.BeginFogSmooth();
    }

    private void UpdateAmbientFogClear()
    {
        if (fogManualOverride || !ambientFogActive || weather == null || weather.IsRaining)
            return;

        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null || ambientFogClearMinutes < 0)
            return;

        int minutes = clock.MinutesSinceMidnight;
        // Limpeza atravessando meia-noite (névoa noturna → 6:00).
        if (ambientFogClearMinutes == MorningFogStartMinutes)
        {
            if (minutes >= MorningFogStartMinutes && minutes < NightStartMinutes)
            {
                ambientFogActive = false;
                ambientFogClearMinutes = -1;
                weather.StopFogSmooth();
            }
            return;
        }

        if (minutes >= ambientFogClearMinutes)
        {
            ambientFogActive = false;
            ambientFogClearMinutes = -1;
            weather.StopFogSmooth();
        }
    }

    private void UpdateNightDryThunder()
    {
        if (weather == null || GameTimeClock.Instance == null)
            return;

        int minutes = GameTimeClock.Instance.MinutesSinceMidnight;
        RefreshNightThunderState(minutes);
    }

    private void RefreshNightThunderState(int minutes)
    {
        bool isNight = minutes >= NightStartMinutes || minutes < MorningFogStartMinutes;
        if (isNight && (weather == null || !weather.IsRaining))
            StartNightThunder();
        else
            StopNightThunder();
    }

    private void StartNightThunder()
    {
        if (nightThunderActive)
            return;

        nightThunderActive = true;
        weather?.StartDryNightThunderLoop();
    }

    private void StopNightThunder()
    {
        if (!nightThunderActive)
            return;

        nightThunderActive = false;
        weather?.StopDryNightThunderLoop();
    }

    private void EnsureWeather()
    {
        if (weather == null)
            weather = GetComponent<RainWeatherSystem>() ?? gameObject.AddComponent<RainWeatherSystem>();
    }

    private static float GetRainChance(GameTimeClock.Season season)
    {
        return season switch
        {
            GameTimeClock.Season.Verao => 0.18f,
            GameTimeClock.Season.Outono => 0.42f,
            GameTimeClock.Season.Inverno => Random.Range(0.69f, 0.75f),
            _ => Random.Range(0.28f, 0.52f) // Primavera variável
        };
    }

    private static float GetMorningFogChance(GameTimeClock.Season season)
    {
        return season switch
        {
            GameTimeClock.Season.Verao => 0.12f,
            GameTimeClock.Season.Outono => 0.58f,
            GameTimeClock.Season.Inverno => 0.68f,
            _ => 0.28f
        };
    }

    private static float GetNightFogChance(GameTimeClock.Season season)
    {
        return season switch
        {
            GameTimeClock.Season.Verao => 0.08f,
            GameTimeClock.Season.Outono => 0.42f,
            GameTimeClock.Season.Inverno => 0.52f,
            _ => 0.14f
        };
    }

    private static float GetDaytimeFogChance(GameTimeClock.Season season)
    {
        return season switch
        {
            GameTimeClock.Season.Verao => 0.03f,
            GameTimeClock.Season.Outono => 0.16f,
            GameTimeClock.Season.Inverno => 0.22f,
            _ => 0.06f
        };
    }
}
