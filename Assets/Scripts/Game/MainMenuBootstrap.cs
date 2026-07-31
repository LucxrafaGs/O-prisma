using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuBootstrap : MonoBehaviour
{
    private void Awake()
    {
        GameSettings.Apply();
        PrismaUIBuilder.EnsureEventSystem(transform);
        PrismaUIBuilder.EnsureCanvas(transform);
        CharacterLibraryAccess.WarmUp();

        if (GetComponent<MainMenuUI>() == null)
            gameObject.AddComponent<MainMenuUI>();
    }
}

public class MainMenuUI : MonoBehaviour
{
    private static readonly Color Ink = new(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color InkMuted = new(0.68f, 0.74f, 0.82f, 1f);
    private static readonly Color TitleGold = new(0.96f, 0.88f, 0.62f, 1f);
    private static readonly Color Play = new(0.18f, 0.58f, 0.48f, 1f);
    private static readonly Color Load = new(0.22f, 0.42f, 0.68f, 1f);
    private static readonly Color Settings = new(0.28f, 0.32f, 0.4f, 1f);
    private static readonly Color Quit = new(0.58f, 0.24f, 0.28f, 1f);
    private static readonly Color Panel = new(0.08f, 0.1f, 0.14f, 0.72f);

    private GameObject menuPanel;
    private GameObject settingsPanel;

    private void Start()
    {
        // Garante EventSystem mesmo com Domain Reload desligado.
        PrismaUIBuilder.EnsureEventSystem(transform);
        BuildInterface();
    }

    private void BuildInterface()
    {
        Canvas canvas = PrismaUIBuilder.EnsureCanvas(transform);
        RectTransform root = canvas.transform as RectTransform;

        ClearBuiltChildren(root);

        BuildAtmosphere(root);

        menuPanel = BuildMenuPanel(root);
        settingsPanel = BuildSettingsPanel(root);
        settingsPanel.SetActive(false);
    }

    private static void ClearBuiltChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        Image existing = root.GetComponent<Image>();
        if (existing != null)
            Destroy(existing);
    }

    private static void BuildAtmosphere(RectTransform root)
    {
        Image bg = root.gameObject.GetComponent<Image>();
        if (bg == null)
            bg = root.gameObject.AddComponent<Image>();
        bg.sprite = PrismaUISprites.White;
        bg.color = new Color(0.05f, 0.07f, 0.11f, 1f);
        bg.raycastTarget = false;

        // Camadas de gradiente soft (topo quente / base fria).
        CreateGlow(root, "GlowTop", new Vector2(0.5f, 1f), new Vector2(1400f, 520f), new Vector2(0f, -40f),
            new Color(0.35f, 0.28f, 0.18f, 0.35f));
        CreateGlow(root, "GlowBottom", new Vector2(0.5f, 0f), new Vector2(1200f, 480f), new Vector2(0f, 40f),
            new Color(0.12f, 0.22f, 0.38f, 0.4f));
        CreateGlow(root, "GlowCenter", new Vector2(0.5f, 0.45f), new Vector2(900f, 900f), Vector2.zero,
            new Color(0.15f, 0.2f, 0.28f, 0.22f));
    }

    private static void CreateGlow(
        RectTransform parent,
        string name,
        Vector2 anchor,
        Vector2 size,
        Vector2 anchoredPos,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        Image image = go.GetComponent<Image>();
        image.sprite = PrismaUISprites.Circle;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
    }

    private GameObject BuildMenuPanel(RectTransform root)
    {
        RectTransform panel = PrismaUIBuilder.CreatePanel(root, "MenuPanel", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), raycast: false);

        TextMeshProUGUI brand = PrismaUIBuilder.CreateText(panel, "Brand", "PRISMA", 92, TextAlignmentOptions.Center);
        RectTransform brandRect = brand.rectTransform;
        brandRect.anchorMin = new Vector2(0.5f, 1f);
        brandRect.anchorMax = new Vector2(0.5f, 1f);
        brandRect.pivot = new Vector2(0.5f, 1f);
        brandRect.anchoredPosition = new Vector2(0f, -96f);
        brandRect.sizeDelta = new Vector2(900f, 110f);
        brand.color = TitleGold;
        brand.fontStyle = FontStyles.Bold;
        brand.characterSpacing = 12f;

        TextMeshProUGUI tagline = PrismaUIBuilder.CreateText(
            panel,
            "Tagline",
            "Pedra Branca espera por você",
            24,
            TextAlignmentOptions.Center);
        RectTransform tagRect = tagline.rectTransform;
        tagRect.anchorMin = new Vector2(0.5f, 1f);
        tagRect.anchorMax = new Vector2(0.5f, 1f);
        tagRect.pivot = new Vector2(0.5f, 1f);
        tagRect.anchoredPosition = new Vector2(0f, -210f);
        tagRect.sizeDelta = new Vector2(720f, 36f);
        tagline.color = InkMuted;
        tagline.fontStyle = FontStyles.Italic;

