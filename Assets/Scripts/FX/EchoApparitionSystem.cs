using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawna o eco (silhueta preta animada) perto do player em intervalos aleatórios.
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

    private float nextSpawnAt;
    private EchoApparition active;

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
        if (!enabledSpawn)
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

        TrySpawn();
    }

    /// <summary>Disparo manual (Dev Mode / testes).</summary>
    public void ForceSpawn()
    {
        if (active != null)
            Destroy(active.gameObject);
        TrySpawn();
    }

    private void TrySpawn()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
            return;

        active = EchoApparition.Spawn(player.transform);
    }

    private void ScheduleNext()
    {
        nextSpawnAt = Time.time + Random.Range(
            Mathf.Max(5f, spawnIntervalSeconds.x),
            Mathf.Max(spawnIntervalSeconds.x, spawnIntervalSeconds.y));
    }
}
