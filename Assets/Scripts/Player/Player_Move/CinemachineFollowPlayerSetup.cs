using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Câmera pixel-perfect estável: mesma grade do personagem (PPU 64),
/// Cinemachine em FixedUpdate + PixelPerfectCamera (sem shimmer/blur ao andar).
/// </summary>
[DefaultExecutionOrder(-50)]
public class CinemachineFollowPlayerSetup : MonoBehaviour
{
    private const int AssetsPpu = 64;
    private const int RefResolutionX = 640;
    private const int RefResolutionY = 360;

    [SerializeField] private Transform player;
    [SerializeField] private float orthographicSize = 5f;
    [SerializeField] private Vector3 followOffset = new(0f, 0f, -10f);
    [SerializeField] private Vector3 positionDamping = Vector3.zero;
    [SerializeField] private bool usePixelPerfect = true;

    private Camera mainCamera;
    private CinemachineBrain brain;
    private PixelPerfectCamera pixelPerfect;
    private CinemachineCamera cinemachineCamera;

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
            return;

        EnsureBrainOnMainCamera();
        EnsurePixelPerfect();
        EnsureFollowCamera();
        LockLensToPixelSafeOrtho();
    }

    private void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    private void LateUpdate()
    {
        // Reforça snap na mesma grade do player depois de tudo (CM + PPC).
        SnapMainCameraToSpriteGrid();
    }

    private void EnsureBrainOnMainCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        // Sem MSAA / AA — pixel art fica borrosa com isso.
        mainCamera.allowMSAA = false;
        mainCamera.forceIntoRenderTexture = false;

        UniversalAdditionalCameraData urp = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urp != null)
        {
            urp.antialiasing = AntialiasingMode.None;
            urp.renderPostProcessing = false;
        }

        brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();

        // Player move no FixedUpdate (Rigidbody2D) → câmera no mesmo passo.
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.FixedUpdate;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    }

    private void EnsurePixelPerfect()
    {
        if (!usePixelPerfect || mainCamera == null)
            return;

        pixelPerfect = mainCamera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfect == null)
            pixelPerfect = mainCamera.gameObject.AddComponent<PixelPerfectCamera>();

        pixelPerfect.assetsPPU = AssetsPpu;
        pixelPerfect.refResolutionX = RefResolutionX;
        pixelPerfect.refResolutionY = RefResolutionY;
        pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
        pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
    }

    private void EnsureFollowCamera()
    {
        cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        GameObject cameraObject;

        if (cinemachineCamera == null)
        {
            cameraObject = new GameObject("CM_PlayerFollow");
            cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
        }
        else
        {
            cameraObject = cinemachineCamera.gameObject;
        }

        cinemachineCamera.Target.TrackingTarget = player;
        cinemachineCamera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        cinemachineCamera.Lens.OrthographicSize = GetPixelSafeOrtho(orthographicSize);

        CinemachineFollow follow = cameraObject.GetComponent<CinemachineFollow>();
        if (follow == null)
            follow = cameraObject.AddComponent<CinemachineFollow>();

        follow.FollowOffset = followOffset;
        follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        // Qualquer damping = lag subpixel = tremor no tilemap.
        follow.TrackerSettings.PositionDamping = Vector3.zero;

        // Extensão oficial CM ↔ Pixel Perfect.
        if (usePixelPerfect && cameraObject.GetComponent<CinemachinePixelPerfect>() == null)
            cameraObject.AddComponent<CinemachinePixelPerfect>();
    }

    private void LockLensToPixelSafeOrtho()
    {
        float safe = GetPixelSafeOrtho(orthographicSize);
        orthographicSize = safe;

        if (cinemachineCamera != null)
            cinemachineCamera.Lens.OrthographicSize = safe;

        if (mainCamera != null && (pixelPerfect == null || !usePixelPerfect))
            mainCamera.orthographicSize = safe;
    }

    private void OnCameraUpdated(CinemachineBrain updatedBrain)
    {
        if (brain != null && updatedBrain != brain)
            return;

        SnapMainCameraToSpriteGrid();
    }

    private void SnapMainCameraToSpriteGrid()
    {
        if (mainCamera == null || !mainCamera.orthographic)
            return;

        // IMPORTANTE: mesma grade do PlayerController (SpriteUnit).
        // NÃO usar UnitsPerScreenPixel aqui — isso misturava grades e causava tremor.
        Vector3 p = mainCamera.transform.position;
        p = PixelSnap2D.Snap(p, PixelSnap2D.SpriteUnit);
        mainCamera.transform.position = p;
    }

    /// <summary>
    /// Ortho size tal que a altura em pixels de arte (PPU) seja inteira.
    /// Evita blur de escala quebrada na vertical.
    /// </summary>
    private static float GetPixelSafeOrtho(float desired)
    {
        // altura mundo = ortho*2 → pixels arte = ortho*2*PPU deve ser inteiro.
        float pixels = desired * 2f * AssetsPpu;
        int rounded = Mathf.Max(AssetsPpu, Mathf.RoundToInt(pixels));
        // Prefere múltiplo par pra pivot estável.
        if (rounded % 2 != 0)
            rounded++;
        return rounded / (2f * AssetsPpu);
    }
}
