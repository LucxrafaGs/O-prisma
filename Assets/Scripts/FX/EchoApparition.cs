using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Eco: NPC comum todo preto. Anda de fora da tela até o jogador, para, e carrega.
/// Áudio do mundo permanece normal (sem mute).
/// </summary>
[DisallowMultipleComponent]
public class EchoApparition : MonoBehaviour
{
    private PlayerAppearance appearance;
    private ParticleSystem aura;
    private Transform player;
    private PlayerController.Facing facing = PlayerController.Facing.Down;
    private float walkFrameDuration = 0.12f;
    private int animFrameIndex;
    private float animFrameTimer;

    private const float WalkSpeed = 1.55f;
    private const float ChargeSpeed = 22f;

    public static EchoApparition Spawn(Transform playerTransform)
    {
        GameObject go = new("EchoApparition");
        EchoApparition echo = go.AddComponent<EchoApparition>();
        echo.Setup(playerTransform);
        return echo;
    }

    private void Setup(Transform playerTransform)
    {
        player = playerTransform;
        transform.localScale = CharacterWorldScale.Vector;

        appearance = gameObject.AddComponent<PlayerAppearance>();
        appearance.SetApplySavedAppearanceOnAwake(false);

        CharacterSpriteLibrary library = CharacterLibraryAccess.Get();
        Dictionary<CharacterLayerType, string> look = library != null
            ? CharacterRandomizer.CreateRandomNpcLook(library)
            : CharacterAppearanceData.Load();
        appearance.ApplyAppearance(look);
        CharacterLitMaterial.ApplyToHierarchy(transform);
        PaintNpcBlack();

        Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.None;

        if (GetComponent<CharacterDepthSort>() == null)
            gameObject.AddComponent<CharacterDepthSort>();

        SortingGroup group = GetComponent<SortingGroup>();
        if (group == null)
            group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = WorldDepth.ActorOrderMax;

        Vector2 stopPoint = PickStopPoint();
        transform.position = PickOffscreenSpawn(stopPoint);

        Vector2 toStop = stopPoint - (Vector2)transform.position;
        facing = FacingFrom(toStop.sqrMagnitude > 0.01f ? toStop.normalized : Vector2.down);
        appearance.SetFrame(IdleIndex(facing));

        aura = CreateBlackAura();
        WorldAudioEvents.NotifyEchoAppeared();

        StartCoroutine(RunSequence(stopPoint));
    }

