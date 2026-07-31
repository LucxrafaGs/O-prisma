using UnityEngine;

/// <summary>
/// Colisor de pés para top-down. Mantém o BoxCollider2D no root (como o Player)
/// e alinha o Visual nos pés — sem filho FootCollider desalinhado.
/// </summary>
public static class CharacterFootCollider
{
    public const string ChildName = "FootCollider";
    public const float Width = 0.14f;
    public const float Height = 0.1f;

    public static void Apply(Transform root, SpriteRenderer bodyRenderer = null)
    {
        if (root == null)
            return;

        // Remove legado que ficava longe dos pés.
        Transform legacy = root.Find(ChildName);
        if (legacy != null)
            Object.Destroy(legacy.gameObject);

        BoxCollider2D box = root.GetComponent<BoxCollider2D>();
        if (box == null)
            box = root.gameObject.AddComponent<BoxCollider2D>();

        box.enabled = true;
        box.isTrigger = false;
        box.size = new Vector2(Width, Height);
        // Pivot bottom-left tipico: sola centrada.
        box.offset = new Vector2(0.125f, Height * 0.5f);
        box.compositeOperation = Collider2D.CompositeOperation.None;

        PlayerAppearance appearance = root.GetComponent<PlayerAppearance>();
        if (appearance != null)
            appearance.AlignVisualToCollider(box);
        else if (bodyRenderer != null)
        {
            // Sem appearance: pelo menos tenta colocar o offset nos pés do mesh.
            box.offset = EstimateRootOffset(bodyRenderer);
        }
    }

    /// <summary>Compat: chama Apply no transform do collider.</summary>
    public static void Apply(BoxCollider2D box, SpriteRenderer bodyRenderer = null)
    {
        if (box == null)
            return;
        Apply(box.transform, bodyRenderer);
    }

    private static Vector2 EstimateRootOffset(SpriteRenderer bodyRenderer)
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
            return new Vector2(0.125f, Height * 0.5f);

        Sprite sprite = bodyRenderer.sprite;
        float minY = 0f;
        bool found = false;
        var path = new System.Collections.Generic.List<Vector2>(32);
        int shapes = sprite.GetPhysicsShapeCount();
        for (int s = 0; s < shapes; s++)
        {
            path.Clear();
            sprite.GetPhysicsShape(s, path);
            for (int i = 0; i < path.Count; i++)
            {
                if (!found || path[i].y < minY)
                {
                    minY = path[i].y;
                    found = true;
                }
            }
        }

        if (!found)
        {
            Vector2[] vertices = sprite.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!found || vertices[i].y < minY)
                {
                    minY = vertices[i].y;
                    found = true;
                }
            }
        }

        float feetCenterLocalY = minY + Height * 0.5f;
        float offsetX = (sprite.rect.width * 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit;
        return new Vector2(offsetX, feetCenterLocalY);
    }
}
