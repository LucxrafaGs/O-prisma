using System;
using UnityEngine;

/// <summary>
/// One calendar day = 20 real minutes. Clock shows normal HH:MM.
/// At 03:00 the player must sleep; day advances to 06:00 next morning.
/// </summary>
public class GameTimeClock : MonoBehaviour
{
    public const float RealSecondsPerDay = 20f * 60f;
    public const int DaysPerSeason = 28;
    public const int WakeHour = 6;
    public const int SleepHour = 3;
    public const float WakeGameMinutes = (24 - WakeHour + SleepHour) * 60f; // 21h

    public static GameTimeClock Instance { get; private set; }

    public static void ClearInstanceForDomainReload() => Instance = null;

    public static void ClearStaticEventsForDomainReload()
    {
        OnDayStarted = null;
        OnSleepRequired = null;
        OnTimeChanged = null;
        OnSeasonChanged = null;
    }

    public enum Season
    {
        Primavera,
        Verao,
        Outono,
        Inverno
    }

    public static event Action OnDayStarted;
    public static event Action OnSleepRequired;
    public static event Action OnTimeChanged;
    public static event Action OnSeasonChanged;

    [SerializeField] private bool pauseWhenMenusOpen = true;

    private float dayElapsedRealSeconds;
    private bool sleepPending;

    public int DayOfMonth { get; private set; } = 1;
    public int Year { get; private set; } = 1;
    public Season CurrentSeason { get; private set; } = Season.Primavera;
    public int Hour { get; private set; } = WakeHour;
    public int Minute { get; private set; }

    public string TimeLabel => $"{Hour:00}:{Minute:00}";
    public string DateLabel => $"Ano {Year} · Dia {DayOfMonth}";
    public string SeasonLabel => CurrentSeason switch
    {
        Season.Verao => "Verão",
        Season.Outono => "Outono",
        Season.Inverno => "Inverno",
        _ => "Primavera"
    };

    public bool IsSleepPending => sleepPending;
    public float DayElapsedRealSeconds => dayElapsedRealSeconds;

    /// <summary>Minutes since midnight (0-1439).</summary>
    public int MinutesSinceMidnight => Hour * 60 + Minute;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
            return;
        }

        Instance = this;
        ResetToMorning();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (sleepPending)
            return;

        if (pauseWhenMenusOpen && (PrismaBackpackMenu.IsOpen || DevModeController.IsOpen || Time.timeScale <= 0f))
            return;

        dayElapsedRealSeconds += Time.unscaledDeltaTime;
        if (dayElapsedRealSeconds > RealSecondsPerDay)
            dayElapsedRealSeconds = RealSecondsPerDay;

        int previousMinuteStamp = Hour * 60 + Minute;
        RecalculateClock();
        int currentMinuteStamp = Hour * 60 + Minute;
        if (currentMinuteStamp != previousMinuteStamp)
            OnTimeChanged?.Invoke();

        if (Hour == SleepHour && Minute == 0 && dayElapsedRealSeconds > 1f)
            TriggerSleepRequired();
    }

    private void RecalculateClock()
    {
        float t = Mathf.Clamp01(dayElapsedRealSeconds / RealSecondsPerDay);
        int totalMinutesFromWake = Mathf.FloorToInt(t * WakeGameMinutes);

        int minutesFromMidnight = WakeHour * 60 + totalMinutesFromWake;
        if (minutesFromMidnight >= 24 * 60)
            minutesFromMidnight -= 24 * 60;

        Hour = minutesFromMidnight / 60;
        Minute = minutesFromMidnight % 60;

        if (t >= 1f)
        {
            Hour = SleepHour;
            Minute = 0;
        }
    }

    private void TriggerSleepRequired()
    {
        if (sleepPending)
            return;

        sleepPending = true;
        Hour = SleepHour;
        Minute = 0;
        OnSleepRequired?.Invoke();
        OnTimeChanged?.Invoke();
    }

    public void SleepUntilMorning()
    {
        AdvanceDay();
        ResetToMorning();
        sleepPending = false;
        OnDayStarted?.Invoke();
        OnTimeChanged?.Invoke();
    }

    public void DevAddMinutes(int minutes)
    {
        if (minutes == 0)
            return;

        sleepPending = false;
        float deltaReal = minutes / WakeGameMinutes * RealSecondsPerDay;
        dayElapsedRealSeconds = Mathf.Clamp(dayElapsedRealSeconds + deltaReal, 0f, RealSecondsPerDay);
        RecalculateClock();
        OnTimeChanged?.Invoke();

        if (Hour == SleepHour && Minute == 0 && dayElapsedRealSeconds >= RealSecondsPerDay - 0.05f)
            TriggerSleepRequired();
    }

    public void DevSetTime(int hour, int minute)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);
        sleepPending = false;

        int targetFromMidnight = hour * 60 + minute;
        int wakeStart = WakeHour * 60;
        int minutesFromWake = targetFromMidnight >= wakeStart
            ? targetFromMidnight - wakeStart
            : (24 * 60 - wakeStart) + targetFromMidnight;

        minutesFromWake = Mathf.Clamp(minutesFromWake, 0, Mathf.FloorToInt(WakeGameMinutes));
        dayElapsedRealSeconds = minutesFromWake / WakeGameMinutes * RealSecondsPerDay;
        RecalculateClock();
        OnTimeChanged?.Invoke();
    }

    public void DevSetDay(int day)
    {
        DayOfMonth = Mathf.Clamp(day, 1, DaysPerSeason);
        OnTimeChanged?.Invoke();
    }

    public void DevSetSeason(Season season)
    {
        if (CurrentSeason == season)
        {
            OnTimeChanged?.Invoke();
            return;
        }

        CurrentSeason = season;
        OnSeasonChanged?.Invoke();
        OnTimeChanged?.Invoke();
    }

    public void DevSkipTo(int hour, int minute)
    {
        DevSetTime(hour, minute);
    }

    private void AdvanceDay()
    {
        DayOfMonth++;
        if (DayOfMonth <= DaysPerSeason)
            return;

        DayOfMonth = 1;
        if (CurrentSeason == Season.Inverno)
        {
            CurrentSeason = Season.Primavera;
            Year++;
        }
        else
        {
            CurrentSeason = CurrentSeason switch
            {
                Season.Primavera => Season.Verao,
                Season.Verao => Season.Outono,
                _ => Season.Inverno
            };
        }

        OnSeasonChanged?.Invoke();
    }

    /// <summary>Restaura calendário/relógio a partir do save (sem disparar novo dia).</summary>
    public void ApplySaveState(int year, int dayOfMonth, Season season, float elapsedRealSeconds, bool sleep)
    {
        Year = Mathf.Max(1, year);
        DayOfMonth = Mathf.Clamp(dayOfMonth, 1, DaysPerSeason);
        CurrentSeason = season;
        dayElapsedRealSeconds = Mathf.Clamp(elapsedRealSeconds, 0f, RealSecondsPerDay);
        sleepPending = sleep;
        RecalculateClock();
        OnSeasonChanged?.Invoke();
        OnTimeChanged?.Invoke();
    }

    private void ResetToMorning()
    {
        dayElapsedRealSeconds = 0f;
        Hour = WakeHour;
        Minute = 0;
    }
}
