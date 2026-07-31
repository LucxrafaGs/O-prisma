#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Força Collider Type = Grid em todas as tiles do tilemap Paredes
/// (colisao = celula inteira, alinhada ao desenho do tilemap).
/// </summary>
public static class ParedesTilemapColliderFix
{
    [MenuItem("Prisma/Fix Paredes Tilemap Colliders (Grid)")]
    public static void Fix()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        int changed = 0;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap map = tilemaps[i];
            if (map == null || map.name != "Paredes")
                continue;

            EnsureTilemapCollider(map);

            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(pos))
                    continue;

                TileBase tileBase = map.GetTile(pos);
                Tile tile = tileBase as Tile;
                if (tile == null)
                {
                    // Sprite pintado direto: cria Tile com Grid collider.
                    Sprite sprite = map.GetSprite(pos);
                    if (sprite == null)
                        continue;

                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.Grid;
                    map.SetTile(pos, tile);
                    changed++;
                    continue;
                }

                if (tile.colliderType != Tile.ColliderType.Grid)
                {
                    tile.colliderType = Tile.ColliderType.Grid;
                    EditorUtility.SetDirty(tile);
                    map.RefreshTile(pos);
                    changed++;
                }
            }

            TilemapCollider2D col = map.GetComponent<TilemapCollider2D>();
            if (col != null)
            {
                col.enabled = false;
                col.enabled = true;
                EditorUtility.SetDirty(col);
            }

            EditorUtility.SetDirty(map);
            Debug.Log($"Prisma: Paredes — {changed} tiles com Collider Type = Grid. Colisao = celula inteira.");
            return;
        }

        Debug.LogError("Prisma: tilemap 'Paredes' nao encontrado na cena aberta.");
    }

    private static void EnsureTilemapCollider(Tilemap map)
    {
        if (map.GetComponent<TilemapCollider2D>() != null)
            return;
        map.gameObject.AddComponent<TilemapCollider2D>();
    }
}
#endif
