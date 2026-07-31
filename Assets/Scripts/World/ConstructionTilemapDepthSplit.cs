using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Camadas Construções / Construções 2 / Construções 3:
/// tiles COM collider ficam na base (player passa na frente);
/// tiles SEM collider vão para um Tilemap "Roof" irmão (player passa atrás).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
[DefaultExecutionOrder(-50)]
public class ConstructionTilemapDepthSplit : MonoBehaviour
{
    public const string RoofSuffix = " Roof";
    public const int RoofSortBoost = 10000;

    private static readonly string[] TargetNames =
    {
        "Construções",
        "Construções 2",
        "Construções 3",
        "Construcoes",
        "Construcoes 2",
        "Construcoes 3",
    };

    private Tilemap baseMap;
    private Tilemap roofMap;
    private TilemapRenderer baseRenderer;
    private TilemapRenderer roofRenderer;
    private bool splitDone;

    private void Awake()
    {
        if (!IsConstructionLayer(gameObject.name))
        {
            enabled = false;
            return;
        }

        EnsureSplit();
    }

    private void LateUpdate()
    {
        if (!splitDone || roofRenderer == null)
            return;

        // Roof sempre acima do player (OrderBias + boost).
        // Base permanece com sorting baixo → player passa na frente da parte com collider.
        roofRenderer.sortingOrder = WorldDepth.OrderBias + RoofSortBoost;
    }

    public void EnsureSplit()
    {
        baseMap = GetComponent<Tilemap>();
        baseRenderer = GetComponent<TilemapRenderer>();
        if (baseMap == null || baseRenderer == null)
            return;

        // Camada só visual (sem TilemapCollider): trata o mapa inteiro como roof.
        TilemapCollider2D baseCollider = GetComponent<TilemapCollider2D>();
        if (baseCollider == null || !baseCollider.enabled)
        {
            EnsureRoofMapEmptyVisualOnly();
            splitDone = true;
            return;
        }

        EnsureRoofMap();
        MoveNonColliderTilesToRoof();
        splitDone = true;
    }

    private void EnsureRoofMapEmptyVisualOnly()
    {
        // Só sobe o sorting deste próprio mapa.
        if (baseRenderer != null)
            baseRenderer.sortingOrder = WorldDepth.OrderBias + RoofSortBoost;
    }

    private void EnsureRoofMap()
    {
        string roofName = gameObject.name + RoofSuffix;
        Transform existing = transform.parent != null
            ? transform.parent.Find(roofName)
            : null;

        GameObject roofObject;
        if (existing != null)
        {
            roofObject = existing.gameObject;
        }
        else
        {
            roofObject = new GameObject(roofName);
            roofObject.transform.SetParent(transform.parent, false);
            roofObject.transform.localPosition = transform.localPosition;
            roofObject.transform.localRotation = transform.localRotation;
            roofObject.transform.localScale = transform.localScale;
            roofObject.layer = gameObject.layer;
            // Coloca logo após a base.
            roofObject.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        }

        roofMap = roofObject.GetComponent<Tilemap>();
        if (roofMap == null)
            roofMap = roofObject.AddComponent<Tilemap>();

        roofRenderer = roofObject.GetComponent<TilemapRenderer>();
        if (roofRenderer == null)
            roofRenderer = roofObject.AddComponent<TilemapRenderer>();

        roofRenderer.sortingLayerID = baseRenderer.sortingLayerID;
        roofRenderer.sortingOrder = WorldDepth.OrderBias + RoofSortBoost;
        roofRenderer.mode = baseRenderer.mode;
        roofRenderer.sharedMaterial = baseRenderer.sharedMaterial;

        // Roof nunca colide.
        TilemapCollider2D roofCol = roofObject.GetComponent<TilemapCollider2D>();
        if (roofCol != null)
        {
            if (Application.isPlaying)
                Destroy(roofCol);
            else
                DestroyImmediate(roofCol);
        }
    }

    private void MoveNonColliderTilesToRoof()
    {
        if (roofMap == null)
            return;

        BoundsInt bounds = baseMap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = baseMap.GetTile(pos);
            if (tile == null)
                continue;

            if (TileHasCollider(tile, pos))
                continue;

            // Visual-only → roof; limpa da base.
            Matrix4x4 matrix = baseMap.GetTransformMatrix(pos);
            Color color = baseMap.GetColor(pos);

            roofMap.SetTile(pos, tile);
            roofMap.SetTransformMatrix(pos, matrix);
            roofMap.SetColor(pos, color);

            baseMap.SetTile(pos, null);
        }

        baseMap.CompressBounds();
        roofMap.CompressBounds();
    }

    private bool TileHasCollider(TileBase tileBase, Vector3Int position)
    {
        if (tileBase is Tile tile)
            return tile.colliderType != Tile.ColliderType.None;

        // Outros TileBase: se o sprite tem physics shape, considera colisão.
        Sprite sprite = null;
        if (tileBase is AnimatedTile animated &&
            animated.m_AnimatedSprites != null &&
            animated.m_AnimatedSprites.Length > 0)
            sprite = animated.m_AnimatedSprites[0];

        if (sprite != null)
            return sprite.GetPhysicsShapeCount() > 0;

        // Fallback: ponto central da célula no TilemapCollider2D.
        TilemapCollider2D mapCollider = GetComponent<TilemapCollider2D>();
        if (mapCollider != null && mapCollider.enabled && baseMap != null)
        {
            Vector2 center = baseMap.GetCellCenterWorld(position);
            return mapCollider.OverlapPoint(center);
        }

        return false;
    }

    public static bool IsConstructionLayer(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Ignora os Roofs gerados.
        if (name.EndsWith(RoofSuffix))
            return false;

        for (int i = 0; i < TargetNames.Length; i++)
        {
            if (name == TargetNames[i])
                return true;
        }

        // Aceita variações "Construções*" sem ser Roof.
        return name.StartsWith("Constru") && !name.Contains(RoofSuffix.Trim());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAll()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        int count = 0;
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null || !IsConstructionLayer(map.gameObject.name))
                continue;

            if (map.GetComponent<ConstructionTilemapDepthSplit>() == null)
            {
                map.gameObject.AddComponent<ConstructionTilemapDepthSplit>();
                count++;
            }
        }

        if (count > 0)
            Debug.Log($"Prisma: ConstructionTilemapDepthSplit em {count} camadas Construções.");
    }
}
