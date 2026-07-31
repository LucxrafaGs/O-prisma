using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Click NPCs to talk. Typing text; second click completes; click outside dismisses when done.
/// </summary>
[DefaultExecutionOrder(-40)]
public class NpcDialogueSystem : MonoBehaviour
{
    private enum Phase
    {
        Hidden,
        Typing,
        Complete
    }

    private static readonly Color CardColor = new(0.93f, 0.89f, 0.8f, 0.98f);
    private static readonly Color Ink = new(0.18f, 0.14f, 0.11f, 1f);
    private static readonly Color InkMuted = new(0.4f, 0.34f, 0.28f, 1f);

    [SerializeField] private float charsPerSecond = 38f;

    private Phase phase = Phase.Hidden;
    private NpcController activeNpc;
    private string fullText = string.Empty;
    private float typedChars;
    private int lineIndex;

    private Canvas canvas;
    private RectTransform cardRoot;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI bodyLabel;
    private TextMeshProUGUI hintLabel;

    private void Awake()
    {
        EnsureUi();
        HideCard();
    }

    private void Update()
    {
        if (PrismaBackpackMenu.IsOpen)
            return;

        if (phase == Phase.Typing)
        {
            typedChars += charsPerSecond * Time.unscaledDeltaTime;
            int visible = Mathf.Clamp(Mathf.FloorToInt(typedChars), 0, fullText.Length);
            bodyLabel.text = fullText.Substring(0, visible);
            if (visible >= fullText.Length)
                SetComplete();
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Dev mode captura a UI — nao inicia dialogo por baixo dos paineis.
        if (DevModeController.IsOpen)
            return;

        HandleClick(Mouse.current.position.ReadValue());
    }

    private void HandleClick(Vector2 screenPos)
    {
        if (phase != Phase.Hidden && IsPointerOverCard(screenPos))
        {
            if (phase == Phase.Typing)
                CompleteTyping();
            return;
        }

        NpcController hitNpc = RaycastNpc(screenPos);
        if (hitNpc != null)
        {
            if (phase == Phase.Typing && activeNpc == hitNpc)
            {
                CompleteTyping();
                return;
            }

            BeginDialogue(hitNpc);
            return;
        }

        if (phase == Phase.Complete)
            CloseDialogue();
        else if (phase == Phase.Typing)
            CompleteTyping();
    }

    private void BeginDialogue(NpcController npc)
    {
        if (activeNpc != null && activeNpc != npc)
            activeNpc.SetMovementLocked(false);

        activeNpc = npc;
        activeNpc.SetMovementLocked(true);
        lineIndex = Random.Range(0, npc.Lines.Length);
        fullText = npc.Lines[lineIndex];
        typedChars = 0f;
        phase = Phase.Typing;

        nameLabel.text = npc.DisplayName;
        bodyLabel.text = string.Empty;
        hintLabel.text = "clique para adiantar";
        cardRoot.gameObject.SetActive(true);
        PositionCardNearNpc(npc);
    }

    private void CompleteTyping()
    {
        typedChars = fullText.Length;
        bodyLabel.text = fullText;
        SetComplete();
    }

    private void SetComplete()
    {
        phase = Phase.Complete;
        hintLabel.text = "clique fora para fechar";
    }

    private void CloseDialogue()
    {
        if (activeNpc != null)
            activeNpc.SetMovementLocked(false);

        activeNpc = null;
        phase = Phase.Hidden;
        HideCard();
    }

    private void HideCard()
    {
        if (cardRoot != null)
            cardRoot.gameObject.SetActive(false);
    }

    private void PositionCardNearNpc(NpcController npc)
    {
        Camera cam = Camera.main;
        if (cam == null || cardRoot == null)
            return;

        Vector3 world = npc.transform.position + new Vector3(0f, 1.35f, 0f);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            local.x = Mathf.Clamp(local.x, -760f, 760f);
            local.y = Mathf.Clamp(local.y, -420f, 420f);
            cardRoot.anchoredPosition = local;
        }
    }

    private bool IsPointerOverCard(Vector2 screenPos)
    {
        if (cardRoot == null || !cardRoot.gameObject.activeSelf)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(cardRoot, screenPos, null);
    }

    private static NpcController RaycastNpc(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        Vector3 world3 = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
        Vector2 world = world3;

        // Raio generoso: o collider fisico e so nos pes; o trigger Interaction cobre o corpo.
        Collider2D[] hits = Physics2D.OverlapCircleAll(world, 0.75f);
        NpcController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            NpcController npc = hit.GetComponent<NpcController>() ?? hit.GetComponentInParent<NpcController>();
            if (npc == null)
                continue;

            float dist = ((Vector2)npc.transform.position - world).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = npc;
            }
        }

        return best;
    }

    private void EnsureUi()
    {
        GameObject canvasObject = new GameObject(
            "NpcDialogueCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        cardRoot = CreateCard(canvas.transform);
    }

    private RectTransform CreateCard(Transform parent)
    {
        GameObject card = new GameObject("SpeechCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(460f, 150f);
        card.GetComponent<Image>().color = CardColor;

        nameLabel = PrismaUIBuilder.CreateText(card.transform, "Name", string.Empty, 22, TextAlignmentOptions.TopLeft);
        nameLabel.color = Ink;
        nameLabel.fontStyle = FontStyles.Bold;
        RectTransform nameRect = nameLabel.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -14f);
        nameRect.sizeDelta = new Vector2(-36f, 30f);

        bodyLabel = PrismaUIBuilder.CreateText(card.transform, "Body", string.Empty, 20, TextAlignmentOptions.TopLeft);
        bodyLabel.color = Ink;
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(18f, 34f);
        bodyRect.offsetMax = new Vector2(-18f, -46f);

        hintLabel = PrismaUIBuilder.CreateText(card.transform, "Hint", string.Empty, 14, TextAlignmentOptions.BottomRight);
        hintLabel.color = InkMuted;
        RectTransform hintRect = hintLabel.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-14f, 10f);
        hintRect.sizeDelta = new Vector2(-28f, 22f);

        return rect;
    }
}
