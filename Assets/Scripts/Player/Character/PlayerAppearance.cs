using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class PlayerAppearance : MonoBehaviour
{
    public const string VisualRootName = "Visual";

    [SerializeField] private CharacterSpriteLibrary library;
    [SerializeField] private bool applySavedAppearanceOnAwake = true;

    private readonly Dictionary<CharacterLayerType, SpriteRenderer> renderers = new();
    private readonly Dictionary<CharacterLayerType, Sprite[]> spriteSheets = new();
    private readonly Dictionary<CharacterLayerType, Sprite[]> page4SpriteSheets = new();
    private SpriteRenderer bodyRenderer;
    private Transform visualRoot;
    private bool usePage4Sprites;
    private string activeHatId = string.Empty;
    private readonly List<Vector2> physicsPathBuffer = new(64);

    private void Awake()
    {
        EnsureVisualRoot();
        EnsureSortingGroup();
        EnsureRenderers();

        // NPCs share PlayerAppearance but should not load the player save.
        if (applySavedAppearanceOnAwake && GetComponent<PlayerController>() != null)
            ApplySavedAppearance();

        CharacterLitMaterial.ApplyToHierarchy(transform);
    }

    public void SetApplySavedAppearanceOnAwake(bool enabled)
    {
        applySavedAppearanceOnAwake = enabled;
    }

    public void ApplySavedAppearance()
    {
        ApplyAppearance(CharacterAppearanceData.Load());
    }

    public void ApplyAppearance(Dictionary<CharacterLayerType, string> selection)
    {
        if (library == null)
            library = CharacterLibraryAccess.Get();

        Dictionary<CharacterLayerType, string> resolvedSelection = selection != null
            ? new Dictionary<CharacterLayerType, string>(selection)
            : CharacterLayerDefinitions.CreateDefaultSelection();
        CharacterCapePairing.EnforcePairedCapes(resolvedSelection);
        activeHatId = resolvedSelection.TryGetValue(CharacterLayerType.Hat, out string hatId) ? hatId : string.Empty;

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.RenderOrder)
        {
            string id = resolvedSelection.TryGetValue(layer, out string sheetId) ? sheetId : string.Empty;
            spriteSheets[layer] = library != null && !string.IsNullOrEmpty(id) ? library.GetSprites(id) : null;
            page4SpriteSheets[layer] = library != null && !string.IsNullOrEmpty(id)
                ? library.GetSprites(ToPage4Id(id))
                : null;
        }

        EnsureRenderers();
        ResetLayerPositions();
        CharacterLitMaterial.ApplyToHierarchy(transform);
    }

    public void SetUsePage4Sprites(bool enabled)
    {
        usePage4Sprites = enabled;
    }

    public SpriteRenderer BodyRenderer
    {
        get
        {
            if (bodyRenderer == null)
                EnsureRenderers();
            return bodyRenderer;
        }
    }

    /// <summary>
    /// Cola os pés VISUAIS no collider. Só Y; valor em local snapped à grade PPU
    /// pra não oscilar entre frames (blur ao andar de frente/costas).
    /// </summary>
    public void AlignVisualToCollider(BoxCollider2D box)
    {
        EnsureVisualRoot();
        if (visualRoot == null || box == null)
            return;

        SpriteRenderer body = BodyRenderer;
        if (body == null || body.sprite == null)
            return;

        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        visualRoot.localPosition = Vector3.zero;

        if (!TryGetVisualFeetLocalY(body, out float feetLocalY))
            return;

        float spriteFeetWorldY = body.transform.TransformPoint(new Vector3(0f, feetLocalY, 0f)).y;
        float colliderFeetWorldY = box.bounds.min.y;
        float worldDy = colliderFeetWorldY - spriteFeetWorldY;
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        if (scaleY < 0.0001f)
            return;

        float localDy = worldDy / scaleY;
        localDy = PixelSnap2D.Snap(localDy, PixelSnap2D.SpriteUnit);
        visualRoot.localPosition = new Vector3(0f, localDy, 0f);
    }

    /// <summary>
    /// Y local dos pés do mesh (relativo ao pivot do Body).
    /// </summary>
    private bool TryGetVisualFeetLocalY(SpriteRenderer body, out float feetLocalY)
    {
        feetLocalY = 0f;
        Sprite sprite = body.sprite;
        if (sprite == null)
            return false;

        float minY = float.MaxValue;
        bool found = false;

        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount > 0)
        {
            for (int s = 0; s < shapeCount; s++)
            {
                physicsPathBuffer.Clear();
                sprite.GetPhysicsShape(s, physicsPathBuffer);
                for (int i = 0; i < physicsPathBuffer.Count; i++)
                {
                    float y = physicsPathBuffer[i].y;
                    if (y < minY)
                    {
                        minY = y;
                        found = true;
                    }
                }
            }
        }

        if (!found)
        {
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0)
                return false;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y < minY)
                {
                    minY = vertices[i].y;
                    found = true;
                }
            }
        }

        if (!found || minY > 1000f)
            return false;

        feetLocalY = minY;
        return true;
    }

    private static string ToPage4Id(string page1Id)
    {
        return string.IsNullOrEmpty(page1Id)
            ? string.Empty
            : page1Id.Replace("char_a_p1_", "char_a_p4_");
    }

    public void ApplyAppearance(string skinId, string outfitId, string hairId, string hatId)
    {
        Dictionary<CharacterLayerType, string> selection = CharacterAppearanceData.Load();
        selection[CharacterLayerType.Skin] = skinId;
        selection[CharacterLayerType.Outfit] = outfitId ?? string.Empty;
        selection[CharacterLayerType.Hair] = hairId ?? string.Empty;
        selection[CharacterLayerType.Hat] = hatId ?? string.Empty;
        ApplyAppearance(selection);
    }

    public void SetFrame(int spriteIndex)
    {
        int displayFrame = ResolveDisplayFrame(spriteIndex);

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.RenderOrder)
        {
            if (!renderers.TryGetValue(layer, out SpriteRenderer renderer))
                continue;

            if (layer == CharacterLayerType.Hair && !CharacterHairVisibility.ShouldShowHair(activeHatId))
            {
                renderer.sprite = null;
                renderer.enabled = false;
                continue;
            }

            Sprite[] sprites = GetActiveSpriteSheet(layer);
            SetLayerSprite(renderer, sprites, displayFrame);
            CenterLayerOnPivot(renderer);
        }
    }

    /// <summary>
    /// Pivot bottom-left em frames de larguras diferentes desloca o desenho.
    /// Centraliza cada frame no eixo X (equivalente a BottomCenter).
    /// </summary>
    private static void CenterLayerOnPivot(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (renderer.sprite == null)
        {
            renderer.transform.localPosition = Vector3.zero;
            return;
        }

        Sprite sprite = renderer.sprite;
        float offsetX = (sprite.rect.width * 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit;
        renderer.transform.localPosition = new Vector3(offsetX, 0f, 0f);
    }

    public int ResolveDisplayFrame(int preferredFrame)
    {
        if (HasFrameForLayer(CharacterLayerType.Skin, preferredFrame))
            return preferredFrame;

        if (preferredFrame != 0 && HasFrameForLayer(CharacterLayerType.Skin, 0))
            return 0;

        return preferredFrame;
    }

    private bool HasFrameForLayer(CharacterLayerType layer, int frameIndex)
    {
        Sprite[] sprites = GetActiveSpriteSheet(layer);
        if (sprites == null || sprites.Length == 0)
            return false;

        return CharacterSpriteFrames.FindByFrame(sprites, frameIndex) != null;
    }

    private Sprite[] GetActiveSpriteSheet(CharacterLayerType layer)
    {
        if (usePage4Sprites)
        {
            page4SpriteSheets.TryGetValue(layer, out Sprite[] page4Sprites);
            return page4Sprites;
        }

        spriteSheets.TryGetValue(layer, out Sprite[] page1Sprites);
        return page1Sprites;
    }

    private void EnsureSortingGroup()
    {
        if (GetComponent<SortingGroup>() == null)
            gameObject.AddComponent<SortingGroup>();
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        Transform existing = transform.Find(VisualRootName);
        if (existing == null)
        {
            GameObject go = new GameObject(VisualRootName);
            go.transform.SetParent(transform, false);
            visualRoot = go.transform;
        }
        else
        {
            visualRoot = existing;
        }

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private void EnsureRenderers()
    {
        EnsureVisualRoot();

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.RenderOrder)
        {
            SpriteRenderer renderer = GetOrCreateLayer(layer);
            renderers[layer] = renderer;

            if (layer == CharacterLayerType.Skin)
                bodyRenderer = renderer;
        }
    }

    private void ResetLayerPositions()
    {
        foreach (CharacterLayerType layer in CharacterLayerDefinitions.RenderOrder)
        {
            if (renderers.TryGetValue(layer, out SpriteRenderer renderer) && renderer != null)
                renderer.transform.localPosition = Vector3.zero;
        }
    }

    private SpriteRenderer GetOrCreateLayer(CharacterLayerType layer)
    {
        EnsureVisualRoot();

        string layerName = CharacterLayerDefinitions.RendererName(layer);
        Transform child = visualRoot.Find(layerName);
        if (child == null)
        {
            // Migra filho antigo direto no Player, se existir.
            Transform legacy = transform.Find(layerName);
            if (legacy != null && legacy != visualRoot)
            {
                legacy.SetParent(visualRoot, false);
                child = legacy;
            }
        }

        if (child == null)
        {
            GameObject layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(visualRoot, false);
            child = layerObject.transform;
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<SpriteRenderer>();

        renderer.sortingOrder = CharacterLayerDefinitions.SortingOrder(layer);
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return renderer;
    }

    private static void SetLayerSprite(SpriteRenderer renderer, Sprite[] sprites, int spriteIndex)
    {
        if (renderer == null)
            return;

        if (sprites == null || sprites.Length == 0)
        {
            renderer.sprite = null;
            renderer.enabled = false;
            return;
        }

        Sprite sprite = CharacterSpriteFrames.FindByFrame(sprites, spriteIndex);
        if (sprite == null)
        {
            renderer.sprite = null;
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;
        renderer.sprite = sprite;
    }
}
