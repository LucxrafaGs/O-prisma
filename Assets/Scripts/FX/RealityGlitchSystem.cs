using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Glitches de realidade: distorcem a visão do jogador (tela toda ou ponto).
/// Disparo via <see cref="DevFireShowcase"/> no modo Dev.
/// </summary>
[DefaultExecutionOrder(-20)]
public class RealityGlitchSystem : MonoBehaviour
{
    public enum GlitchKind
    {
        DigitalTear = 0,
        ChromaticStatic = 1,
        GlassBubble = 2,
        CorruptBlocks = 3,
        WaveInvert = 4,
        RealityVortex = 5
    }

    public static RealityGlitchSystem Instance { get; private set; }

    private const string ShaderName = "Prisma/RealityGlitch";

    private Material glitchMaterial;
    private RenderTexture captureRt;
    private Canvas overlayCanvas;
    private RawImage overlayImage;
    private Coroutine running;
    private bool capturing;

    private static readonly Color Purple = new(0.45f, 0.05f, 0.72f, 1f);
    private static readonly Color Black = new(0.02f, 0f, 0.05f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureResources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (running != null)
            StopCoroutine(running);

        ReleaseRt();
        if (glitchMaterial != null)
            Destroy(glitchMaterial);
    }

    private void LateUpdate()
    {
        if (!capturing || overlayImage == null || !overlayImage.enabled)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        EnsureRt();
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = captureRt;
        cam.Render();
        cam.targetTexture = prev;

        if (glitchMaterial != null)
            glitchMaterial.SetFloat("_TimeSeed", Time.unscaledTime);
    }

    /// <summary>Dev: dispara as 5+ variações em sequência.</summary>
    public void DevFireShowcase()
    {
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(ShowcaseRoutine());
    }

    public void Play(GlitchKind kind, float duration = 1.4f, bool nearPlayer = false)
    {
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(PlayOne(kind, duration, nearPlayer));
    }

    private IEnumerator ShowcaseRoutine()
    {
        GlitchKind[] sequence =
        {
            GlitchKind.DigitalTear,
            GlitchKind.ChromaticStatic,
            GlitchKind.GlassBubble,
            GlitchKind.CorruptBlocks,
            GlitchKind.WaveInvert,
            GlitchKind.RealityVortex
        };

        for (int i = 0; i < sequence.Length; i++)
        {
            bool local = sequence[i] == GlitchKind.GlassBubble || sequence[i] == GlitchKind.RealityVortex;
            yield return PlayOne(sequence[i], 1.35f, local);
            yield return new WaitForSecondsRealtime(0.18f);
        }

        running = null;
    }

    private IEnumerator PlayOne(GlitchKind kind, float duration, bool nearPlayer)
    {
        EnsureResources();
        EnsureRt();

        if (glitchMaterial == null || overlayImage == null)
            yield break;

        Vector2 bubble = nearPlayer ? ScreenPointFromPlayer() : RandomBubbleCenter();
        glitchMaterial.SetFloat("_Mode", (float)kind);
        glitchMaterial.SetVector("_BubbleCenter", new Vector4(bubble.x, bubble.y, 0f, 0f));
        glitchMaterial.SetFloat("_BubbleRadius", Random.Range(0.18f, 0.38f));
        glitchMaterial.SetColor("_Purple", Purple);
        glitchMaterial.SetColor("_Black", Black);
        glitchMaterial.SetFloat("_Intensity", 0f);

        overlayImage.texture = captureRt;
        overlayImage.material = glitchMaterial;
        overlayImage.enabled = true;
        capturing = true;

        float half = duration * 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float peak;
            if (t < half)
                peak = Mathf.SmoothStep(0f, 1f, t / half);
            else
                peak = Mathf.SmoothStep(1f, 0f, (t - half) / Mathf.Max(0.01f, duration - half));

            // Pico com “engasgos” de bug.
            float stutter = 1f;
            if (Random.value > 0.92f)
                stutter = Random.Range(0.4f, 1.35f);

            glitchMaterial.SetFloat("_Intensity", peak * stutter);
            glitchMaterial.SetFloat("_TimeSeed", Time.unscaledTime * (1f + peak));

            if (kind == GlitchKind.GlassBubble || kind == GlitchKind.RealityVortex)
            {
                // Bolha anda um pouco (matéria instável).
                bubble += new Vector2(Mathf.Sin(Time.unscaledTime * 3f), Mathf.Cos(Time.unscaledTime * 2.2f)) * (0.0008f * peak);
                glitchMaterial.SetVector("_BubbleCenter", new Vector4(bubble.x, bubble.y, 0f, 0f));
            }

            yield return null;
        }

        capturing = false;
        overlayImage.enabled = false;
        overlayImage.texture = null;
        glitchMaterial.SetFloat("_Intensity", 0f);
    }

    private Vector2 ScreenPointFromPlayer()
    {
        Camera cam = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            player = GameObject.Find("Player");

        if (cam == null || player == null)
            return RandomBubbleCenter();

        Vector3 sp = cam.WorldToViewportPoint(player.transform.position);
        return new Vector2(Mathf.Clamp01(sp.x), Mathf.Clamp01(sp.y));
    }

    private static Vector2 RandomBubbleCenter()
    {
        return new Vector2(Random.Range(0.25f, 0.75f), Random.Range(0.25f, 0.75f));
    }

    private void EnsureResources()
    {
        if (glitchMaterial == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader != null)
                glitchMaterial = new Material(shader) { name = "RealityGlitch_Runtime" };
            else
                Debug.LogError("Prisma: shader Prisma/RealityGlitch não encontrado.");
        }

        if (overlayCanvas != null)
            return;

        GameObject canvasObject = new(
            "RealityGlitchCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // abaixo do DevMode (200), acima do jogo
        overlayCanvas = canvas;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageObject = new("GlitchOverlay", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = imageObject.GetComponent<RawImage>();
        overlayImage.raycastTarget = false;
        overlayImage.enabled = false;
        overlayImage.color = Color.white;
    }

    private void EnsureRt()
    {
        int w = Mathf.Max(8, Screen.width);
        int h = Mathf.Max(8, Screen.height);
        if (captureRt != null && captureRt.width == w && captureRt.height == h)
            return;

        ReleaseRt();
        captureRt = new RenderTexture(w, h, 0, RenderTextureFormat.DefaultHDR)
        {
            name = "RealityGlitchCapture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        captureRt.Create();
    }

    private void ReleaseRt()
    {
        if (captureRt == null)
            return;
        captureRt.Release();
        Destroy(captureRt);
        captureRt = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        // Só na cena de jogo — evita canvas/host lixo no MainMenu.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != GameScenes.Game && scene.path != "Assets/Scenes/SampleScene.unity")
            return;

        GameObject host = GameObject.Find("NpcWorld");
        if (host == null)
            host = new GameObject("RealityGlitchSystem");

        if (host.GetComponent<RealityGlitchSystem>() == null)
            host.AddComponent<RealityGlitchSystem>();
    }
}
