using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sons do mundo — arraste clips no Inspector (vários por categoria).
/// <list type="bullet">
/// <item><b>Aleatórios</b> — ambiente / SFX em intervalos</item>
/// <item><b>Trovão</b> — só quando o clima dispara trovão</item>
/// <item><b>Chuva</b> — loop enquanto chove</item>
/// <item><b>Música</b> — toca e às vezes pausa</item>
/// <item><b>Passos</b> — enquanto o player anda</item>
/// </list>
/// Crie um empty "Sounds of the World" na cena ou deixe o bootstrap criar.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-5)]
public class SoundsOfTheWorld : MonoBehaviour
{
    public const string DefaultObjectName = "Sounds of the World";

    public static SoundsOfTheWorld Instance { get; private set; }

    [System.Serializable]
    public class RandomOneShotChannel
    {
        public string name = "Ambiente";
        public bool enabled = true;
        public AudioClip[] clips;
        [Tooltip("Espera mínima/máxima (s) entre tentativas.")]
        public Vector2 intervalSeconds = new(10f, 28f);
        [Range(0f, 1f)]
        [Tooltip("Chance de tocar quando o timer dispara.")]
        public float playChance = 0.75f;
        public Vector2 volumeRange = new(0.35f, 0.7f);
        public Vector2 pitchRange = new(0.92f, 1.08f);
    }

    [Header("Aleatórios (sem vínculo)")]
    [SerializeField] private RandomOneShotChannel[] randomChannels =
    {
        new()
        {
            name = "Ambiente",
            intervalSeconds = new Vector2(12f, 35f),
            playChance = 0.7f
        },
        new()
        {
            name = "Efeitos",
            intervalSeconds = new Vector2(6f, 18f),
            playChance = 0.55f
        }
    };

    [Header("Trovão (só no flash)")]
    [SerializeField] private AudioClip[] thunderClips;
    [SerializeField] private Vector2 thunderVolume = new(0.7f, 1f);
    [SerializeField] private Vector2 thunderPitch = new(0.9f, 1.05f);
    [SerializeField] private float thunderDelayMin;
    [SerializeField] private float thunderDelayMax = 0.35f;

    [Header("Chuva (loop enquanto chove)")]
    [SerializeField] private AudioClip[] rainLoopClips;
    [SerializeField] [Range(0f, 1f)] private float rainVolume = 0.55f;
    [SerializeField] private float rainFadeSeconds = 2.5f;

    [Header("Música (toca e às vezes para)")]
    [SerializeField] private AudioClip[] musicClips;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.4f;
    [SerializeField] private Vector2 musicPlaySeconds = new(45f, 120f);
    [SerializeField] private Vector2 musicSilenceSeconds = new(15f, 60f);
    [SerializeField] private bool musicStartsOnAwake = true;

    [Header("Passos do player")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.45f;
    [SerializeField] private Vector2 footstepPitch = new(0.92f, 1.08f);
    [SerializeField] private float footstepIntervalWalk = 0.38f;
    [SerializeField] private float footstepIntervalRun = 0.26f;

    private AudioSource sfxSource;
    private AudioSource thunderSource;
    private AudioSource rainSource;
    private AudioSource musicSource;
    private AudioSource footstepSource;

    private Coroutine[] randomRoutines;
    private Coroutine musicRoutine;
    private Coroutine rainFadeRoutine;
    private bool rainAudioActive;
    private bool wasRaining;
    private float footstepTimer;
    private PlayerController player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != GameScenes.Game && scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        EnsureInScene();
    }

    public static SoundsOfTheWorld EnsureInScene()
    {
        SoundsOfTheWorld existing = Object.FindAnyObjectByType<SoundsOfTheWorld>();
        if (existing != null)
            return existing;

        GameObject named = GameObject.Find(DefaultObjectName);
        if (named != null)
        {
            SoundsOfTheWorld onNamed = named.GetComponent<SoundsOfTheWorld>();
            if (onNamed == null)
                onNamed = named.AddComponent<SoundsOfTheWorld>();
            return onNamed;
        }

        // Aceita variações de nome que o usuário possa ter criado.
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name;
            if (n == DefaultObjectName || n == "SoundsOfTheWorld" || n == "Sounds Of The World")
            {
                SoundsOfTheWorld c = all[i].GetComponent<SoundsOfTheWorld>();
                if (c == null)
                    c = all[i].gameObject.AddComponent<SoundsOfTheWorld>();
                return c;
            }
        }

