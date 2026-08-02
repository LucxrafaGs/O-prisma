using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// F2 toggles developer controls for time, date, season and quick skips.
/// </summary>
[DefaultExecutionOrder(-25)]
public class DevModeController : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public static void ClearOpenFlag() => IsOpen = false;

    private static readonly Color Panel = new(0.1f, 0.12f, 0.16f, 0.96f);
    private static readonly Color Accent = new(0.35f, 0.75f, 0.55f, 1f);
    private static readonly Color Soft = new(0.9f, 0.92f, 0.95f, 1f);
    private static readonly Color ButtonColor = new(0.22f, 0.35f, 0.32f, 1f);

    private GameObject root;
    private TextMeshProUGUI statusText;
    private TMP_InputField hourField;
    private TMP_InputField minuteField;
    private TMP_InputField dayField;

    private void Awake()
    {
        BuildUi();
        SetOpen(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            SetOpen(!IsOpen);

        if (!IsOpen)
            return;

        RefreshStatus();
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        if (root != null)
            root.SetActive(open);

        if (open)
            RefreshFieldsFromClock();
    }

    private void RefreshStatus()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null || statusText == null)
            return;

        string rainState = RainWeatherSystem.Instance != null && RainWeatherSystem.Instance.IsRaining
            ? "Chuva: ON"
            : "Chuva: OFF";
        string fogState = RainWeatherSystem.Instance != null && RainWeatherSystem.Instance.IsFoggy
            ? "Neblina: ON"
            : "Neblina: OFF";
        string collidersState = ColliderDebugOverlay.Enabled ? "Colliders: ON" : "Colliders: OFF";

        statusText.text =
            $"DEV MODE\n{clock.TimeLabel}  ·  {clock.DateLabel}  ·  {clock.SeasonLabel}\n" +
            $"Progresso do dia: {clock.DayElapsedRealSeconds / GameTimeClock.RealSecondsPerDay * 100f:0.0}%\n" +
            $"{rainState}  ·  {fogState}  ·  {collidersState}";
    }

    private void RefreshFieldsFromClock()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        if (hourField != null)
            hourField.text = clock.Hour.ToString("00");
        if (minuteField != null)
            minuteField.text = clock.Minute.ToString("00");
        if (dayField != null)
            dayField.text = clock.DayOfMonth.ToString();
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject(
            "DevModeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("DevPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(canvasObject.transform, false);
        RectTransform panel = root.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-20f, -20f);
        panel.sizeDelta = new Vector2(360f, 0f);
        root.GetComponent<Image>().color = Panel;

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        statusText = CreateBlockText(panel, "Status", "DEV MODE", 17, Accent, 92f);

        hourField = CreateLabeledField(panel, "Hora", "06");
        minuteField = CreateLabeledField(panel, "Minuto", "00");
        dayField = CreateLabeledField(panel, "Dia", "1");

        CreateAction(panel, "Aplicar hora/data", ApplyTimeAndDay);
        CreateAction(panel, "+1 hora", () => GameTimeClock.Instance?.DevAddMinutes(60));
        CreateAction(panel, "+10 min", () => GameTimeClock.Instance?.DevAddMinutes(10));
        CreateAction(panel, "12:00 meio-dia", () => GameTimeClock.Instance?.DevSetTime(12, 0));
        CreateAction(panel, "17:00 golden hour", () => GameTimeClock.Instance?.DevSetTime(17, 0));
        CreateAction(panel, "18:00 crepusculo", () => GameTimeClock.Instance?.DevSetTime(18, 0));
        CreateAction(panel, "19:00 noite", () => GameTimeClock.Instance?.DevSetTime(19, 0));
        CreateAction(panel, "06:00 amanhecer", () => GameTimeClock.Instance?.DevSetTime(6, 0));
        CreateAction(panel, "03:00 dormir", () => GameTimeClock.Instance?.DevSetTime(3, 0));
        CreateAction(panel, "Proxima estacao", CycleSeason);
        CreateAction(panel, rainButtonLabel, ToggleRain);
        CreateAction(panel, fogButtonLabel, ToggleFog);
        CreateAction(panel, "Disparar trovao", TriggerThunder);
        CreateAction(panel, "Chamar Echo", TriggerEcho);
        CreateAction(panel, colliderDebugButtonLabel, ToggleColliderDebug);
        CreateAction(panel, "Fechar (F2)", () => SetOpen(false));

        CreateBlockText(panel, "Tip", "F2 painel · F3 colliders", 13, Soft, 22f);
    }

    private string rainButtonLabel = "Ligar chuva";
    private string fogButtonLabel = "Ligar neblina";
    private string colliderDebugButtonLabel = "Ver colliders (F3)";

    private void ApplyTimeAndDay()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        int.TryParse(hourField != null ? hourField.text : "6", out int hour);
        int.TryParse(minuteField != null ? minuteField.text : "0", out int minute);
        int.TryParse(dayField != null ? dayField.text : "1", out int day);
        clock.DevSetDay(day);
        clock.DevSetTime(hour, minute);
        RefreshStatus();
    }

    private void ToggleRain()
    {
        WeatherDirector director = WeatherDirector.Instance ?? FindAnyObjectByType<WeatherDirector>();
        if (director == null)
            director = gameObject.AddComponent<WeatherDirector>();

        director.DevToggleRain();

        RainWeatherSystem rain = RainWeatherSystem.Instance;
        rainButtonLabel = rain != null && rain.IsRaining ? "Desligar chuva" : "Ligar chuva";
        fogButtonLabel = rain != null && rain.IsFoggy ? "Desligar neblina" : "Ligar neblina";
        RefreshWeatherButtonLabels();
        RefreshStatus();
    }

    private void ToggleFog()
    {
        WeatherDirector director = WeatherDirector.Instance ?? FindAnyObjectByType<WeatherDirector>();
        if (director == null)
            director = gameObject.AddComponent<WeatherDirector>();

        director.DevToggleFog();

        RainWeatherSystem rain = RainWeatherSystem.Instance;
        fogButtonLabel = rain != null && rain.IsFoggy ? "Desligar neblina" : "Ligar neblina";
        RefreshWeatherButtonLabels();
        RefreshStatus();
    }

    private void TriggerThunder()
    {
        WeatherDirector director = WeatherDirector.Instance ?? FindAnyObjectByType<WeatherDirector>();
        if (director == null)
            director = gameObject.AddComponent<WeatherDirector>();

        director.DevTriggerThunder();
        RefreshStatus();
    }

    private void TriggerEcho()
    {
        EchoApparitionSystem echo = EchoApparitionSystem.Instance
            ?? FindAnyObjectByType<EchoApparitionSystem>();
        if (echo == null)
        {
            GameObject host = new("EchoApparitionSystem");
            echo = host.AddComponent<EchoApparitionSystem>();
        }

        echo.ForceSpawn();
        RefreshStatus();
    }

    private void ToggleColliderDebug()
    {
        ColliderDebugOverlay.Toggle();
        colliderDebugButtonLabel = ColliderDebugOverlay.Enabled
            ? "Esconder colliders (F3)"
            : "Ver colliders (F3)";
        RefreshColliderDebugButtonLabel();
        RefreshStatus();
    }

    private void RefreshColliderDebugButtonLabel()
    {
        if (root == null)
            return;

        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label == null)
                continue;

            if (label.text.Contains("colliders (F3)"))
            {
                label.text = colliderDebugButtonLabel;
                if (label.transform.parent != null)
                    label.transform.parent.name = "Btn_" + colliderDebugButtonLabel;
            }
        }
    }

    private void RefreshWeatherButtonLabels()
    {
        if (root == null)
            return;

        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label == null)
                continue;

            if (label.text == "Ligar chuva" || label.text == "Desligar chuva")
            {
                label.text = rainButtonLabel;
                if (label.transform.parent != null)
                    label.transform.parent.name = "Btn_" + rainButtonLabel;
            }
            else if (label.text == "Ligar neblina" || label.text == "Desligar neblina")
            {
                label.text = fogButtonLabel;
                if (label.transform.parent != null)
                    label.transform.parent.name = "Btn_" + fogButtonLabel;
            }
        }
    }

    private void CycleSeason()
    {
        GameTimeClock clock = GameTimeClock.Instance;
        if (clock == null)
            return;

        GameTimeClock.Season next = clock.CurrentSeason switch
        {
            GameTimeClock.Season.Primavera => GameTimeClock.Season.Verao,
            GameTimeClock.Season.Verao => GameTimeClock.Season.Outono,
            GameTimeClock.Season.Outono => GameTimeClock.Season.Inverno,
            _ => GameTimeClock.Season.Primavera
        };
        clock.DevSetSeason(next);
        RefreshStatus();
    }

    private static TextMeshProUGUI CreateBlockText(
        RectTransform parent,
        string name,
        string value,
        int fontSize,
        Color color,
        float height)
    {
        TextMeshProUGUI text = PrismaUIBuilder.CreateText(parent, name, value, fontSize, TextAlignmentOptions.TopLeft);
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;

        RectTransform rect = text.rectTransform;
        rect.sizeDelta = new Vector2(0f, height);
        return text;
    }

    private TMP_InputField CreateLabeledField(RectTransform parent, string label, string value)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 34f;
        rowLayout.minHeight = 34f;

        HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 10f;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlHeight = true;
        rowGroup.childControlWidth = true;
        rowGroup.childForceExpandHeight = true;
        rowGroup.childForceExpandWidth = true;

        TextMeshProUGUI caption = PrismaUIBuilder.CreateText(row.transform, label + "Label", label, 15, TextAlignmentOptions.MidlineLeft);
        caption.color = Soft;
        caption.raycastTarget = false;
        LayoutElement captionLayout = caption.gameObject.AddComponent<LayoutElement>();
        captionLayout.preferredWidth = 90f;
        captionLayout.flexibleWidth = 0f;

        GameObject fieldObject = new GameObject(label + "Field", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        fieldObject.transform.SetParent(row.transform, false);
        fieldObject.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);
        LayoutElement fieldLayout = fieldObject.GetComponent<LayoutElement>();
        fieldLayout.flexibleWidth = 1f;

        TextMeshProUGUI text = PrismaUIBuilder.CreateText(fieldObject.transform, "Text", value, 16, TextAlignmentOptions.MidlineLeft);
        text.color = Soft;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 2f);
        textRect.offsetMax = new Vector2(-10f, -2f);

        TextMeshProUGUI placeholder = PrismaUIBuilder.CreateText(fieldObject.transform, "Placeholder", value, 16, TextAlignmentOptions.MidlineLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.25f);
        RectTransform phRect = placeholder.rectTransform;
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10f, 2f);
        phRect.offsetMax = new Vector2(-10f, -2f);

        TMP_InputField input = fieldObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = value;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.caretColor = Accent;
        return input;
    }

    private void CreateAction(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            "Btn_" + label,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = ButtonColor;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 36f;
        layout.minHeight = 36f;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        TextMeshProUGUI text = PrismaUIBuilder.CreateText(buttonObject.transform, "Label", label, 15, TextAlignmentOptions.Center);
        text.color = Soft;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