    private void PaintNpcBlack()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = Color.black;
    }

    private Vector2 PickStopPoint()
    {
        Vector2 origin = player != null ? (Vector2)player.position : Vector2.zero;
        Vector2 offset = Random.insideUnitCircle.normalized;
        if (offset.sqrMagnitude < 0.01f)
            offset = Vector2.down;
        float dist = Random.Range(1.6f, 2.6f);
        return ClampToCamera(origin + offset * dist, margin: 0.7f);
    }

    /// <summary>Nasce fora da tela, alinhado com o ponto de parada.</summary>
    private Vector2 PickOffscreenSpawn(Vector2 stopPoint)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return stopPoint + Vector2.left * 5f;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 camPos = cam.transform.position;
        float margin = 1.4f;

        Vector2 fromPlayer = player != null
            ? (stopPoint - (Vector2)player.position).normalized
            : Vector2.right;
        if (fromPlayer.sqrMagnitude < 0.01f)
            fromPlayer = Vector2.right;

        // Preferir a borda mais alinhada à direção (vindo de fora).
        Vector2 candidate = stopPoint + fromPlayer * (Mathf.Max(halfW, halfH) + margin);
        int edge = Mathf.Abs(fromPlayer.x) >= Mathf.Abs(fromPlayer.y)
            ? (fromPlayer.x >= 0f ? 1 : 0)
            : (fromPlayer.y >= 0f ? 3 : 2);

        return edge switch
        {
            0 => new Vector2(camPos.x - halfW - margin, Mathf.Clamp(stopPoint.y, camPos.y - halfH, camPos.y + halfH)),
            1 => new Vector2(camPos.x + halfW + margin, Mathf.Clamp(stopPoint.y, camPos.y - halfH, camPos.y + halfH)),
            2 => new Vector2(Mathf.Clamp(stopPoint.x, camPos.x - halfW, camPos.x + halfW), camPos.y - halfH - margin),
            _ => new Vector2(Mathf.Clamp(stopPoint.x, camPos.x - halfW, camPos.x + halfW), camPos.y + halfH + margin)
        };
    }

    private static Vector2 ClampToCamera(Vector2 world, float margin)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return world;

        float halfH = cam.orthographicSize - margin;
        float halfW = halfH * cam.aspect;
        Vector2 c = cam.transform.position;
        return new Vector2(
            Mathf.Clamp(world.x, c.x - halfW, c.x + halfW),
            Mathf.Clamp(world.y, c.y - halfH, c.y + halfH));
    }

    private ParticleSystem CreateBlackAura()
    {
        GameObject go = new("EchoAura");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.14f);
        main.startColor = new Color(0f, 0f, 0f, 0.9f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 55f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.42f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new();
        g.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.55f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = EchoSilhouetteFactory.GetParticleMaterial();
        renderer.sortingOrder = WorldDepth.ActorOrderMax + 1;

        return ps;
    }

    private IEnumerator RunSequence(Vector2 stopPoint)
    {
        SoundsOfTheWorld audio = SoundsOfTheWorld.Instance;

        // Anda normalmente até parar na frente do jogador; sussurro sobe um pouco.
        yield return Approach(stopPoint, audio, whisperFrom: 0.45f, whisperTo: 0.7f);

        appearance.SetFrame(IdleIndex(facing));
        float hold = Random.Range(0.7f, 1.35f);
        float held = 0f;
        while (held < hold)
        {
            held += Time.deltaTime;
            if (player != null)
            {
                Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    facing = FacingFrom(toPlayer);
                    appearance.SetFrame(IdleIndex(facing));
                }
            }

            yield return null;
        }

        BoostParticles();
        // Corre com tudo enquanto o sussurro sobe ao máximo.
        yield return ChargeAtPlayer(audio, whisperFrom: 0.7f, whisperTo: 1f);

        Destroy(gameObject);
    }

    private IEnumerator Approach(Vector2 stopPoint, SoundsOfTheWorld audio, float whisperFrom, float whisperTo)
    {
        Vector2 start = transform.position;
        float total = Mathf.Max(0.01f, Vector2.Distance(start, stopPoint));

        while (Vector2.Distance(transform.position, stopPoint) > 0.08f)
        {
            float progress = 1f - Vector2.Distance(transform.position, stopPoint) / total;
            audio?.SetEncounterWhisperVolume(Mathf.Lerp(whisperFrom, whisperTo, progress));

            Vector2 to = stopPoint - (Vector2)transform.position;
            facing = FacingFrom(to.normalized);
            transform.position = Vector3.MoveTowards(
                transform.position, stopPoint, WalkSpeed * Time.deltaTime);
            TickWalkAnim(fast: false);
            yield return null;
        }

        transform.position = stopPoint;
        audio?.SetEncounterWhisperVolume(whisperTo);
    }

    private IEnumerator ChargeAtPlayer(SoundsOfTheWorld audio, float whisperFrom, float whisperTo)
    {
        Vector2 target = player != null ? (Vector2)player.position : (Vector2)transform.position;
        Vector2 dir = target - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector2.down;
        dir.Normalize();
        facing = FacingFrom(dir);

        Vector2 end = target + dir * 5.5f;
        float maxTime = 1.4f;
        float t = 0f;
        while (t < maxTime && Vector2.Distance(transform.position, end) > 0.15f)
        {
            t += Time.deltaTime;
            audio?.SetEncounterWhisperVolume(Mathf.Lerp(whisperFrom, whisperTo, t / maxTime));
            transform.position = Vector3.MoveTowards(
                transform.position, end, ChargeSpeed * Time.deltaTime);
            TickWalkAnim(fast: true);

            if (Camera.main != null)
            {
                float dx = Mathf.Abs(transform.position.x - Camera.main.transform.position.x);
                float dy = Mathf.Abs(transform.position.y - Camera.main.transform.position.y);
                float limX = Camera.main.orthographicSize * Camera.main.aspect + 2.5f;
                float limY = Camera.main.orthographicSize + 2.5f;
                if (dx > limX || dy > limY)
                    yield break;
            }

            yield return null;
        }

        audio?.SetEncounterWhisperVolume(whisperTo);
    }

    private void TickWalkAnim(bool fast = false)
    {
        float dur = fast ? walkFrameDuration * 0.55f : walkFrameDuration;
        animFrameTimer += Time.deltaTime;
        if (animFrameTimer < dur)
            return;

        animFrameTimer -= dur;
        animFrameIndex = (animFrameIndex + 1) % 6;
        appearance.SetFrame(WalkIndex(facing, animFrameIndex));
        PaintNpcBlack();
    }

    private void BoostParticles()
    {
        if (aura == null)
            return;
        var emission = aura.emission;
        emission.rateOverTime = 110f;
        var main = aura.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
    }

    private static PlayerController.Facing FacingFrom(Vector2 input)
    {
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            return input.y > 0f ? PlayerController.Facing.Up : PlayerController.Facing.Down;
        return input.x > 0f ? PlayerController.Facing.Right : PlayerController.Facing.Left;
    }

    private static int IdleIndex(PlayerController.Facing direction)
    {
        return direction switch
        {
            PlayerController.Facing.Left => 24,
            PlayerController.Facing.Right => 16,
            PlayerController.Facing.Up => 8,
            _ => 0
        };
    }

    private static int WalkIndex(PlayerController.Facing direction, int frame)
    {
        return direction switch
        {
            PlayerController.Facing.Left => 56 + frame,
            PlayerController.Facing.Right => 48 + frame,
            PlayerController.Facing.Up => 40 + frame,
            _ => 32 + frame
        };
    }
}

/// <summary>Material de partículas pretas do eco (corpo usa sprites lit do NPC).</summary>
public static class EchoSilhouetteFactory
{
    private static Material particleMaterial;
    private static Texture2D particleTex;

    public static void ClearForDomainReload()
    {
        particleMaterial = null;
        particleTex = null;
    }

    public static Material GetParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        if (particleTex == null)
        {
            particleTex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "EchoParticleTex"
            };
            Color[] px = new Color[16];
            for (int i = 0; i < px.Length; i++)
                px[i] = Color.black;
            particleTex.SetPixels(px);
            particleTex.Apply(false, true);
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        particleMaterial = new Material(shader)
        {
            name = "EchoParticleMat",
            hideFlags = HideFlags.HideAndDontSave
        };
        particleMaterial.mainTexture = particleTex;
        return particleMaterial;
    }
}
