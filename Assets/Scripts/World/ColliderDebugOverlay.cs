using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// Contornos dos Collider2D na Game view (F3).
/// Desenha em espaço de tela via WorldToScreenPoint — mesmo referencial do gizmo da Scene.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ColliderDebugOverlay : MonoBehaviour
{
    public static ColliderDebugOverlay Instance { get; private set; }
    public static bool Enabled { get; private set; }

    private static readonly Color PlayerColor = new(0.15f, 1f, 0.3f, 1f);
    private static readonly Color FonteColor = new(1f, 0.9f, 0.1f, 1f);
    private static readonly Color TilemapColor = new(1f, 0.3f, 0.15f, 1f);
    private static readonly Color DefaultColor = new(0.35f, 0.8f, 1f, 0.95f);
    private static readonly Color DisabledColor = new(0.55f, 0.55f, 0.55f, 0.5f);
    private static readonly Color PivotColor = new(1f, 0.2f, 1f, 1f);

    private Material lineMaterial;
    private readonly List<Vector3> screenBuffer = new(64);
    private GUIStyle labelStyle;
    private string statusLabel = "Colliders: OFF (F3)";
    private string playerDiag = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Domain Reload desligado: limpa overlays velhos pra não ficar código/desenho antigo.
        ColliderDebugOverlay[] existing =
            Object.FindObjectsByType<ColliderDebugOverlay>(FindObjectsInactive.Include);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Object.Destroy(existing[i].gameObject);
        }

        Instance = null;

        GameObject go = new GameObject("ColliderDebugOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<ColliderDebugOverlay>();
    }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (Instance != null)
            Instance.statusLabel = enabled ? "Colliders: ON (F3)" : "Colliders: OFF (F3)";
    }

    public static void Toggle() => SetEnabled(!Enabled);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateLineMaterial();
    }

    private void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    private void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
            Toggle();
    }

    private void OnGUI()
    {
        if (!Enabled)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
        }

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.Box(new Rect(12f, 12f, 700f, 78f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(20f, 18f, 680f, 22f), statusLabel + "  |  Magenta=pivot  Ciano=sprite Body", labelStyle);
        GUI.Label(new Rect(20f, 40f, 680f, 22f), "Verde=Player | Amarelo=Fonte | Vermelho=Paredes | Azul=outros", labelStyle);
        if (!string.IsNullOrEmpty(playerDiag))
            GUI.Label(new Rect(20f, 62f, 680f, 22f), playerDiag, labelStyle);
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!Enabled || camera == null || camera.cameraType != CameraType.Game)
            return;

        if (lineMaterial == null)
            CreateLineMaterial();
        if (lineMaterial == null)
            return;

        lineMaterial.SetPass(0);

        // Espaço de pixel da câmera (Y sobe). WorldToScreenPoint usa o mesmo.
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, camera.pixelWidth, 0f, camera.pixelHeight);

        playerDiag = string.Empty;

        Collider2D[] colliders = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null)
                continue;
            DrawCollider(camera, col, ResolveColor(col));
        }

        DrawPlayerPivot(camera);
        GL.PopMatrix();
    }

    private void CreateLineMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private static Color ResolveColor(Collider2D col)
    {
        if (!col.enabled || !col.gameObject.activeInHierarchy)
            return DisabledColor;

        Transform t = col.transform;
        Transform root = t;
        while (root.parent != null)
            root = root.parent;

        if (root.name == "Player")
            return PlayerColor;
        if (t.name == "Fonte")
            return FonteColor;
        if (col is TilemapCollider2D || t.name == "Paredes")
            return TilemapColor;
        return DefaultColor;
    }

    private void DrawCollider(Camera camera, Collider2D col, Color color)
    {
        switch (col)
        {
            case BoxCollider2D box:
                DrawBoxLikeEditor(camera, box, color);
                if (box.transform.root.name == "Player" || box.gameObject.name == "Player")
                    UpdatePlayerDiag(box);
                break;
            case CircleCollider2D circle:
                DrawCircle(camera, circle, color);
                break;
            case PolygonCollider2D poly:
                DrawPolygon(camera, poly, color);
                break;
            case TilemapCollider2D:
                DrawWorldRect(camera, col.bounds, color);
                DrawTilemapCells(camera, col.GetComponent<Tilemap>(), color);
                break;
            default:
                DrawWorldRect(camera, col.bounds, color);
                break;
        }
    }

    /// <summary>
    /// Mesma matemática do gizmo da Scene: TransformPoint(offset ± size/2).
    /// </summary>
    private void DrawBoxLikeEditor(Camera camera, BoxCollider2D box, Color color)
    {
        Vector2 half = box.size * 0.5f;
        Vector2 o = box.offset;
        Vector3[] world =
        {
            box.transform.TransformPoint(o + new Vector2(-half.x, -half.y)),
            box.transform.TransformPoint(o + new Vector2(half.x, -half.y)),
            box.transform.TransformPoint(o + new Vector2(half.x, half.y)),
            box.transform.TransformPoint(o + new Vector2(-half.x, half.y))
        };

        screenBuffer.Clear();
        for (int i = 0; i < 4; i++)
        {
            if (!TryWorldToScreen(camera, world[i], out Vector3 s))
                return;
            screenBuffer.Add(s);
        }

        DrawScreenLoop(screenBuffer, color);
    }

    private void DrawCircle(Camera camera, CircleCollider2D circle, Color color)
    {
        Vector3 center = circle.transform.TransformPoint(circle.offset);
        Vector3 lossy = circle.transform.lossyScale;
        float radius = circle.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));

        screenBuffer.Clear();
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 world = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            if (!TryWorldToScreen(camera, world, out Vector3 s))
                return;
            screenBuffer.Add(s);
        }

        DrawScreenLoop(screenBuffer, color);
    }

    private void DrawPolygon(Camera camera, PolygonCollider2D poly, Color color)
    {
        for (int p = 0; p < poly.pathCount; p++)
        {
            Vector2[] path = poly.GetPath(p);
            screenBuffer.Clear();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 world = poly.transform.TransformPoint(path[i] + poly.offset);
                if (!TryWorldToScreen(camera, world, out Vector3 s))
                    return;
                screenBuffer.Add(s);
            }

            DrawScreenLoop(screenBuffer, color);
        }
    }

    private void DrawTilemapCells(Camera camera, Tilemap map, Color color)
    {
        if (map == null)
            return;

        BoundsInt b = map.cellBounds;
        int drawn = 0;
        Color c = new(color.r, color.g, color.b, 0.7f);
        for (int y = b.yMin; y < b.yMax && drawn < 400; y++)
        {
            for (int x = b.xMin; x < b.xMax && drawn < 400; x++)
            {
                Vector3Int cell = new(x, y, 0);
                if (!map.HasTile(cell))
                    continue;

                Vector3 min = map.CellToWorld(cell);
                Vector3 max = map.CellToWorld(cell + Vector3Int.one);
                DrawWorldRect(camera, MinMaxBounds(min, max), c);
                drawn++;
            }
        }
    }

    private void DrawWorldRect(Camera camera, Bounds bounds, Color color)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] world =
        {
            new(min.x, min.y, 0f),
            new(max.x, min.y, 0f),
            new(max.x, max.y, 0f),
            new(min.x, max.y, 0f)
        };

        screenBuffer.Clear();
        for (int i = 0; i < 4; i++)
        {
            if (!TryWorldToScreen(camera, world[i], out Vector3 s))
                return;
            screenBuffer.Add(s);
        }

        DrawScreenLoop(screenBuffer, color);
    }

    private void UpdatePlayerDiag(BoxCollider2D box)
    {
        Rigidbody2D rb = box.attachedRigidbody;
        Vector3 tp = box.transform.TransformPoint(box.offset);
        Vector2 rbp = rb != null ? rb.position : (Vector2)box.transform.position;

        string visualInfo = string.Empty;
        Transform visual = box.transform.Find(PlayerAppearance.VisualRootName);
        SpriteRenderer body = null;
        if (visual != null)
        {
            Transform bodyT = visual.Find("Body");
            if (bodyT != null)
                body = bodyT.GetComponent<SpriteRenderer>();
            visualInfo = $" visualLocal=({visual.localPosition.x:F2},{visual.localPosition.y:F2})";
        }

        string bodyInfo = string.Empty;
        if (body != null && body.sprite != null)
        {
            Vector3 bc = body.bounds.center;
            bodyInfo = $" bodyBounds=({bc.x:F2},{bc.y:F2})";
        }

        playerDiag =
            $"offset=({box.offset.x:F3},{box.offset.y:F3}) size=({box.size.x:F3},{box.size.y:F3}) | " +
            $"tr=({box.transform.position.x:F2},{box.transform.position.y:F2}) " +
            $"rb=({rbp.x:F2},{rbp.y:F2}) box=({tp.x:F2},{tp.y:F2})" +
            visualInfo + bodyInfo;
    }

    private void DrawPlayerPivot(Camera camera)
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;

        if (TryWorldToScreen(camera, player.transform.position, out Vector3 s))
        {
            const float mark = 8f;
            GL.Begin(GL.LINES);
            GL.Color(PivotColor);
            GL.Vertex3(s.x - mark, s.y, 0f);
            GL.Vertex3(s.x + mark, s.y, 0f);
            GL.Vertex3(s.x, s.y - mark, 0f);
            GL.Vertex3(s.x, s.y + mark, 0f);
            GL.End();
        }

        // Ciano = bounds do sprite Body (onde o personagem realmente está).
        Transform visual = player.transform.Find(PlayerAppearance.VisualRootName);
        Transform bodyT = visual != null ? visual.Find("Body") : player.transform.Find("Body");
        if (bodyT == null)
            return;

        SpriteRenderer body = bodyT.GetComponent<SpriteRenderer>();
        if (body == null || body.sprite == null)
            return;

        DrawWorldRect(camera, body.bounds, new Color(0.2f, 1f, 1f, 1f));
    }

    private static Bounds MinMaxBounds(Vector3 a, Vector3 b)
    {
        Vector3 min = Vector3.Min(a, b);
        Vector3 max = Vector3.Max(a, b);
        Bounds bounds = new();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static bool TryWorldToScreen(Camera camera, Vector3 world, out Vector3 screen)
    {
        screen = camera.WorldToScreenPoint(world);
        return screen.z > 0f;
    }

    private static void DrawScreenLoop(List<Vector3> points, Color color)
    {
        if (points.Count < 2)
            return;

        GL.Begin(GL.LINES);
        GL.Color(color);
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Count];
            GL.Vertex3(a.x, a.y, 0f);
            GL.Vertex3(b.x, b.y, 0f);
        }
        GL.End();
    }
}
