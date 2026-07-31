using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerAppearance))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class NpcController : MonoBehaviour
{
    public enum Facing
    {
        Down,
        Left,
        Right,
        Up
    }

    [SerializeField] private float moveSpeed = 1.35f;
    [SerializeField] private float walkFrameDuration = 0.14f;
    [SerializeField] private Vector2 wanderRadius = new(4.5f, 3.2f);
    [SerializeField] private float minIdleTime = 1.2f;
    [SerializeField] private float maxIdleTime = 3.5f;
    [SerializeField] private float minWalkTime = 1.5f;
    [SerializeField] private float maxWalkTime = 3.8f;

    private PlayerAppearance appearance;
    private Rigidbody2D body;
    private Vector2 homePosition;
    private Facing facing = Facing.Down;
    private Vector2 moveDirection;
    private bool isWalking;
    private bool movementLocked;
    private float stateTimer;
    private float bumpCooldown;
    private int animFrameIndex;
    private float animFrameTimer;

    public string DisplayName { get; private set; } = "Morador";
    public string[] Lines { get; private set; } = { "..." };

    public void Configure(string displayName, string[] lines, Dictionary<CharacterLayerType, string> look)
    {
        DisplayName = displayName;
        Lines = lines != null && lines.Length > 0 ? lines : new[] { "Oi." };

        appearance = GetComponent<PlayerAppearance>();
        appearance.SetApplySavedAppearanceOnAwake(false);
        appearance.ApplyAppearance(look);
        appearance.SetFrame(GetIdleSpriteIndex(facing));
        CharacterLitMaterial.ApplyToHierarchy(transform);
        ApplyFootCollider();
    }

    private void Awake()
    {
        appearance = GetComponent<PlayerAppearance>();
        body = GetComponent<Rigidbody2D>();
        if (GetComponent<CharacterDepthSort>() == null)
            gameObject.AddComponent<CharacterDepthSort>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.mass = 40f;
        body.linearDamping = 12f;
        body.angularDamping = 0.05f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Sem interpolate: evita shimmer quando a câmera trava em pixels.
        body.interpolation = RigidbodyInterpolation2D.None;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        EnsureInteractionHitbox();

        homePosition = body.position;
        PickIdle();
        ApplyFootCollider();
    }

    private void ApplyFootCollider()
    {
        if (appearance == null)
            appearance = GetComponent<PlayerAppearance>();
        CharacterFootCollider.Apply(
            transform,
            appearance != null ? appearance.BodyRenderer : null);
    }

    private void LateUpdate()
    {
        if (appearance == null)
            return;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null && box.enabled)
            appearance.AlignVisualToCollider(box);
    }

    private void EnsureInteractionHitbox()
    {
        Transform existing = transform.Find("Interaction");
        GameObject hitbox;
        if (existing == null)
        {
            hitbox = new GameObject("Interaction");
            hitbox.transform.SetParent(transform, false);
            hitbox.layer = gameObject.layer;
        }
        else
        {
            hitbox = existing.gameObject;
        }

        hitbox.transform.localPosition = Vector3.zero;

        CircleCollider2D circle = hitbox.GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = hitbox.AddComponent<CircleCollider2D>();

        circle.isTrigger = true;
        // Clique no corpo; física sólida fica no box dos pés.
        circle.radius = 0.55f;
        circle.offset = new Vector2(0.125f, 0.45f);
    }

    private void Start()
    {
        if (appearance != null)
            appearance.SetFrame(GetIdleSpriteIndex(facing));
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked)
        {
            isWalking = false;
            body.linearVelocity = Vector2.zero;
            animFrameIndex = 0;
            animFrameTimer = 0f;
            appearance?.SetFrame(GetIdleSpriteIndex(facing));
        }
    }

    private void Update()
    {
        if (bumpCooldown > 0f)
            bumpCooldown -= Time.deltaTime;

        if (movementLocked)
            return;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            if (isWalking)
                PickIdle();
            else
                PickWalk();
        }

        if (isWalking)
            UpdateWalkAnimation();
    }

    private void FixedUpdate()
    {
        if (movementLocked || !isWalking)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 offset = body.position - homePosition;
        if (Mathf.Abs(offset.x) > wanderRadius.x || Mathf.Abs(offset.y) > wanderRadius.y)
        {
            moveDirection = (homePosition - body.position).normalized;
            if (moveDirection.sqrMagnitude < 0.01f)
                moveDirection = Random.insideUnitCircle.normalized;
            facing = GetFacingFromInput(moveDirection);
        }

        body.linearVelocity = moveDirection * moveSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleBump(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (bumpCooldown > 0f || !isWalking || movementLocked)
            return;

        HandleBump(collision);
    }

    private void HandleBump(Collision2D collision)
    {
        if (!isWalking || movementLocked || bumpCooldown > 0f)
            return;

        // Desvia ao encostar em jogador, outro NPC ou obstáculo sólido.
        if (collision.collider.isTrigger)
            return;

        Vector2 normal = collision.GetContact(0).normal;
        Vector2 bounce = Vector2.Reflect(moveDirection, normal);
        if (bounce.sqrMagnitude < 0.01f)
            bounce = -moveDirection;

        if (bounce.sqrMagnitude < 0.01f)
            bounce = Random.insideUnitCircle;

        moveDirection = bounce.normalized;
        facing = GetFacingFromInput(moveDirection);
        bumpCooldown = 0.35f;
    }

    private void PickIdle()
    {
        isWalking = false;
        body.linearVelocity = Vector2.zero;
        stateTimer = Random.Range(minIdleTime, maxIdleTime);
        animFrameIndex = 0;
        animFrameTimer = 0f;
        appearance?.SetFrame(GetIdleSpriteIndex(facing));
    }

    private void PickWalk()
    {
        isWalking = true;
        stateTimer = Random.Range(minWalkTime, maxWalkTime);
        moveDirection = Random.insideUnitCircle.normalized;
        if (moveDirection.sqrMagnitude < 0.01f)
            moveDirection = Vector2.down;
        facing = GetFacingFromInput(moveDirection);
        animFrameIndex = 0;
        animFrameTimer = 0f;
    }

    private void UpdateWalkAnimation()
    {
        animFrameTimer += Time.deltaTime;
        if (animFrameTimer < walkFrameDuration)
            return;

        animFrameTimer -= walkFrameDuration;
        animFrameIndex = (animFrameIndex + 1) % 6;
        appearance?.SetFrame(GetWalkSpriteIndex(facing, animFrameIndex));
    }

    private static Facing GetFacingFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            return input.y > 0f ? Facing.Up : Facing.Down;

        return input.x > 0f ? Facing.Right : Facing.Left;
    }

    private static int GetIdleSpriteIndex(Facing direction)
    {
        return direction switch
        {
            Facing.Left => 24,
            Facing.Right => 16,
            Facing.Up => 8,
            _ => 0
        };
    }

    private static int GetWalkSpriteIndex(Facing direction, int frame)
    {
        return direction switch
        {
            Facing.Left => 56 + frame,
            Facing.Right => 48 + frame,
            Facing.Up => 40 + frame,
            _ => 32 + frame
        };
    }
}
