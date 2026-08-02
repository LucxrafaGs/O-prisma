using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Eco: silhueta preta do personagem (sprites reais), glitch seguindo,
/// abordagem → silêncio total → carga rápida com trovão.
/// </summary>
[DisallowMultipleComponent]
public class EchoApparition : MonoBehaviour
{
    public enum ApproachStyle
    {
        Walk = 0,
        Slide = 1,
        WalkThenSlide = 2
    }

    private PlayerAppearance appearance;
    private ParticleSystem aura;
    private Transform player;
    private ApproachStyle style;
    private PlayerController.Facing facing = PlayerController.Facing.Down;
    private float walkFrameDuration = 0.11f;
    private int animFrameIndex;
    private float animFrameTimer;
    private bool worldMuted;
    private bool finished;

    private const float WalkSpeed = 1.55f;
    private const float SlideSpeed = 1.1f;
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
        style = (ApproachStyle)Random.Range(0, 3);
        transform.localScale = CharacterWorldScale.Vector;

        appearance = gameObject.AddComponent<PlayerAppearance>();
        appearance.SetApplySavedAppearanceOnAwake(false);

        CharacterSpriteLibrary library = CharacterLibraryAccess.Get();
        Dictionary<CharacterLayerType, string> look = library != null
            ? CharacterRandomizer.CreateRandomNpcLook(library)
            : CharacterAppearanceData.Load();
        appearance.ApplyAppearance(look);
        TintAllBlack();

        SortingGroup group = GetComponent<SortingGroup>();
        if (group == null)
            group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = WorldDepth.ActorOrderMax;

        Vector2 stopPoint = PickStopPoint();
        Vector2 spawnPoint = PickSpawnPoint(stopPoint);
        transform.position = spawnPoint;

        Vector2 toStop = stopPoint - spawnPoint;
        facing = FacingFrom(toStop.sqrMagnitude > 0.01f ? toStop.normalized : Vector2.down);
        appearance.SetFrame(IdleIndex(facing));

        aura = CreateBlackAura(dense: true);
        RealityGlitchSystem glitch = RealityGlitchSystem.Instance
            ?? Object.FindAnyObjectByType<RealityGlitchSystem>();
        if (glitch == null)
        {
            GameObject host = new("RealityGlitchSystem");
            glitch = host.AddComponent<RealityGlitchSystem>();
        }

        RealityGlitchSystem.GlitchKind kind = Random.value < 0.5f
            ? RealityGlitchSystem.GlitchKind.GlassBubble
            : RealityGlitchSystem.GlitchKind.RealityVortex;
        glitch.BeginFollow(transform, kind, 0.72f);

        SoundsOfTheWorld audio = SoundsOfTheWorld.Instance ?? SoundsOfTheWorld.EnsureInScene();
        audio?.NotifyEchoSequenceStarted();

