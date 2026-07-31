using System;
using UnityEngine;

/// <summary>
/// Captura e restaura o estado completo do jogo + autosave.
/// </summary>
[DefaultExecutionOrder(50)]
public class GameSessionSave : MonoBehaviour
{
    public static GameSessionSave Instance { get; private set; }

    public const float AutosaveIntervalSeconds = 10f * 60f;
    private const string ActiveSlotPrefsKey = "prisma_active_save_slot";

    [SerializeField] private float autosaveInterval = AutosaveIntervalSeconds;

    private float autosaveTimer;
    private bool worldApplied;
    private bool quitSaved;

    public static void ClearInstanceForDomainReload() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (GameFlowState.ActiveSaveSlot < 0)
            GameFlowState.ActiveSaveSlot = PlayerPrefs.GetInt(ActiveSlotPrefsKey, -1);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        ApplyPendingLoadIfAny();
        autosaveTimer = 0f;
    }

    private void Update()
    {
        if (!worldApplied && GameFlowState.PendingLoad == null)
            worldApplied = true;

        if (GameFlowState.ActiveSaveSlot < 0)
            return;

        autosaveTimer += Time.unscaledDeltaTime;
        if (autosaveTimer < autosaveInterval)
            return;

        autosaveTimer = 0f;
        TryAutosave("automático (10 min)");
    }

    private void OnApplicationQuit()
    {
        SaveOnExit();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveOnExit();
    }

    private void OnDisable()
    {
        // Ao sair da cena de jogo (menu / fechar play), tenta autosave.
        if (Application.isPlaying)
            SaveOnExit();
    }

    public static void SetActiveSlot(int slot)
    {
        GameFlowState.ActiveSaveSlot = slot;
        PlayerPrefs.SetInt(ActiveSlotPrefsKey, slot);
        PlayerPrefs.Save();
    }

    public static GameSaveData CaptureCurrent()
    {
        string name = CharacterProfileData.LoadName();
        CharacterGender gender = CharacterProfileData.LoadGender();
        GameSaveData data = GameSaveData.FromSelection(name, gender, CharacterAppearanceData.Load());
        data.savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        GameTimeClock clock = GameTimeClock.Instance;
        if (clock != null)
        {
            data.year = clock.Year;
            data.dayOfMonth = clock.DayOfMonth;
            data.season = (int)clock.CurrentSeason;
            data.dayElapsedRealSeconds = clock.DayElapsedRealSeconds;
            data.sleepPending = clock.IsSleepPending;
        }

        Transform player = FindPlayerTransform();
        if (player != null)
        {
            Vector3 p = player.position;
            data.playerX = p.x;
            data.playerY = p.y;
            data.hasPlayerPosition = true;
        }

        RainWeatherSystem weather = RainWeatherSystem.Instance;
        if (weather != null)
        {
            data.isRaining = weather.IsRaining;
            data.isFoggy = weather.IsFoggy;
        }

        data.hotbarIndex = PlayerHotbar.CurrentIndex;
        data.saveVersion = 2;
        return data;
    }

    public static bool TryAutosave(string reason = "automático")
    {
        int slot = GameFlowState.ActiveSaveSlot;
        if (slot < 0 || slot >= GameSaveSystem.MaxSlots)
            return false;

        GameSaveData data = CaptureCurrent();
        if (data.IsEmpty)
            return false;

        GameSaveSystem.SaveSlot(slot, data);
        Debug.Log($"Prisma: save {reason} no slot {slot + 1}.");
        return true;
    }

    public static bool SaveManualToActiveSlot()
    {
        return TryAutosave("manual");
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null || data.IsEmpty)
            return;

        GameSaveSystem.ApplyToActiveProfile(data);

        GameTimeClock clock = GameTimeClock.Instance;
        if (clock != null)
            clock.ApplySaveState(data.year, data.dayOfMonth, (GameTimeClock.Season)data.season, data.dayElapsedRealSeconds, data.sleepPending);

        WeatherDirector director = WeatherDirector.Instance;
        if (director != null)
            director.RestoreFromSave(data.isRaining, data.isFoggy);
        else
        {
            RainWeatherSystem weather = RainWeatherSystem.Instance;
            if (weather != null)
            {
                weather.SetRaining(data.isRaining);
                weather.SetFoggy(data.isFoggy);
            }
        }

        Transform player = FindPlayerTransform();
        if (player != null && data.hasPlayerPosition)
        {
            player.position = new Vector3(data.playerX, data.playerY, player.position.z);
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = new Vector2(data.playerX, data.playerY);
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
                controller.ResolveSolidOverlaps();
        }

        PlayerHotbar.EnsureDefaults();
        PlayerHotbar.SetIndex(data.hotbarIndex);
        worldApplied = true;
    }

    private void ApplyPendingLoadIfAny()
    {
        GameSaveData pending = GameFlowState.PendingLoad;
        if (pending == null || pending.IsEmpty)
        {
            worldApplied = true;
            return;
        }

        GameFlowState.PendingLoad = null;
        ApplySaveData(pending);
    }

    private void SaveOnExit()
    {
        if (quitSaved)
            return;

        quitSaved = true;
        TryAutosave("ao sair");
    }

    private static Transform FindPlayerTransform()
    {
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        if (controller != null)
            return controller.transform;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }
}
