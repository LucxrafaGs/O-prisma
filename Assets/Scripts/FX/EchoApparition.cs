using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Silhueta preta “bugada” que aparece perto do player (eco da realidade).
/// Comportamentos: jitter, surge e some, ou atravessa a tela correndo.
/// </summary>
[DisallowMultipleComponent]
public class EchoApparition : MonoBehaviour
{
    public enum Behavior
    {
        GlitchJitter = 0,
        WakeNearPlayer = 1,
        DashAcross = 2
    }

    private SpriteRenderer body;
    private ParticleSystem aura;
    private Behavior behavior;
    private Transform player;
    private float life;
    private Vector2 dashVelocity;
    private float glitchTimer;
    private Vector2 home;

    public static EchoApparition Spawn(Transform player, Behavior behavior, float lifetime)
    {
        GameObject go = new("EchoApparition");
        EchoApparition echo = go.AddComponent<EchoApparition>();
        echo.Setup(player, behavior, lifetime);
        return echo;
    }

    private void Setup(Transform playerTransform, Behavior chosen, float lifetime)
    {
        player = playerTransform;
        behavior = chosen;
        life = lifetime;
        home = player != null
            ? (Vector2)player.position + Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.8f)
            : Vector2.zero;

        if (behavior == Behavior.DashAcross && Camera.main != null)
        {
            Camera cam = Camera.main;
            float halfW = cam.orthographicSize * cam.aspect + 1.5f;
            float y = player != null ? player.position.y + Random.Range(-1.2f, 1.2f) : cam.transform.position.y;
            bool fromLeft = Random.value < 0.5f;
            home = new Vector2(cam.transform.position.x + (fromLeft ? -halfW : halfW), y);
            dashVelocity = new Vector2(fromLeft ? Random.Range(6f, 10f) : Random.Range(-10f, -6f), Random.Range(-0.4f, 0.4f));
        }

        transform.position = home;
        transform.localScale = CharacterWorldScale.Vector;

        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = EchoSilhouetteFactory.GetSprite();
        body.color = Color.black;
        body.sortingLayerID = 0;
        body.sortingOrder = WorldDepth.ActorOrderMax;
        body.material = EchoSilhouetteFactory.GetUnlitMaterial();

        if (GetComponent<SortingGroup>() == null)
            gameObject.AddComponent<SortingGroup>().sortingOrder = WorldDepth.ActorOrderMax;

        aura = CreateBlackAura();
        WorldAudioEvents.NotifyEchoAppeared();
        StartCoroutine(RunBehavior());
    }

    private ParticleSystem CreateBlackAura()
    {
        GameObject go = new("EchoAura");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(0f, 0f, 0f, 0.85f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new();
        g.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = EchoSilhouetteFactory.GetParticleMaterial();
        renderer.sortingOrder = WorldDepth.ActorOrderMax + 1;

        return ps;
    }

    private IEnumerator RunBehavior()
    {
        float end = Time.time + life;
        switch (behavior)
        {
            case Behavior.GlitchJitter:
                yield return GlitchJitterUntil(end);
                break;
            case Behavior.WakeNearPlayer:
                yield return WakeNearPlayerUntil(end);
                break;
            case Behavior.DashAcross:
                yield return DashAcrossUntil(end);
                break;
        }

        Destroy(gameObject);
    }

    private IEnumerator GlitchJitterUntil(float end)
    {
        while (Time.time < end)
        {
            glitchTimer -= Time.deltaTime;
            if (glitchTimer <= 0f)
            {
                glitchTimer = Random.Range(0.06f, 0.18f);
                Vector2 offset = new(Random.Range(-0.55f, 0.55f), Random.Range(-0.25f, 0.35f));
                transform.position = home + offset;
                if (body != null)
                    body.flipX = Random.value < 0.5f;
            }

            if (Random.value < 0.04f)
                transform.position = home + Random.insideUnitCircle * 0.15f;

            yield return null;
        }
    }

    private IEnumerator WakeNearPlayerUntil(float end)
    {
        // Surge “acordando” perto do player.
        float t = 0f;
        Vector3 start = transform.position + new Vector3(0f, 0.35f, 0f);
        Vector3 target = home;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / 0.35f);
            if (body != null)
            {
                Color c = body.color;
                c.a = Mathf.Clamp01(t / 0.35f);
                body.color = c;
            }

            yield return null;
        }

        while (Time.time < end - 0.4f)
        {
            if (Random.value < 0.08f)
            {
                transform.position = home + new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.15f));
                if (body != null)
                    body.flipX = !body.flipX;
            }

            yield return null;
        }

        float fade = 0f;
        Color baseColor = body != null ? body.color : Color.black;
        while (fade < 0.4f)
        {
            fade += Time.deltaTime;
            if (body != null)
            {
                Color c = baseColor;
                c.a = 1f - fade / 0.4f;
                body.color = c;
            }

            yield return null;
        }
    }

    private IEnumerator DashAcrossUntil(float end)
    {
        while (Time.time < end)
        {
            transform.position += (Vector3)(dashVelocity * Time.deltaTime);
            if (body != null)
                body.flipX = dashVelocity.x < 0f;

            if (Random.value < 0.12f)
                transform.position += new Vector3(0f, Random.Range(-0.15f, 0.15f), 0f);

            // Saiu bem longe da câmera → encerra.
            if (Camera.main != null)
            {
                float dx = Mathf.Abs(transform.position.x - Camera.main.transform.position.x);
                float limit = Camera.main.orthographicSize * Camera.main.aspect + 2.5f;
                if (dx > limit)
                    yield break;
            }

            yield return null;
        }
    }
}

/// <summary>Sprites/materiais pretos gerados em runtime para o eco.</summary>
public static class EchoSilhouetteFactory
{
    private static Sprite silhouette;
    private static Material unlitMaterial;
    private static Material particleMaterial;
    private static Texture2D particleTex;

    public static void ClearForDomainReload()
    {
        silhouette = null;
        unlitMaterial = null;
        particleMaterial = null;
        particleTex = null;
    }

    public static Sprite GetSprite()
    {
        if (silhouette != null)
            return silhouette;

        const int w = 16;
        const int h = 28;
        Texture2D tex = new(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "EchoSilhouetteTex"
        };

        Color clear = new(0f, 0f, 0f, 0f);
        Color solid = Color.black;
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        // Silhueta humana simples (pés → cabeça).
        FillRect(pixels, w, h, 6, 0, 4, 3, solid);   // pés
        FillRect(pixels, w, h, 5, 3, 6, 8, solid);   // pernas/corpo
        FillRect(pixels, w, h, 4, 11, 8, 7, solid);  // tronco
        FillRect(pixels, w, h, 2, 14, 3, 3, solid);  // braço L
        FillRect(pixels, w, h, 11, 14, 3, 3, solid); // braço R
        FillRect(pixels, w, h, 5, 18, 6, 6, solid);  // cabeça
        FillRect(pixels, w, h, 6, 24, 4, 3, solid);  // topo cabeça

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        silhouette = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        silhouette.name = "EchoSilhouette";
        return silhouette;
    }

    private static void FillRect(Color[] pixels, int w, int h, int x, int y, int rw, int rh, Color color)
    {
        for (int py = y; py < y + rh && py < h; py++)
        for (int px = x; px < x + rw && px < w; px++)
        {
            if (px < 0 || py < 0)
                continue;
            pixels[py * w + px] = color;
        }
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
