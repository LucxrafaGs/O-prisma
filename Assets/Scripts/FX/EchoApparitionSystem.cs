using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawna o eco: primeiro sussurros (volume subindo), depois a silhueta andando.
/// </summary>
[DefaultExecutionOrder(-4)]
public class EchoApparitionSystem : MonoBehaviour
{
    public static EchoApparitionSystem Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private bool enabledSpawn = true;
    [SerializeField] private Vector2 spawnIntervalSeconds = new(28f, 75f);
    [SerializeField] [Range(0f, 1f)] private float spawnChance = 0.55f;
    [SerializeField] private bool allowDuringRain = true;
    [SerializeField] private Vector2 preludeWhisperSeconds = new(3.2f, 5.5f);
    [SerializeField] private Vector2 preludeWhisperVolume = new(0.08f, 0.48f);

    private float nextSpawnAt;
    private EchoApparition active;
    private Coroutine encounterRoutine;
    private bool encounterBusy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != GameScenes.Game && scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        if (Object.FindAnyObjectByType<EchoApparitionSystem>() != null)
            return;

        GameObject go = new("EchoApparitionSystem");
        go.AddComponent<EchoApparitionSystem>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ScheduleNext();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        EchoSilhouetteFactory.ClearForDomainReload();
    }

    private void Update()
    {
        if (!enabledSpawn || encounterBusy)
            return;
        if (active != null)
        {
            if (active.gameObject == null)
                active = null;
            else
                return;
        }

        if (Time.time < nextSpawnAt)
            return;

        ScheduleNext();
        if (Random.value > spawnChance)
            return;
        if (!allowDuringRain && RainWeatherSystem.Instance != null && RainWeatherSystem.Instance.IsRaining)
            return;

        BeginEncounter();
    }

    /// <summary>Dev Mode: sussurros primeiro, depois o eco anda até o jogador.</summary>
    public void ForceSpawn()
    {
        BeginEncounter();
    }

    private void BeginEncounter()
    {
        if (encounterRoutine != null)
            StopCoroutine(encounterRoutine);

        if (active != null)
        {
            Destroy(active.gameObject);
            active = null;
        }

        encounterRoutine = StartCoroutine(EncounterRoutine());
    }

    private IEnumerator EncounterRoutine()
    {
        encounterBusy = true;

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            encounterBusy = false;
            encounterRoutine = null;
            yield break;
        }

        SoundsOfTheWorld audio = SoundsOfTheWorld.Instance ?? SoundsOfTheWorld.EnsureInScene();
        float prelude = Random.Range(
            Mathf.Max(1.5f, preludeWhisperSeconds.x),
            Mathf.Max(preludeWhisperSeconds.x, preludeWhisperSeconds.y));
        float volStart = preludeWhisperVolume.x;
        float volMid = preludeWhisperVolume.y;

        audio?.BeginEncounterWhisper(volStart);

        float t = 0f;
        while (t < prelude)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / prelude);
            // Sobe suave, com um empurrão no final antes do eco aparecer.
            float curved = p * p;
            audio?.SetEncounterWhisperVolume(Mathf.Lerp(volStart, volMid, curved));
            yield return null;
        }

        active = EchoApparition.Spawn(player.transform);

        while (active != null && active.gameObject != null)
            yield return null;

        active = null;
        audio?.EndEncounterWhisper(1.2f);

        encounterBusy = false;
        encounterRoutine = null;
        ScheduleNext();
    }

    private void ScheduleNext()
    {
        nextSpawnAt = Time.time + Random.Range(
            Mathf.Max(5f, spawnIntervalSeconds.x),
            Mathf.Max(spawnIntervalSeconds.x, spawnIntervalSeconds.y));
    }
}
