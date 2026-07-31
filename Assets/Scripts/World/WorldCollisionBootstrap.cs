using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// No Play: reativa colisão do mundo. O CircleCollider da Fonte estava desligado
/// na cena; tiles do Paredes passam a usar célula inteira (Grid).
/// </summary>
public static class WorldCollisionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        FixPlayerCollider();
        FixFonte();
        FixParedesTilemap();
        Physics2D.SyncTransforms();
    }

    private static void FixPlayerCollider()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;

        // Só garante sólido. NÃO mexe em offset/size — isso vem do modo de edição.
        Transform autoFoot = player.transform.Find(CharacterFootCollider.ChildName);
        if (autoFoot != null)
            Object.Destroy(autoFoot.gameObject);

        BoxCollider2D box = player.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.enabled = true;
            box.isTrigger = false;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
        }
    }

    private static void FixFonte()
    {
        GameObject fonte = GameObject.Find("Fonte");
        if (fonte == null)
            return;

        MonoBehaviour[] behaviours = fonte.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null)
                continue;
            if (mb.GetType().Name == "SpritePhysicsCollider2D")
                Object.Destroy(mb);
        }

        PolygonCollider2D[] polys = fonte.GetComponents<PolygonCollider2D>();
        for (int i = 0; i < polys.Length; i++)
            Object.Destroy(polys[i]);

        CircleCollider2D circle = fonte.GetComponent<CircleCollider2D>();
        if (circle == null)
            return;

        // NÃO mexe em offset/radius — só garante que está sólido e ligado.
        circle.enabled = true;
        circle.isTrigger = false;

        Rigidbody2D body = fonte.GetComponent<Rigidbody2D>();
        if (body == null)
            body = fonte.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
    }

    private static void FixParedesTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int m = 0; m < maps.Length; m++)
        {
            Tilemap map = maps[m];
            if (map == null || map.name != "Paredes")
                continue;

            TilemapCollider2D col = map.GetComponent<TilemapCollider2D>();
            if (col == null)
                col = map.gameObject.AddComponent<TilemapCollider2D>();

            col.enabled = true;
            col.isTrigger = false;

            BoundsInt bounds = map.cellBounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    if (!map.HasTile(pos))
                        continue;

                    Tile tile = map.GetTile<Tile>(pos);
                    if (tile != null)
                    {
                        if (tile.colliderType != Tile.ColliderType.Grid)
                        {
                            tile.colliderType = Tile.ColliderType.Grid;
                            map.RefreshTile(pos);
                        }
                        continue;
                    }

                    Sprite sprite = map.GetSprite(pos);
                    if (sprite == null)
                        continue;

                    Tile created = ScriptableObject.CreateInstance<Tile>();
                    created.sprite = sprite;
                    created.color = Color.white;
                    created.colliderType = Tile.ColliderType.Grid;
                    map.SetTile(pos, created);
                }
            }

            col.enabled = false;
            col.enabled = true;
            return;
        }
    }
}
