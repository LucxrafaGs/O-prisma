using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// In-game HUD: clock/date/season (top-right) and held item slot (bottom-left, Tab to cycle).
/// </summary>
[DefaultExecutionOrder(-30)]
public class GameHudController : MonoBehaviour
{
    private static readonly Color Panel = new(0.18f, 0.14f, 0.11f, 0.88f);
    private static readonly Color Paper = new(0.93f, 0.89f, 0.8f, 0.96f);
    private static readonly Color Ink = new(0.16f, 0.12f, 0.1f, 1f);
    private static readonly Color Soft = new(0.92f, 0.86f, 0.74f, 1f);

    private TextMeshProUGUI timeText;
    private TextMeshProUGUI dateText;
    private TextMeshProUGUI seasonText;
    private TextMeshProUGUI itemNameText;
    private GameObject sleepOverlay;
    private TextMeshProUGUI sleepText;

    private void Awake()
    {
        PlayerHotbar.EnsureDefaults();
        BuildHud();
        GameTimeClock.OnSleepRequired += ShowSleepPrompt;
    }

    private void OnDestroy()
    {
        GameTimeClock.OnSleepRequired -= ShowSleepPrompt;
    }

    private void Update()
    {
        RefreshClock();
        RefreshHotbar();

        if (PrismaBackpackMenu.IsOpen || DevModeController.IsOpen)
            return;

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && !IsSleepShowing())
            PlayerHotbar.CycleNext();

        if (IsSleepShowing() && Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                ConfirmSleep();
        }
    }

    private bool IsSleepShowing() => sleepOverlay != null && sleepOverlay.activeSelf;

    private void RefreshClock()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        timeText.text = clock.TimeLabel;
        dateText.text = clock.DateLabel;
        seasonText.text = clock.SeasonLabel;
    }

    private void RefreshHotbar()
    {
        PlayerHotbar.HeldItem item = PlayerHotbar.Current;
        itemNameText.text = item != null ? item.DisplayName : "—";
    }

    private void ShowSleepPrompt()
    {
        if (sleepOverlay == null)
            return;

        sleepOverlay.SetActive(true);
        sleepText.text = "São 03:00.\nVocê precisa dormir.\n\nPressione E ou Espaço";
        Time.timeScale = 0f;
    }

    private void ConfirmSleep()
    {
        sleepOverlay.SetActive(false);
        Time.timeScale = 1f;
        GameTimeClock.Instance?.SleepUntilMorning();
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject(
            "GameHudCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform clockPanel = CreateCornerPanel(
            canvas.transform,
            "ClockPanel",
            new Vector2(1f, 1f),
            new Vector2(-28f, -28f),
            new Vector2(300f, 126f),
            Panel);

        timeText = AddLabel(clockPanel, "Time", "06:00", 36, Soft, FontStyles.Bold);
        AnchorTop(timeText.rectTransform, -12f, 48f);
        dateText = AddLabel(clockPanel, "Date", "Dia 1", 20, Soft, FontStyles.Normal);
        AnchorTop(dateText.rectTransform, -58f, 30f);
        seasonText = AddLabel(clockPanel, "Season", "Primavera", 20, Soft, FontStyles.Normal);
        AnchorBottom(seasonText.rectTransform, 14f, 30f);

        RectTransform itemPanel = CreateCornerPanel(
            canvas.transform,
            "ItemPanel",
            new Vector2(0f, 0f),
            new Vector2(28f, 28f),
            new Vector2(240f, 112f),
            Paper);

        TextMeshProUGUI hint = AddLabel(itemPanel, "Hint", "TAB · item na mão", 14, new Color(0.35f, 0.3f, 0.24f, 1f), FontStyles.Normal);
        AnchorTop(hint.rectTransform, -10f, 24f);
        itemNameText = AddLabel(itemPanel, "ItemName", "Lanterna", 28, Ink, FontStyles.Bold);
        AnchorFill(itemNameText.rectTransform, new Vector2(16f, 16f), new Vector2(-16f, -36f));

        sleepOverlay = new GameObject("SleepOverlay", typeof(RectTransform), typeof(Image));
        sleepOverlay.transform.SetParent(canvas.transform, false);
        RectTransform sleepRect = sleepOverlay.GetComponent<RectTransform>();
        sleepRect.anchorMin = Vector2.zero;
        sleepRect.anchorMax = Vector2.one;
        sleepRect.offsetMin = Vector2.zero;
        sleepRect.offsetMax = Vector2.zero;
        sleepOverlay.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.06f, 0.82f);
        sleepText = AddLabel(sleepOverlay.transform, "SleepText", string.Empty, 30, Soft, FontStyles.Normal);
        AnchorFill(sleepText.rectTransform, new Vector2(48f, 48f), new Vector2(-48f, -48f));
        sleepText.alignment = TextAlignmentOptions.Center;
        sleepOverlay.SetActive(false);
    }

    private static RectTransform CreateCornerPanel(
        Transform parent,
        string name,
        Vector2 cornerAnchor,
        Vector2 anchoredPos,
        Vector2 size,
        Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = cornerAnchor;
        rect.anchorMax = cornerAnchor;
        rect.pivot = cornerAnchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI AddLabel(Transform parent, string name, string text, int size, Color color, FontStyles style)
    {
        TextMeshProUGUI label = PrismaUIBuilder.CreateText(parent, name, text, size, TextAlignmentOptions.Center);
        label.color = color;
        label.fontStyle = style;
        return label;
    }

    private static void AnchorTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, height);
        rect.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void AnchorBottom(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, height);
        rect.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void AnchorFill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

/// <summary>
/// Quick-select held tools. Tab cycles. Defaults: lantern + camera.
/// </summary>
public static class PlayerHotbar
{
    public sealed class HeldItem
    {
        public string Id;
        public string DisplayName;
    }

    private static readonly System.Collections.Generic.List<HeldItem> Items = new();
    private static int index;

    public static HeldItem Current => Items.Count == 0 ? null : Items[Mathf.Clamp(index, 0, Items.Count - 1)];
    public static int CurrentIndex => index;

    public static void SetIndex(int value)
    {
        EnsureDefaults();
        if (Items.Count == 0)
        {
            index = 0;
            return;
        }

        index = Mathf.Clamp(value, 0, Items.Count - 1);
    }

    public static void EnsureDefaults()
    {
        if (Items.Count > 0)
            return;

        Items.Add(new HeldItem { Id = "lanterna", DisplayName = "Lanterna" });
        Items.Add(new HeldItem { Id = "camera", DisplayName = "Câmera" });
        PlayerInventory.UnlockFixed("lanterna");
        PlayerInventory.UnlockFixed("camera");
        index = 0;
    }

    public static void CycleNext()
    {
        EnsureDefaults();
        if (Items.Count == 0)
            return;

        index = (index + 1) % Items.Count;
    }
}