        GameObject go = new(DefaultObjectName);
        return go.AddComponent<SoundsOfTheWorld>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureSources();
    }

    private void OnEnable()
    {
        WorldAudioEvents.Thunder += OnThunder;
        WorldAudioEvents.RainStarted += OnRainStarted;
        WorldAudioEvents.RainStopped += OnRainStopped;
        StartRandomChannels();
        if (musicStartsOnAwake)
            RestartMusicCycle();
    }

    private void OnDisable()
    {
        WorldAudioEvents.Thunder -= OnThunder;
        WorldAudioEvents.RainStarted -= OnRainStarted;
        WorldAudioEvents.RainStopped -= OnRainStopped;
        StopAllCoroutines();
        randomRoutines = null;
        musicRoutine = null;
        rainFadeRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        player = Object.FindAnyObjectByType<PlayerController>();
        SyncRainFromWeather();
    }

    private void Update()
    {
        UpdateFootsteps();
        PollRainState();
    }

    private void EnsureSources()
    {
        sfxSource = CreateSource("SFX", false);
        thunderSource = CreateSource("Thunder", false);
        rainSource = CreateSource("Rain", true);
        musicSource = CreateSource("Music", true);
        footstepSource = CreateSource("Footsteps", false);

        rainSource.loop = true;
        musicSource.loop = false;
    }

    private AudioSource CreateSource(string childName, bool loop)
    {
        Transform child = transform.Find(childName);
        GameObject go = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
            go.transform.SetParent(transform, false);

        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null)
            source = go.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void StartRandomChannels()
    {
        if (randomChannels == null || randomChannels.Length == 0)
            return;

        randomRoutines = new Coroutine[randomChannels.Length];
        for (int i = 0; i < randomChannels.Length; i++)
            randomRoutines[i] = StartCoroutine(RandomChannelLoop(randomChannels[i]));
    }

    private IEnumerator RandomChannelLoop(RandomOneShotChannel channel)
    {
        // Atraso inicial aleatório para não sincronizar tudo no Play.
        yield return new WaitForSeconds(Random.Range(1f, 8f));

        while (enabled)
        {
            if (channel == null || !channel.enabled || channel.clips == null || channel.clips.Length == 0)
            {
                yield return new WaitForSeconds(3f);
                continue;
            }

            float wait = Random.Range(
                Mathf.Max(0.5f, channel.intervalSeconds.x),
                Mathf.Max(channel.intervalSeconds.x, channel.intervalSeconds.y));
            yield return new WaitForSeconds(wait);

            if (!channel.enabled)
                continue;
            if (Random.value > channel.playChance)
                continue;

            AudioClip clip = Pick(channel.clips);
            if (clip == null)
                continue;

            float volume = Random.Range(channel.volumeRange.x, channel.volumeRange.y);
            float pitch = Random.Range(channel.pitchRange.x, channel.pitchRange.y);
            PlayOneShot(sfxSource, clip, volume, pitch);
        }
    }

    private void OnThunder(bool strong)
    {
        if (thunderClips == null || thunderClips.Length == 0)
            return;

        StartCoroutine(PlayThunderDelayed(strong));
    }

    private IEnumerator PlayThunderDelayed(bool strong)
    {
        float delay = Random.Range(thunderDelayMin, Mathf.Max(thunderDelayMin, thunderDelayMax));
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        AudioClip clip = Pick(thunderClips);
        if (clip == null)
            yield break;

        float volume = Random.Range(thunderVolume.x, thunderVolume.y);
        if (strong)
            volume = Mathf.Min(1f, volume * 1.15f);
        float pitch = Random.Range(thunderPitch.x, thunderPitch.y);
        PlayOneShot(thunderSource, clip, volume, pitch);
    }

    private void OnRainStarted()
    {
        StartRainAudio();
    }

    private void OnRainStopped()
    {
        StopRainAudio();
    }

    private void PollRainState()
    {
        RainWeatherSystem weather = RainWeatherSystem.Instance;
        bool raining = weather != null && weather.IsRaining;
        if (raining == wasRaining)
            return;

        wasRaining = raining;
        // Backup se o evento não disparou (ordem de Awake).
        if (raining)
            StartRainAudio();
        else
            StopRainAudio();
    }

    private void SyncRainFromWeather()
    {
        RainWeatherSystem weather = RainWeatherSystem.Instance;
        wasRaining = weather != null && weather.IsRaining;
        if (wasRaining)
            StartRainAudio();
    }

    private void StartRainAudio()
    {
        AudioClip clip = Pick(rainLoopClips);
        if (clip == null || rainSource == null)
            return;

        if (rainAudioActive && rainSource.isPlaying && rainSource.clip == clip)
            return;

        if (rainFadeRoutine != null)
            StopCoroutine(rainFadeRoutine);

        rainSource.clip = clip;
        rainSource.volume = 0f;
        rainSource.loop = true;
        rainSource.Play();
        rainAudioActive = true;
        rainFadeRoutine = StartCoroutine(FadeSource(rainSource, rainVolume, rainFadeSeconds));
    }

    private void StopRainAudio()
    {
        if (!rainAudioActive || rainSource == null)
            return;

        if (rainFadeRoutine != null)
            StopCoroutine(rainFadeRoutine);
        rainFadeRoutine = StartCoroutine(FadeOutAndStop(rainSource, rainFadeSeconds));
        rainAudioActive = false;
    }

    private void RestartMusicCycle()
    {
        if (musicRoutine != null)
            StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(MusicCycle());
    }

    private IEnumerator MusicCycle()
    {
        if (musicClips == null || musicClips.Length == 0)
            yield break;

        while (enabled)
        {
            AudioClip clip = Pick(musicClips);
            if (clip == null)
            {
                yield return new WaitForSeconds(5f);
                continue;
            }

            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.loop = true;
            musicSource.Play();

            float playFor = Random.Range(
                Mathf.Max(5f, musicPlaySeconds.x),
                Mathf.Max(musicPlaySeconds.x, musicPlaySeconds.y));
            yield return new WaitForSeconds(playFor);

            // Às vezes para (silêncio).
            float fade = Mathf.Min(2f, playFor * 0.1f);
            yield return FadeOutAndStop(musicSource, fade);

            float silence = Random.Range(
                Mathf.Max(0f, musicSilenceSeconds.x),
                Mathf.Max(musicSilenceSeconds.x, musicSilenceSeconds.y));
            if (silence > 0f)
                yield return new WaitForSeconds(silence);
        }
    }

    private void UpdateFootsteps()
    {
        if (footstepClips == null || footstepClips.Length == 0 || footstepSource == null)
            return;

        if (player == null)
            player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null || !player.IsMoving)
        {
            footstepTimer = 0f;
            return;
        }

        float interval = player.IsRunning ? footstepIntervalRun : footstepIntervalWalk;
        footstepTimer += Time.deltaTime;
        if (footstepTimer < interval)
            return;

        footstepTimer = 0f;
        AudioClip clip = Pick(footstepClips);
        if (clip == null)
            return;

        float pitch = Random.Range(footstepPitch.x, footstepPitch.y);
        PlayOneShot(footstepSource, clip, footstepVolume, pitch);
    }

    private static void PlayOneShot(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        if (source == null || clip == null)
            return;

        source.pitch = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int guard = 0;
        while (guard++ < 8)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static IEnumerator FadeSource(AudioSource source, float targetVolume, float seconds)
    {
        if (source == null)
            yield break;

        float start = source.volume;
        float t = 0f;
        seconds = Mathf.Max(0.05f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, targetVolume, t / seconds);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private static IEnumerator FadeOutAndStop(AudioSource source, float seconds)
    {
        if (source == null)
            yield break;

        float start = source.volume;
        float t = 0f;
        seconds = Mathf.Max(0.05f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }

        source.Stop();
        source.clip = null;
        source.volume = start;
    }
}
