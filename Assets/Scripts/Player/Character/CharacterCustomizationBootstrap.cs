// Prisma - bootstrap da cena de personalizacao (reload)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class CharacterCustomizationBootstrap : MonoBehaviour
{
    public static RenderTexture PreviewRenderTexture { get; private set; }

    [SerializeField] private CharacterSpriteLibrary library;
    [SerializeField] private CharacterPreviewAnimator previewAnimator;
    [SerializeField] private CharacterCustomizationUI customizationUI;

    private void Awake()
    {
        QualitySettings.antiAliasing = 0;

        if (library == null)
            library = CharacterLibraryAccess.Get();

        library?.WarmUp();

        if (library == null || library.Entries.Count < 100)
        {
            Debug.LogWarning(
                library == null
                    ? "CharacterSpriteLibrary nao encontrada em Resources. Rode Prisma > Force Rebuild Character Library."
                    : $"CharacterSpriteLibrary desatualizada ({library.Entries.Count} itens). Rode Prisma > Force Rebuild Character Library.");
        }

        EnsureEventSystem();
        EnsureCanvas();
        EnsurePreview();
        EnsureUI();
    }

    private void OnDestroy()
    {
        if (PreviewRenderTexture != null)
        {
            PreviewRenderTexture.Release();
            PreviewRenderTexture = null;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(transform);
    }

    private void EnsureCanvas()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("CustomizationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsurePreview()
    {
        if (previewAnimator != null)
            return;

        ConfigureDisplayCamera(Camera.main);

        PreviewRenderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        PreviewRenderTexture.antiAliasing = 1;
        PreviewRenderTexture.filterMode = FilterMode.Point;
        PreviewRenderTexture.useMipMap = false;

        GameObject cameraObject = new GameObject("PreviewCamera");
        Camera previewCamera = cameraObject.AddComponent<Camera>();
        UniversalAdditionalCameraData previewCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        previewCameraData.renderType = CameraRenderType.Base;

        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 2.2f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.14f, 0.16f, 0.22f, 1f);
        previewCamera.transform.position = new Vector3(0f, 2f, -10f);
        previewCamera.targetTexture = PreviewRenderTexture;
        previewCamera.depth = 1;
        previewCamera.cullingMask = ~0;
        previewCamera.allowMSAA = false;
        previewCamera.useOcclusionCulling = false;

        GameObject previewRoot = new GameObject("PreviewPlayer");
        previewRoot.transform.position = Vector3.zero;
        previewRoot.transform.localScale = new Vector3(4f, 4f, 1f);

        previewRoot.AddComponent<PlayerAppearance>();
        previewAnimator = previewRoot.AddComponent<CharacterPreviewAnimator>();
    }

    private static void ConfigureDisplayCamera(Camera mainCamera)
    {
        if (mainCamera == null)
            return;

        mainCamera.enabled = true;
        mainCamera.targetTexture = null;
        mainCamera.targetDisplay = 0;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 1f);
        mainCamera.cullingMask = 0;
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5f;
        mainCamera.depth = -10;
        mainCamera.useOcclusionCulling = false;
    }

    private void EnsureUI()
    {
        if (customizationUI != null)
            return;

        GameObject uiObject = new GameObject("CustomizationUI");
        uiObject.transform.SetParent(transform);
        customizationUI = uiObject.AddComponent<CharacterCustomizationUI>();
        bool newCharacter = GameFlowState.StartNewCharacter;
        customizationUI.Initialize(library, previewAnimator, PreviewRenderTexture, newCharacter);
        GameFlowState.StartNewCharacter = false;
    }
}