        StartCoroutine(RunSequence(stopPoint));
    }

    private void OnDestroy()
    {
        CleanupFx(restoreAudio: worldMuted && !finished);
    }

    private void CleanupFx(bool restoreAudio)
    {
        RealityGlitchSystem.Instance?.EndFollow();
        SoundsOfTheWorld audio = SoundsOfTheWorld.Instance;
        if (audio == null)
            return;

        audio.StopEchoWhisper();
        if (restoreAudio)
            audio.RestoreWorldAfterEcho(withThunder: false);
        audio.NotifyEchoSequenceEnded();
    }

    private void TintAllBlack()
    {
        Material mat = EchoSilhouetteFactory.GetUnlitMaterial();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].color = Color.black;
            renderers[i].material = mat;
        }
    }

    private Vector2 PickStopPoint()
    {
        Vector2 origin = player != null ? (Vector2)player.position : Vector2.zero;
        // Parado na frente do player, visível (pode ficar um pouco longe).
        Vector2 offset = Random.insideUnitCircle.normalized;
        if (offset.sqrMagnitude < 0.01f)
            offset = Vector2.down;
        float dist = Random.Range(1.6f, 2.9f);
        Vector2 point = origin + offset * dist;
        return ClampToCamera(point, margin: 0.55f);
    }

    private Vector2 PickSpawnPoint(Vector2 stopPoint)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return stopPoint + Vector2.left * 3f;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 camPos = cam.transform.position;

        // Spawn do lado oposto ao player em relação ao stop, na borda da visão.
        Vector2 away = player != null
            ? (stopPoint - (Vector2)player.position).normalized
            : Vector2.right;
        if (away.sqrMagnitude < 0.01f)
            away = Vector2.right;

        Vector2 spawn = stopPoint + away * Random.Range(2.8f, 4.2f);
        // Mantém dentro / quase na borda da câmera para o jogador ver.
        spawn.x = Mathf.Clamp(spawn.x, camPos.x - halfW + 0.2f, camPos.x + halfW - 0.2f);
        spawn.y = Mathf.Clamp(spawn.y, camPos.y - halfH + 0.2f, camPos.y + halfH - 0.2f);

        // Se ficou colado no stop, empurra para a borda.
        if (Vector2.Distance(spawn, stopPoint) < 1.2f)
        {
            int edge = Random.Range(0, 4);
            spawn = edge switch
            {
                0 => new Vector2(camPos.x - halfW + 0.35f, stopPoint.y),
                1 => new Vector2(camPos.x + halfW - 0.35f, stopPoint.y),
                2 => new Vector2(stopPoint.x, camPos.y - halfH + 0.35f),
                _ => new Vector2(stopPoint.x, camPos.y + halfH - 0.35f)
            };
        }

        return spawn;
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

    private ParticleSystem CreateBlackAura(bool dense)
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
        main.maxParticles = dense ? 160 : 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = dense ? 55f : 28f;

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
        // Abordagem: anda / desliza / anda e desliza, sussurro sobe com proximidade.
        yield return Approach(stopPoint);

        // Para na frente do player — silêncio total (música, chuva, sussurro…).
        appearance.SetFrame(IdleIndex(facing));
        SoundsOfTheWorld.Instance?.StopEchoWhisper();
        SoundsOfTheWorld.Instance?.MuteWorldForEcho();
        worldMuted = true;
        RealityGlitchSystem.Instance?.SetFollowIntensity(0.95f);

        float hold = Random.Range(1.1f, 2.1f);
        float held = 0f;
        while (held < hold)
        {
            held += Time.deltaTime;
            // Olha para o player enquanto parado.
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

        // Trovão + sons voltam + carga mega rápida.
        SoundsOfTheWorld.Instance?.RestoreWorldAfterEcho(withThunder: true);
        worldMuted = false;
        RealityGlitchSystem.Instance?.SetFollowIntensity(1f);
        BoostParticles();

        yield return ChargeAtPlayer();

        finished = true;
        CleanupFx(restoreAudio: false);
        Destroy(gameObject);
    }

    private IEnumerator Approach(Vector2 stopPoint)
    {
        Vector2 start = transform.position;
        float totalDist = Mathf.Max(0.01f, Vector2.Distance(start, stopPoint));
        float slideAfter = style == ApproachStyle.WalkThenSlide
            ? Random.Range(0.35f, 0.65f)
            : (style == ApproachStyle.Slide ? 0f : 2f);

        while (Vector2.Distance(transform.position, stopPoint) > 0.08f)
        {
            float progress = 1f - Vector2.Distance(transform.position, stopPoint) / totalDist;
            UpdateWhisperProximity();

            Vector2 to = stopPoint - (Vector2)transform.position;
            Vector2 dir = to.normalized;
            facing = FacingFrom(dir);

            bool sliding = style == ApproachStyle.Slide
                || (style == ApproachStyle.WalkThenSlide && progress >= slideAfter);

            float speed = sliding ? SlideSpeed : WalkSpeed;
            transform.position = Vector3.MoveTowards(
                transform.position, stopPoint, speed * Time.deltaTime);

            if (sliding)
                appearance.SetFrame(IdleIndex(facing));
            else
                TickWalkAnim();

            float distToPlayer = player != null
                ? Vector2.Distance(transform.position, player.position)
                : 3f;
            float intensity = Mathf.Lerp(0.55f, 0.95f, 1f - Mathf.Clamp01(distToPlayer / 5f));
            RealityGlitchSystem.Instance?.SetFollowIntensity(intensity);

            yield return null;
        }

        transform.position = stopPoint;
    }

    private IEnumerator ChargeAtPlayer()
    {
        Vector2 target = player != null ? (Vector2)player.position : (Vector2)transform.position;
        Vector2 dir = (target - (Vector2)transform.position);
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector2.down;
        dir.Normalize();
        facing = FacingFrom(dir);

        // Continua além do player.
        Vector2 end = target + dir * 5.5f;
        float maxTime = 1.4f;
        float t = 0f;
        while (t < maxTime && Vector2.Distance(transform.position, end) > 0.15f)
        {
            t += Time.deltaTime;
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
    }

    private void UpdateWhisperProximity()
    {
        SoundsOfTheWorld audio = SoundsOfTheWorld.Instance;
        if (audio == null || player == null)
            return;

        float dist = Vector2.Distance(transform.position, player.position);
        // Longe ~0.05, perto ~0.85
        float volume = Mathf.Lerp(0.82f, 0.06f, Mathf.Clamp01(dist / 6.5f));
        audio.SetEchoWhisperVolume(volume);
    }

    private void TickWalkAnim(bool fast = false)
    {
        float dur = fast ? walkFrameDuration * 0.45f : walkFrameDuration;
        animFrameTimer += Time.deltaTime;
        if (animFrameTimer < dur)
            return;

        animFrameTimer -= dur;
        animFrameIndex = (animFrameIndex + 1) % 6;
        appearance.SetFrame(WalkIndex(facing, animFrameIndex));
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

/// <summary>Materiais pretos gerados em runtime para o eco / partículas.</summary>
public static class EchoSilhouetteFactory
{
    private static Material unlitMaterial;
    private static Material particleMaterial;
    private static Texture2D particleTex;

    public static void ClearForDomainReload()
    {
        unlitMaterial = null;
        particleMaterial = null;
        particleTex = null;
    }

    public static Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
            return unlitMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        unlitMaterial = new Material(shader)
        {
            name = "EchoUnlit",
            color = Color.black,
            hideFlags = HideFlags.HideAndDontSave
        };
        return unlitMaterial;
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
