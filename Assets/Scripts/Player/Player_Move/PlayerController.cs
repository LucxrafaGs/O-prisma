// v3: movimento + aparência em camadas (PlayerAppearance)
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerAppearance))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public enum Facing
    {
        Down,
        Left,
        Right,
        Up
    }

    private enum MovementState
    {
        Idle,
        Walk,
        Run,
        ToeTapIdle
    }

    private static readonly float[] RunFrameDurations = { 0.08f, 0.055f, 0.125f, 0.08f, 0.055f, 0.125f };

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;

    [Header("Animation")]
    [SerializeField] private float walkFrameDuration = 0.135f;
    [SerializeField] private float idleFidgetDelay = 5f;
    [SerializeField] private float toeTapHoldDuration = 3f;

    private PlayerAppearance playerAppearance;
    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;

    private Facing facing = Facing.Down;
    private MovementState movementState = MovementState.Idle;

    private int animFrameIndex;
    private float animFrameTimer;

    private float idleTimer;
    private bool toeTapPlaying;
    private bool toeTapInLoopPhase;
    private int toeTapFrameIndex;
    private float toeTapFrameTimer;

    public Facing CurrentFacing => facing;

    /// <summary>True enquanto há input de movimento (passos / SFX).</summary>
    public bool IsMoving => ReadMovementInput().sqrMagnitude > 0.01f;

    /// <summary>True se está correndo (Shift) com movimento.</summary>
    public bool IsRunning => IsMoving && IsRunHeld();

    private ContactFilter2D solidFilter;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
    private readonly Collider2D[] overlapHits = new Collider2D[12];
    private const float CastSkin = 0.02f;
    private const float StopPadding = 0.08f;

    private void Awake()
    {
        CharacterWorldScale.Apply(transform);

        playerAppearance = GetComponent<PlayerAppearance>();
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Sem interpolate: com pixel snap evita blur ao andar no eixo Y (frente/costas).
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.linearVelocity = Vector2.zero;

        SpriteRenderer preview = GetComponent<SpriteRenderer>();
        if (preview != null)
            preview.enabled = false;

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
            bodyCollider.isTrigger = false;
        }

        // Não recria/altera o BoxCollider — usa exatamente o offset/size do modo de edição.
        Transform autoFoot = transform.Find(CharacterFootCollider.ChildName);
        if (autoFoot != null)
            Destroy(autoFoot.gameObject);

        solidFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            useDepth = false
        };
        solidFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

        if (GetComponent<CharacterDepthSort>() == null)
            gameObject.AddComponent<CharacterDepthSort>();

        if (GetComponent<PlayerFlashlight>() == null)
            gameObject.AddComponent<PlayerFlashlight>();

        SetIdleSprite();
    }

    private void Start()
    {
        // Garante transform e Rigidbody no mesmo lugar (AutoSyncTransforms está off).
        Physics2D.SyncTransforms();
        if (rb != null)
            rb.position = transform.position;

        if (playerAppearance != null && bodyCollider != null)
            playerAppearance.AlignVisualToCollider(bodyCollider);

        ResolveSolidOverlaps();
    }

    private void LateUpdate()
    {
        // Mantém o desenho do personagem colado no collider da edição (não mexe no BoxCollider).
        if (playerAppearance != null && bodyCollider != null)
            playerAppearance.AlignVisualToCollider(bodyCollider);
    }

    private void Update()
    {
        Vector2 input = ReadMovementInput();
        bool isMoving = input.sqrMagnitude > 0.01f;
        bool isRunning = isMoving && IsRunHeld();

        if (isMoving)
        {
            ResetIdleState();
            facing = GetFacingFromInput(input);
            movementState = isRunning ? MovementState.Run : MovementState.Walk;
            UpdateLocomotionAnimation();
        }
        else
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleFidgetDelay)
            {
                movementState = MovementState.ToeTapIdle;
                UpdateToeTapIdleAnimation();
            }
            else
            {
                StopToeTapAnimation();
                movementState = MovementState.Idle;
                animFrameIndex = 0;
                animFrameTimer = 0f;
                SetIdleSprite();
            }
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = Vector2.zero;

        Vector2 input = ReadMovementInput();
        if (input.sqrMagnitude <= 0.01f)
            return;

        float speed = IsRunHeld() ? runSpeed : moveSpeed;
        Vector2 delta = input.normalized * (speed * Time.fixedDeltaTime);

        // Eixos separados: desliza ao longo de paredes/fonte em vez de atravessar.
        Vector2 pos = rb.position;
        pos = MoveAndSlide(pos, new Vector2(delta.x, 0f));
        pos = MoveAndSlide(pos, new Vector2(0f, delta.y));
        rb.MovePosition(PixelSnap2D.Snap(pos, PixelSnap2D.SpriteUnit));
    }

    private Vector2 MoveAndSlide(Vector2 origin, Vector2 delta)
    {
        if (bodyCollider == null || delta.sqrMagnitude < 0.0000001f)
            return origin + delta;

        float distance = delta.magnitude;
        Vector2 direction = delta / distance;
        float castDistance = distance + CastSkin;

        int hits = bodyCollider.Cast(direction, solidFilter, castHits, castDistance);
        float allowed = distance;

        for (int i = 0; i < hits; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.collider.attachedRigidbody == rb)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - CastSkin - StopPadding));
        }

        return origin + direction * allowed;
    }

    public void ResolveSolidOverlaps()
    {
        if (bodyCollider == null || rb == null)
            return;

        Physics2D.SyncTransforms();

        int count = bodyCollider.Overlap(solidFilter, overlapHits);
        Vector2 push = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlapHits[i];
            if (other == null || other.isTrigger)
                continue;
            if (other.attachedRigidbody == rb)
                continue;
            if (other.transform == transform || other.transform.IsChildOf(transform))
                continue;

            ColliderDistance2D dist = bodyCollider.Distance(other);
            if (!dist.isOverlapped)
                continue;

            push += dist.normal * (-dist.distance + CastSkin);
        }

        if (push.sqrMagnitude > 0.000001f)
            rb.position += push;
    }

    private void UpdateToeTapIdleAnimation()
    {
        playerAppearance.SetUsePage4Sprites(true);

        if (!toeTapPlaying)
        {
            toeTapFrameIndex = CharacterP4Animations.ToeTapHoldFrameIndex;
            toeTapFrameTimer = 0f;
            toeTapInLoopPhase = false;
            toeTapPlaying = true;
            SetToeTapSprite();
        }

        toeTapFrameTimer += Time.deltaTime;

        if (!toeTapInLoopPhase)
        {
            if (toeTapFrameTimer < toeTapHoldDuration)
                return;

            toeTapInLoopPhase = true;
            toeTapFrameIndex = CharacterP4Animations.ToeTapLoopStartFrameIndex;
            toeTapFrameTimer = 0f;
            SetToeTapSprite();
            return;
        }

        while (toeTapFrameTimer >= CharacterP4Animations.ToeTapFrameDuration)
        {
            toeTapFrameTimer -= CharacterP4Animations.ToeTapFrameDuration;
            toeTapFrameIndex = toeTapFrameIndex == 1 ? 2 : 1;
            SetToeTapSprite();
        }
    }

    private void SetToeTapSprite()
    {
        int spriteIndex = CharacterP4Animations.GetToeTapSpriteIndex(facing, toeTapFrameIndex);
        SetSprite(spriteIndex);
    }

    private void ResetIdleState()
    {
        idleTimer = 0f;
        StopToeTapAnimation();
    }

    private void StopToeTapAnimation()
    {
        toeTapFrameIndex = 0;
        toeTapFrameTimer = 0f;
        toeTapInLoopPhase = false;
        toeTapPlaying = false;

        if (playerAppearance != null)
            playerAppearance.SetUsePage4Sprites(false);
    }

    private void UpdateLocomotionAnimation()
    {
        float frameDuration = movementState == MovementState.Run
            ? RunFrameDurations[animFrameIndex]
            : walkFrameDuration;

        animFrameTimer += Time.deltaTime;
        if (animFrameTimer < frameDuration)
            return;

        animFrameTimer -= frameDuration;
        animFrameIndex = (animFrameIndex + 1) % 6;

        int spriteIndex = movementState == MovementState.Run
            ? GetRunSpriteIndex(facing, animFrameIndex)
            : GetWalkSpriteIndex(facing, animFrameIndex);

        SetSprite(spriteIndex);
    }

    private static bool IsRunHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private static Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;

        return input;
    }

    private static Facing GetFacingFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            return input.y > 0f ? Facing.Up : Facing.Down;

        return input.x > 0f ? Facing.Right : Facing.Left;
    }

    private void SetIdleSprite()
    {
        SetSprite(GetIdleSpriteIndex(facing));
    }

    private static int GetIdleSpriteIndex(Facing direction)
    {
        switch (direction)
        {
            case Facing.Down: return 0;
            case Facing.Left: return 24;
            case Facing.Right: return 16;
            case Facing.Up: return 8;
            default: return 0;
        }
    }

    private static int GetWalkSpriteIndex(Facing direction, int frame)
    {
        switch (direction)
        {
            case Facing.Down: return 32 + frame;
            case Facing.Left: return 56 + frame;
            case Facing.Right: return 48 + frame;
            case Facing.Up: return 40 + frame;
            default: return 32 + frame;
        }
    }

    private static int GetRunSpriteIndex(Facing direction, int frame)
    {
        int rowStart;

        switch (direction)
        {
            case Facing.Down: rowStart = 32; break;
            case Facing.Left: rowStart = 56; break;
            case Facing.Right: rowStart = 48; break;
            case Facing.Up: rowStart = 40; break;
            default: rowStart = 32; break;
        }

        int[] runColumns = { 0, 1, 6, 3, 4, 7 };
        return rowStart + runColumns[frame];
    }

    private void SetSprite(int spriteIndex)
    {
        if (playerAppearance == null)
            return;

        playerAppearance.SetFrame(spriteIndex);
    }
}