        RectTransform card = PrismaUIBuilder.CreateCard(panel, "MenuCard", new Vector2(460f, 420f), Panel);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, -36f);

        GameObject list = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(card, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = Vector2.zero;
        listRect.anchorMax = Vector2.one;
        listRect.offsetMin = new Vector2(36f, 36f);
        listRect.offsetMax = new Vector2(-36f, -36f);

        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 8, 8);

        AddMenuButton(list.transform, "Jogar", Play, OnPlay);
        AddMenuButton(list.transform, "Carregar", Load, OnLoad);
        AddMenuButton(list.transform, "Configurações", Settings, OnSettings);
        AddMenuButton(list.transform, "Sair", Quit, OnQuit);

        return panel.gameObject;
    }

    private static void AddMenuButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action)
    {
        Button button = PrismaUIBuilder.CreateStyledButton(parent, label, color, action, new Vector2(0f, 78f));
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 78f;
        layout.preferredHeight = 78f;
        layout.flexibleWidth = 1f;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.fontSize = 28f;
    }

    private GameObject BuildSettingsPanel(RectTransform root)
    {
        RectTransform panel = PrismaUIBuilder.CreatePanel(
            root,
            "SettingsPanel",
            Vector2.zero,
            Vector2.one,
            new Color(0.04f, 0.05f, 0.08f, 0.92f),
            raycast: true);

        RectTransform card = PrismaUIBuilder.CreateCard(panel, "SettingsCard", new Vector2(820f, 640f), new Color(0.1f, 0.12f, 0.17f, 0.96f));
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, 20f);

        TextMeshProUGUI title = PrismaUIBuilder.CreateText(card, "SettingsTitle", "Configurações", 40, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(700f, 52f);
        title.color = TitleGold;
        title.fontStyle = FontStyles.Bold;

        GameObject content = new GameObject("SettingsContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(card, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(720f, 460f);
        contentRect.anchoredPosition = new Vector2(0f, -10f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 12, 12);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        TextMeshProUGUI audioHeader = PrismaUIBuilder.CreateText(content.transform, "AudioHeader", "Áudio", 24, TextAlignmentOptions.MidlineLeft);
        audioHeader.color = TitleGold;
        audioHeader.fontStyle = FontStyles.Bold;

        PrismaUIBuilder.CreateSlider(content.transform, "Volume geral", GameSettings.MasterVolume, value =>
        {
            GameSettings.MasterVolume = value;
            GameSettings.Apply();
        });
        PrismaUIBuilder.CreateSlider(content.transform, "Música", GameSettings.MusicVolume, value => GameSettings.MusicVolume = value);
        PrismaUIBuilder.CreateSlider(content.transform, "Efeitos", GameSettings.SfxVolume, value => GameSettings.SfxVolume = value);

        TextMeshProUGUI videoHeader = PrismaUIBuilder.CreateText(content.transform, "VideoHeader", "Vídeo", 24, TextAlignmentOptions.MidlineLeft);
        videoHeader.color = TitleGold;
        videoHeader.fontStyle = FontStyles.Bold;

        PrismaUIBuilder.CreateToggle(content.transform, "Tela cheia", GameSettings.Fullscreen, value =>
        {
            GameSettings.Fullscreen = value;
            GameSettings.Apply();
        });
        PrismaUIBuilder.CreateToggle(content.transform, "VSync", GameSettings.VSync, value =>
        {
            GameSettings.VSync = value;
            GameSettings.Apply();
        });
        PrismaUIBuilder.CreateSlider(content.transform, "Qualidade", GameSettings.QualityLevel / Mathf.Max(1f, QualitySettings.names.Length - 1f), value =>
        {
            GameSettings.QualityLevel = Mathf.RoundToInt(value * (QualitySettings.names.Length - 1));
            GameSettings.Apply();
        });

        Button back = PrismaUIBuilder.CreateStyledButton(panel, "Voltar", Settings, OnBackFromSettings, new Vector2(260f, 64f));
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 48f);

        return panel.gameObject;
    }

    private void OnPlay()
    {
        CharacterLibraryAccess.WarmUp();
        GameFlowState.StartNewCharacter = true;
        GameFlowState.SaveSlotsPurpose = SaveSlotsPurpose.SaveCharacter;
        SceneManager.LoadScene(GameScenes.CharacterCustomization);
    }

    private void OnLoad()
    {
        GameFlowState.StartNewCharacter = false;
        GameFlowState.SaveSlotsPurpose = SaveSlotsPurpose.LoadGame;
        SceneManager.LoadScene(GameScenes.SaveSlots);
    }

    private void OnSettings()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void OnBackFromSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (menuPanel != null)
            menuPanel.SetActive(true);
        GameSettings.Apply();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
