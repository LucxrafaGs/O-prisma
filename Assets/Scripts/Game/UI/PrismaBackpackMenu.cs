using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game backpack menu. ESC opens the physical backpack on a desk — not a pause RPG list.
/// </summary>
[DefaultExecutionOrder(-80)]
public class PrismaBackpackMenu : MonoBehaviour
{
    private enum Section
    {
        None,
        Agenda,
        Mochila,
        Pessoas,
        Mapa,
        TocaFitas,
        Configuracoes
    }

    private enum AgendaTab
    {
        Calendario,
        Anotacoes
    }

    private static readonly Color DeskOverlay = new(0.12f, 0.09f, 0.07f, 0.72f);
    private static readonly Color BackpackBody = new(0.42f, 0.32f, 0.22f, 0.98f);
    private static readonly Color BackpackDark = new(0.28f, 0.2f, 0.14f, 1f);
    private static readonly Color Paper = new(0.93f, 0.89f, 0.8f, 1f);
    private static readonly Color PaperDark = new(0.86f, 0.8f, 0.68f, 1f);
    private static readonly Color Ink = new(0.2f, 0.16f, 0.12f, 1f);
    private static readonly Color InkMuted = new(0.38f, 0.32f, 0.26f, 1f);
    private static readonly Color Pocket = new(0.5f, 0.38f, 0.26f, 1f);
    private static readonly Color PocketHover = new(0.58f, 0.44f, 0.3f, 1f);
    private static readonly Color Danger = new(0.55f, 0.22f, 0.18f, 1f);
    private static readonly Color SoftButton = new(0.35f, 0.3f, 0.24f, 1f);

    public static bool IsOpen { get; private set; }

    public static void ClearOpenFlag() => IsOpen = false;

    [SerializeField] private string mainMenuSceneName = GameScenes.MainMenu;

    private GameObject menuRoot;
    private GameObject hubRoot;
    private GameObject detailRoot;
    private TextMeshProUGUI detailTitle;
    private RectTransform detailBody;
    private bool isOpen;
    private Section openSection = Section.None;
    private AgendaTab agendaTab = AgendaTab.Calendario;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureCanvas();
        BuildShell();
        SetMenuOpen(false);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (!isOpen)
        {
            SetMenuOpen(true);
            return;
        }

        if (openSection != Section.None)
        {
            ShowHub();
            return;
        }

