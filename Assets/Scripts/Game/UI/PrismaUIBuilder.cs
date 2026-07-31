using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class PrismaUIBuilder
{
    public static Canvas EnsureCanvas(Transform parent, int sortingOrder = 10)
    {
        Canvas canvas = null;
        Canvas[] existing = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].renderMode == RenderMode.ScreenSpaceOverlay
                && existing[i].GetComponent<GraphicRaycaster>() != null
                && existing[i].sortingOrder < 100)
            {
                canvas = existing[i];
                break;
            }
        }

        if (canvas != null)
            return canvas;

        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        if (parent != null)
            canvasObject.transform.SetParent(parent, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        return canvas;
    }

    public static EventSystem EnsureEventSystem(Transform parent)
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        EventSystem keep = null;
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem candidate = systems[i];
            if (candidate == null)
                continue;

            if (keep == null)
                keep = candidate;
            else
                Object.Destroy(candidate.gameObject);
        }

        if (keep == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (parent != null)
                eventSystem.transform.SetParent(parent, false);
            keep = eventSystem.GetComponent<EventSystem>();
        }

        ConfigureInputModule(keep);
        return keep;
    }

    private static void ConfigureInputModule(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacy != null)
            Object.Destroy(legacy);

        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null)
            module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        InputActionAsset actions = InputSystem.actions;
        if (actions != null)
            module.actionsAsset = actions;
        else
            module.AssignDefaultActions();

        module.enabled = false;
        module.enabled = true;
        eventSystem.enabled = true;
    }

    public static Image CreateBackground(RectTransform parent, Color color)
    {
        Image background = parent.gameObject.GetComponent<Image>();
        if (background == null)
            background = parent.gameObject.AddComponent<Image>();

        background.sprite = PrismaUISprites.White;
        background.type = Image.Type.Simple;
        background.color = color;
        background.raycastTarget = true;
        return background;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        bool raycast = false)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = raycast;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.extraPadding = true;
        return tmp;
    }

    public static Button CreateButton(Transform parent, string label, Color color, UnityAction onClick, Vector2 size)
    {
        return CreateStyledButton(parent, label, color, onClick, size, soft: true);
    }

    public static Button CreateStyledButton(
        Transform parent,
        string label,
        Color color,
        UnityAction onClick,
        Vector2 size,
        bool soft = true)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(image, soft);
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, Mathf.Clamp(size.y * 0.34f, 20f, 30f), TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 6f);
        textRect.offsetMax = new Vector2(-18f, -6f);
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.98f, 0.98f, 0.98f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;

        return button;
    }

    public static RectTransform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        bool raycast = false)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.sprite = PrismaUISprites.White;
        image.color = color;
        image.raycastTarget = raycast && color.a > 0.01f;
        return rect;
    }

    public static RectTransform CreateCard(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Image image = card.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(image, soft: true);
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    public static Slider CreateSlider(Transform parent, string label, float value, UnityAction<float> onChanged)
    {
        RectTransform row = CreatePanel(parent, label + "Row", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), raycast: false);
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = 72f;
        rowLayout.preferredHeight = 72f;

        TextMeshProUGUI labelText = CreateText(row, "Label", label, 22, TextAlignmentOptions.MidlineLeft);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0.34f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(0f, 40f);
        labelRect.anchoredPosition = Vector2.zero;
        labelText.color = new Color(0.88f, 0.9f, 0.94f, 1f);

        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.36f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.sizeDelta = new Vector2(0f, 28f);
        sliderRect.anchoredPosition = Vector2.zero;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(onChanged.Invoke);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderObject.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(bgImage, soft: false);
        bgImage.color = new Color(0.14f, 0.16f, 0.22f, 1f);
        bgImage.raycastTarget = true;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(6f, 6f);
        fillAreaRect.offsetMax = new Vector2(-6f, -6f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(fillImage, soft: false);
        fillImage.color = new Color(0.35f, 0.72f, 0.78f, 1f);
        fillImage.raycastTarget = false;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlideArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleSlideArea.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = PrismaUISprites.Circle;
        handleImage.type = Image.Type.Simple;
        handleImage.color = Color.white;
        handleImage.raycastTarget = true;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 22f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    public static Toggle CreateToggle(Transform parent, string label, bool value, UnityAction<bool> onChanged)
    {
        GameObject toggleObject = new GameObject(label, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.minHeight = 56f;
        layout.preferredHeight = 56f;

        RectTransform rect = toggleObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 56f);

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(toggleObject.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.sizeDelta = new Vector2(30f, 30f);
        bgRect.anchoredPosition = new Vector2(22f, 0f);
        Image bgImage = background.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(bgImage, soft: false);
        bgImage.color = new Color(0.16f, 0.18f, 0.24f, 1f);
        bgImage.raycastTarget = true;

        GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(background.transform, false);
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(5f, 5f);
        checkRect.offsetMax = new Vector2(-5f, -5f);
        Image checkImage = checkmark.GetComponent<Image>();
        PrismaUISprites.ApplyRounded(checkImage, soft: false);
        checkImage.color = new Color(0.4f, 0.85f, 0.7f, 1f);
        checkImage.raycastTarget = false;

        TextMeshProUGUI text = CreateText(toggleObject.transform, "Label", label, 22, TextAlignmentOptions.MidlineLeft);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(58f, 0f);
        textRect.offsetMax = Vector2.zero;
        text.color = new Color(0.9f, 0.92f, 0.95f, 1f);

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(onChanged.Invoke);
        return toggle;
    }
}