        SetMenuOpen(false);
    }

    private void SetMenuOpen(bool open)
    {
        isOpen = open;
        IsOpen = open;
        menuRoot.SetActive(open);
        Time.timeScale = open ? 0f : 1f;

        if (open)
            ShowHub();
        else
            openSection = Section.None;
    }

    private void ShowHub()
    {
        openSection = Section.None;
        hubRoot.SetActive(true);
        detailRoot.SetActive(false);
        ClearDetailBody();
    }

    private void OpenSection(Section section)
    {
        openSection = section;
        hubRoot.SetActive(false);
        detailRoot.SetActive(true);
        ClearDetailBody();

        detailTitle.text = SectionTitle(section);

        switch (section)
        {
            case Section.Agenda:
                BuildAgendaPanel();
                break;
            case Section.Mochila:
                BuildMochilaPanel();
                break;
            case Section.Pessoas:
                BuildPessoasPanel();
                break;
            case Section.Mapa:
                BuildMapaPanel();
                break;
            case Section.TocaFitas:
                BuildTocaFitasPanel();
                break;
            case Section.Configuracoes:
                BuildConfigPanel();
                break;
        }
    }

    private static string SectionTitle(Section section)
    {
        return section switch
        {
            Section.Agenda => "Agenda escolar",
            Section.Mochila => "Mochila",
            Section.Pessoas => "Pessoas",
            Section.Mapa => "Mapa de Pedra Branca",
            Section.TocaFitas => "Toca-fitas",
            Section.Configuracoes => "Configurações",
            _ => string.Empty
        };
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(transform);
    }

    private void EnsureCanvas()
    {
        Canvas existing = null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>())
        {
            if (canvas.sortingOrder >= 100)
            {
                existing = canvas;
                break;
            }
        }

        if (existing != null)
            return;

        GameObject canvasObject = new GameObject(
            "BackpackMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform);

        Canvas created = canvasObject.GetComponent<Canvas>();
        created.renderMode = RenderMode.ScreenSpaceOverlay;
        created.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private Canvas GetMenuCanvas()
    {
        Canvas best = null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>())
        {
            if (best == null || canvas.sortingOrder > best.sortingOrder)
                best = canvas;
        }

        return best;
    }

    private void BuildShell()
    {
        Canvas canvas = GetMenuCanvas();

        menuRoot = new GameObject("BackpackMenu", typeof(RectTransform));
        menuRoot.transform.SetParent(canvas.transform, false);
        StretchFull((RectTransform)menuRoot.transform);

        Image overlay = menuRoot.AddComponent<Image>();
        overlay.color = DeskOverlay;
        overlay.raycastTarget = true;

        hubRoot = CreateCenteredPanel(menuRoot.transform, "Hub", new Vector2(720f, 620f), BackpackBody).gameObject;
        BuildHub(hubRoot.transform as RectTransform);

        detailRoot = CreateCenteredPanel(menuRoot.transform, "Detail", new Vector2(980f, 700f), Paper).gameObject;
        BuildDetailChrome(detailRoot.transform as RectTransform);
        detailRoot.SetActive(false);
    }

    private void BuildHub(RectTransform hub)
    {
        TextMeshProUGUI title = CreateLabel(hub, "Title", "Mochila", 36, TextAlignmentOptions.Center, Ink);
        PlaceTop(title.rectTransform, -28f, 52f);

        TextMeshProUGUI subtitle = CreateLabel(
            hub,
            "Subtitle",
            "Aberta sobre a mesa · ESC fecha",
            16,
            TextAlignmentOptions.Center,
            InkMuted);
        PlaceTop(subtitle.rectTransform, -78f, 28f);

        RectTransform pockets = CreateEmpty(hub, "Pockets", new Vector2(0.5f, 0.42f), new Vector2(620f, 360f));

        CreatePocket(pockets, "Agenda", "Agenda", new Vector2(0f, 120f), () => OpenSection(Section.Agenda));
        CreatePocket(pockets, "Mochila", "Mochila", new Vector2(-200f, 10f), () => OpenSection(Section.Mochila));
        CreatePocket(pockets, "Pessoas", "Pessoas", new Vector2(0f, 10f), () => OpenSection(Section.Pessoas));
        CreatePocket(pockets, "Mapa", "Mapa", new Vector2(200f, 10f), () => OpenSection(Section.Mapa));
        CreatePocket(pockets, "TocaFitas", "Toca-fitas", new Vector2(-110f, -120f), () => OpenSection(Section.TocaFitas));
        CreatePocket(pockets, "Config", "Configurações", new Vector2(110f, -120f), () => OpenSection(Section.Configuracoes));

        TextMeshProUGUI tip = CreateLabel(
            hub,
            "Tip",
            "Seis bolsos. Poucas coisas. Só o que cabe na vida de um estudante.",
            15,
            TextAlignmentOptions.Center,
            new Color(0.9f, 0.84f, 0.72f, 0.9f));
        PlaceBottom(tip.rectTransform, 22f, 40f);
    }

    private void BuildDetailChrome(RectTransform detail)
    {
        Image border = detail.GetComponent<Image>();
        border.color = Paper;

        RectTransform header = CreateEmpty(detail, "Header", new Vector2(0.5f, 1f), new Vector2(920f, 70f));
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -16f);

        detailTitle = CreateLabel(header, "DetailTitle", string.Empty, 30, TextAlignmentOptions.MidlineLeft, Ink);
        RectTransform titleRect = detailTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(24f, 0f);
        titleRect.offsetMax = new Vector2(-180f, 0f);

        Button back = CreateTintButton(header, "Voltar", SoftButton, ShowHub, new Vector2(140f, 44f));
        RectTransform backRect = back.transform as RectTransform;
        backRect.anchorMin = new Vector2(1f, 0.5f);
        backRect.anchorMax = new Vector2(1f, 0.5f);
        backRect.pivot = new Vector2(1f, 0.5f);
        backRect.anchoredPosition = new Vector2(-8f, 0f);

        detailBody = CreateEmpty(detail, "Body", new Vector2(0.5f, 0.45f), new Vector2(920f, 560f));
    }

    private void ClearDetailBody()
    {
        if (detailBody == null)
            return;

        for (int i = detailBody.childCount - 1; i >= 0; i--)
            Destroy(detailBody.GetChild(i).gameObject);
    }

    private void BuildAgendaPanel()
    {
        RectTransform tabs = CreateEmpty(detailBody, "Tabs", new Vector2(0.5f, 1f), new Vector2(900f, 48f));
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.anchoredPosition = new Vector2(0f, -8f);

        CreateTintButton(tabs, "Calendário", agendaTab == AgendaTab.Calendario ? BackpackDark : SoftButton, () =>
        {
            agendaTab = AgendaTab.Calendario;
            OpenSection(Section.Agenda);
        }, new Vector2(200f, 42f)).transform.localPosition = new Vector3(-120f, 0f, 0f);

        CreateTintButton(tabs, "Anotações", agendaTab == AgendaTab.Anotacoes ? BackpackDark : SoftButton, () =>
        {
            agendaTab = AgendaTab.Anotacoes;
            OpenSection(Section.Agenda);
        }, new Vector2(200f, 42f)).transform.localPosition = new Vector3(120f, 0f, 0f);

        RectTransform content = CreateScrollColumn(detailBody, "AgendaContent", new Vector2(0f, -40f), new Vector2(900f, 480f));

        if (agendaTab == AgendaTab.Calendario)
        {
            AddPaperLine(content, $"{AgendaJournal.WeekdayLabel} · {AgendaJournal.DayOfMonth} de {AgendaJournal.MonthLabel}", 22, true);
            AddPaperLine(content, "Horário de hoje", 16, false);
            foreach (AgendaJournal.ScheduleEntry entry in AgendaJournal.TodaySchedule)
                AddPaperLine(content, $"{entry.Time}  {entry.Title}", 18, false);
        }
        else
        {
            AddPaperLine(content, "Diário de investigação — preenchido automaticamente", 16, false);
            if (AgendaJournal.Notes.Count == 0)
            {
                AddPaperLine(content, "Nenhuma anotação ainda.", 18, false);
            }
            else
            {
                foreach (AgendaJournal.NoteEntry note in AgendaJournal.Notes)
                {
                    AddPaperLine(content, $"{note.DateLabel} · {note.Category}", 17, true);
                    AddPaperLine(content, note.Text, 16, false);
                }
            }
        }
    }

    private void BuildMochilaPanel()
    {
        RectTransform content = CreateScrollColumn(detailBody, "MochilaContent", Vector2.zero, new Vector2(900f, 520f));

        AddPaperLine(content, "Seis espaços livres — escolha com cuidado", 16, false);

        RectTransform grid = CreateEmpty(content, "Slots", new Vector2(0.5f, 0.5f), new Vector2(860f, 160f));
        LayoutElement gridLayout = grid.gameObject.AddComponent<LayoutElement>();
        gridLayout.minHeight = 160f;
        gridLayout.preferredHeight = 160f;

        HorizontalLayoutGroup row = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 12f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlHeight = true;
        row.childControlWidth = true;
        row.childForceExpandHeight = true;
        row.childForceExpandWidth = true;

        IReadOnlyList<PlayerInventory.ItemStack> slots = PlayerInventory.GetFreeSlots();
        for (int i = 0; i < PlayerInventory.FreeSlotCount; i++)
        {
            PlayerInventory.ItemStack item = slots[i];
            string label = item == null ? "vazio" : item.DisplayName;
            CreateSlotCard(grid, label, item == null);
        }

        AddPaperLine(content, "Itens fixos (não ocupam espaço)", 16, true);
        foreach (PlayerInventory.FixedItem item in PlayerInventory.GetFixedItems())
        {
            string state = item.Unlocked ? item.DisplayName : $"{item.DisplayName} — bloqueado";
            string detail = item.Unlocked ? item.Description : "Ainda não faz parte da sua rotina.";
            AddPaperLine(content, state, 17, false);
            AddPaperLine(content, detail, 15, false);
        }
    }

    private void BuildPessoasPanel()
    {
        RectTransform content = CreateScrollColumn(detailBody, "PessoasContent", Vector2.zero, new Vector2(900f, 520f));
        AddPaperLine(content, "Fichas de quem você conhece — sem barras de amizade", 16, false);

        bool any = false;
        foreach (PeopleJournal.PersonCard person in PeopleJournal.KnownPeopleList())
        {
            any = true;
            AddPaperLine(content, person.Name, 22, true);
            AddPaperLine(content, $"{PeopleJournal.RelationLabel(person.Relation)} · {person.Role}", 16, false);
            AddPaperLine(content, person.Likes, 16, false);
            AddPaperLine(content, person.Notes, 16, false);
            AddPaperLine(content, person.Birthday, 15, false);
        }

        if (!any)
            AddPaperLine(content, "Você ainda não registrou ninguém.", 18, false);
    }

    private void BuildMapaPanel()
    {
        RectTransform content = CreateScrollColumn(detailBody, "MapaContent", Vector2.zero, new Vector2(900f, 520f));
        AddPaperLine(content, "Ilha de Pedra Branca — só o que você já viu", 16, false);
        AddPaperLine(content, "Sem GPS. Sem pontos ao vivo. Só anotações.", 15, false);

        foreach (IslandMapJournal.PlaceMark place in IslandMapJournal.DiscoveredPlacesList())
        {
            AddPaperLine(content, $"{place.Name} · {place.District}", 18, true);
            if (!string.IsNullOrEmpty(place.Annotation))
                AddPaperLine(content, place.Annotation, 15, false);
            if (!string.IsNullOrEmpty(place.PlayerMark))
                AddPaperLine(content, $"Marcação: {place.PlayerMark}", 15, false);
        }
    }

    private void BuildTocaFitasPanel()
    {
        RectTransform content = CreateScrollColumn(detailBody, "WalkmanContent", Vector2.zero, new Vector2(900f, 520f));
        AddPaperLine(content, "Fitas desbloqueadas", 16, false);

        foreach (WalkmanLibrary.Tape tape in WalkmanLibrary.UnlockedTapesList())
        {
            bool active = tape.Id == WalkmanLibrary.ActiveTapeId;
            string title = active ? $"▶ {tape.Title}" : tape.Title;
            AddPaperLine(content, $"{title} · {tape.Kind}", 18, true);
            AddPaperLine(content, tape.Blurb, 15, false);

            string capturedId = tape.Id;
            Button play = CreateTintButton(content, active ? "Ouvindo" : "Reproduzir", SoftButton, () =>
            {
                WalkmanLibrary.ActiveTapeId = capturedId;
                OpenSection(Section.TocaFitas);
            }, new Vector2(160f, 40f));
            LayoutElement playLayout = play.gameObject.AddComponent<LayoutElement>();
            playLayout.minHeight = 44f;
            playLayout.preferredHeight = 44f;
        }
    }

    private void BuildConfigPanel()
    {
        RectTransform content = CreateScrollColumn(detailBody, "ConfigContent", Vector2.zero, new Vector2(900f, 520f));

        AddPaperLine(content, "Áudio", 20, true);
        CreateSettingsSlider(content, "Volume geral", GameSettings.MasterVolume, value =>
        {
            GameSettings.MasterVolume = value;
            GameSettings.Apply();
        });
        CreateSettingsSlider(content, "Música", GameSettings.MusicVolume, value => GameSettings.MusicVolume = value);
        CreateSettingsSlider(content, "Efeitos", GameSettings.SfxVolume, value => GameSettings.SfxVolume = value);

        AddPaperLine(content, "Vídeo", 20, true);
        CreateSettingsToggle(content, "Tela cheia", GameSettings.Fullscreen, value =>
        {
            GameSettings.Fullscreen = value;
            GameSettings.Apply();
        });
        CreateSettingsToggle(content, "VSync", GameSettings.VSync, value =>
        {
            GameSettings.VSync = value;
            GameSettings.Apply();
        });

        AddPaperLine(content, "Acessibilidade", 20, true);
        AddPaperLine(content, "Opções extras chegarão conforme o jogo crescer.", 15, false);

        AddPaperLine(content, "Jogo", 20, true);
        CreateWideAction(content, "Salvar agora", SoftButton, OnSaveGame);
        CreateWideAction(content, "Salvar em outro slot", SoftButton, OnSaveGameToSlot);
        CreateWideAction(content, "Carregar", SoftButton, OnLoadGame);
        CreateWideAction(content, "Voltar ao menu principal", SoftButton, GoToMainMenu);
        CreateWideAction(content, "Sair do jogo", Danger, QuitGame);
    }

    private void OnSaveGame()
    {
        Time.timeScale = 1f;
        if (GameFlowState.ActiveSaveSlot >= 0)
        {
            if (GameSessionSave.SaveManualToActiveSlot())
            {
                Debug.Log($"Prisma: progresso salvo no slot {GameFlowState.ActiveSaveSlot + 1}.");
                return;
            }
        }

        OnSaveGameToSlot();
    }

    private void OnSaveGameToSlot()
    {
        Time.timeScale = 1f;
        GameFlowState.StartNewCharacter = false;
        GameFlowState.SaveSlotsPurpose = SaveSlotsPurpose.SaveGame;
        GameFlowState.PendingSave = GameSessionSave.CaptureCurrent();
        if (GameFlowState.PendingSave == null || GameFlowState.PendingSave.IsEmpty)
        {
            GameFlowState.PendingSave = GameSaveData.FromSelection(
                CharacterProfileData.LoadName(),
                CharacterProfileData.LoadGender(),
                CharacterAppearanceData.Load());
        }

        // Autosave antes de trocar de cena, se já houver slot ativo.
        GameSessionSave.TryAutosave("antes de escolher slot");
        SceneManager.LoadScene(GameScenes.SaveSlots);
    }

    private void OnLoadGame()
    {
        Time.timeScale = 1f;
        GameSessionSave.TryAutosave("antes de carregar");
        GameFlowState.StartNewCharacter = false;
        GameFlowState.SaveSlotsPurpose = SaveSlotsPurpose.LoadGame;
        SceneManager.LoadScene(GameScenes.SaveSlots);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        GameSessionSave.TryAutosave("ao voltar ao menu");
        CharacterLibraryAccess.WarmUp();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        GameSessionSave.TryAutosave("ao sair");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CreatePocket(Transform parent, string name, string label, Vector2 anchoredPos, UnityAction onClick)
    {
        Button button = CreateTintButton(parent, label, Pocket, onClick, new Vector2(180f, 92f));
        RectTransform rect = button.transform as RectTransform;
        rect.anchoredPosition = anchoredPos;

        ColorBlock colors = button.colors;
        colors.highlightedColor = PocketHover;
        colors.pressedColor = BackpackDark;
        colors.selectedColor = PocketHover;
        button.colors = colors;
    }

    private void CreateSlotCard(Transform parent, string label, bool empty)
    {
        GameObject card = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = empty ? PaperDark : BackpackDark;
        LayoutElement layout = card.GetComponent<LayoutElement>();
        layout.minWidth = 120f;
        layout.preferredHeight = 140f;

        TextMeshProUGUI text = CreateLabel(card.transform, "Label", label, 16, TextAlignmentOptions.Center, empty ? InkMuted : Paper);
        StretchFull(text.rectTransform);
    }

    private void CreateWideAction(Transform parent, string label, Color color, UnityAction onClick)
    {
        Button button = CreateTintButton(parent, label, color, onClick, new Vector2(0f, 52f));
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 52f;
        layout.preferredHeight = 52f;
        layout.flexibleWidth = 1f;
    }

    private void CreateSettingsSlider(Transform parent, string label, float value, UnityAction<float> onChanged)
    {
        Slider slider = PrismaUIBuilder.CreateSlider(parent, label, value, onChanged);
        LayoutElement layout = slider.transform.parent.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;
        }

        foreach (TextMeshProUGUI text in slider.transform.parent.GetComponentsInChildren<TextMeshProUGUI>())
            text.color = Ink;
    }

    private void CreateSettingsToggle(Transform parent, string label, bool value, UnityAction<bool> onChanged)
    {
        Toggle toggle = PrismaUIBuilder.CreateToggle(parent, label, value, onChanged);
        foreach (TextMeshProUGUI text in toggle.GetComponentsInChildren<TextMeshProUGUI>())
            text.color = Ink;
    }

    private RectTransform CreateScrollColumn(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        GameObject scrollObject = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRect.sizeDelta = size;
        scrollRect.anchoredPosition = anchoredPos;
        scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        StretchFull((RectTransform)viewport.transform);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 24);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport.transform as RectTransform;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        return contentRect;
    }

    private void AddPaperLine(Transform parent, string text, int size, bool bold)
    {
        TextMeshProUGUI label = CreateLabel(parent, "Line", text, size, TextAlignmentOptions.MidlineLeft, Ink);
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.color = bold ? Ink : InkMuted;
        LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = bold ? 30f : 24f;
        layout.preferredHeight = bold ? 30f : 24f;
    }

    private static RectTransform CreateCenteredPanel(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform CreateEmpty(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        GameObject empty = new GameObject(name, typeof(RectTransform));
        empty.transform.SetParent(parent, false);
        RectTransform rect = empty.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int size, TextAlignmentOptions alignment, Color color)
    {
        TextMeshProUGUI label = PrismaUIBuilder.CreateText(parent, name, text, size, alignment);
        label.color = color;
        return label;
    }

    private static Button CreateTintButton(Transform parent, string label, Color color, UnityAction onClick, Vector2 size)
    {
        Button button = PrismaUIBuilder.CreateButton(parent, label, color, onClick, size);
        foreach (TextMeshProUGUI text in button.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.color = Paper;
            text.fontSize = 18;
        }

        return button;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PlaceTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-40f, height);
    }

    private static void PlaceBottom(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-48f, height);
    }
}
