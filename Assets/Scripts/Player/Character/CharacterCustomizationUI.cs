using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterCustomizationUI : MonoBehaviour
{
    private const float SwatchSize = 76f;
    private const float PreviewCellSize = SwatchSize * 3f;
    private const float PreviewClipPadding = 14f;
    private const float PreviewSpriteInset = 6f;
    private const float ScrollWheelSensitivity = 72f;
    private const int GridColumns = 4;
    private const int PreviewGridColumns = 3;

    [SerializeField] private CharacterSpriteLibrary library;
    [SerializeField] private CharacterPreviewAnimator previewAnimator;

    private readonly Dictionary<CharacterLayerType, string> selectedIds = CharacterLayerDefinitions.CreateDefaultSelection();
    private readonly Dictionary<CharacterCustomizationCategory, Button> categoryButtons = new();

    private Transform optionsContent;
    private TextMeshProUGUI optionsHeaderText;
    private TextMeshProUGUI summaryText;
    private TMP_InputField nameInput;
    private RenderTexture previewRenderTexture;
    private CharacterGender selectedGender = CharacterGender.Male;
    private CharacterCustomizationCategory activeCategory = CharacterCustomizationCategory.Skin;
    private string activeStyleGroupKey;
    private bool interfaceBuilt;

    private static readonly Color PanelColor = new(0.14f, 0.16f, 0.22f, 1f);
    private static readonly Color OptionsPanelColor = new(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color CellColor = new(0.22f, 0.24f, 0.3f, 1f);
    private static readonly Color SelectedColor = new(0.28f, 0.5f, 0.72f, 1f);
    private static readonly Color SelectedBorder = new(0.45f, 0.78f, 1f, 1f);

    public void Initialize(CharacterSpriteLibrary spriteLibrary, CharacterPreviewAnimator preview, RenderTexture previewTexture, bool newCharacter)
    {
        library = spriteLibrary;
        previewAnimator = preview;
        previewRenderTexture = previewTexture;
        ApplyInitialSelection(newCharacter);
        BuildInterface();
        RefreshPreview();
    }

    private void ApplyInitialSelection(bool newCharacter)
    {
        selectedIds.Clear();

        if (newCharacter)
        {
            selectedGender = CharacterGender.Male;
            CharacterRandomizer.ApplyNewCharacterDefaults(selectedIds, library, selectedGender);
            return;
        }

        foreach (KeyValuePair<CharacterLayerType, string> pair in CharacterAppearanceData.Load())
            selectedIds[pair.Key] = pair.Value;

        CharacterCapePairing.EnforcePairedCapes(selectedIds);
        selectedGender = CharacterProfileData.LoadGender();
    }

    private void Start()
    {
        if (interfaceBuilt)
            return;

        if (library == null)
            library = CharacterLibraryAccess.Get();

        if (previewRenderTexture == null)
            previewRenderTexture = CharacterCustomizationBootstrap.PreviewRenderTexture;

        ApplyInitialSelection(GameFlowState.StartNewCharacter);
        BuildInterface();
        RefreshPreview();
    }

    private void BuildInterface()
    {
        if (library == null)
            library = CharacterLibraryAccess.Get();

        if (previewRenderTexture == null)
            previewRenderTexture = CharacterCustomizationBootstrap.PreviewRenderTexture;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null || library == null)
            return;

        RectTransform root = canvas.transform as RectTransform;
        ClearChildren(root);

        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.12f, 0.16f, 1f);

        CreateTitle(root);
        CreateProfilePanel(root);
        CreatePreviewPanel(root);
        CreateOptionsPanel(root);
        CreateFooter(root);
        interfaceBuilt = true;
        RebuildOptionsPanel();
        RefreshCategoryButtons();
    }

    private void CreateTitle(RectTransform root)
    {
        TextMeshProUGUI title = CreateText(root, "Titulo", "Personalizar Personagem", 34, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(800f, 50f);
        title.color = new Color(0.95f, 0.9f, 0.75f, 1f);
    }

    private void CreateProfilePanel(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "ProfilePanel", new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.96f), PanelColor);

        TextMeshProUGUI nameLabel = CreateText(panel, "NameLabel", "Nome do personagem", 18, TextAlignmentOptions.MidlineLeft);
        RectTransform nameLabelRect = nameLabel.rectTransform;
        nameLabelRect.anchorMin = new Vector2(0.02f, 0.55f);
        nameLabelRect.anchorMax = new Vector2(0.28f, 0.95f);
        nameLabelRect.offsetMin = Vector2.zero;
        nameLabelRect.offsetMax = Vector2.zero;

        GameObject inputObject = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(panel, false);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.28f, 0.55f);
        inputRect.anchorMax = new Vector2(0.62f, 0.95f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        inputObject.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 1f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(inputObject.transform, false);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(12f, 6f);
        textAreaRect.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI inputText = CreateText(textArea.transform, "Text", "Aventureiro", 20, TextAlignmentOptions.MidlineLeft);
        RectTransform inputTextRect = inputText.rectTransform;
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI placeholder = CreateText(textArea.transform, "Placeholder", "Digite o nome...", 20, TextAlignmentOptions.MidlineLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        RectTransform placeholderRect = placeholder.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        nameInput = inputObject.GetComponent<TMP_InputField>();
        nameInput.textComponent = inputText;
        nameInput.placeholder = placeholder;
        nameInput.text = CharacterProfileData.LoadName();

        TextMeshProUGUI genderLabel = CreateText(panel, "GenderLabel", "Genero", 18, TextAlignmentOptions.MidlineLeft);
        RectTransform genderLabelRect = genderLabel.rectTransform;
        genderLabelRect.anchorMin = new Vector2(0.64f, 0.55f);
        genderLabelRect.anchorMax = new Vector2(0.72f, 0.95f);
        genderLabelRect.offsetMin = Vector2.zero;
        genderLabelRect.offsetMax = Vector2.zero;

        CreateGenderButton(panel, "Masculino", CharacterGender.Male, new Vector2(0.73f, 0.55f), new Vector2(0.85f, 0.95f));
        CreateGenderButton(panel, "Feminino", CharacterGender.Female, new Vector2(0.86f, 0.55f), new Vector2(0.98f, 0.95f));
        RefreshGenderButtons();
    }

    private void CreateGenderButton(RectTransform parent, string label, CharacterGender gender, Vector2 anchorMin, Vector2 anchorMax)
    {
        Button button = CreateButton(parent, label, CellColor, () => SetGender(gender));
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        button.gameObject.name = $"Gender_{gender}";
    }

    private void SetGender(CharacterGender gender)
    {
        selectedGender = gender;
        selectedIds[CharacterLayerType.Skin] = CharacterGenderUtility.GetSkinId(selectedGender);
        RefreshGenderButtons();
        RefreshPreview();
        RebuildOptionsPanel();
    }

    private void RefreshGenderButtons()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        foreach (Button button in canvas.GetComponentsInChildren<Button>())
        {
            if (!button.gameObject.name.StartsWith("Gender_"))
                continue;

            bool selected = button.gameObject.name == $"Gender_{selectedGender}";
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? SelectedColor : CellColor;
        }
    }

    private void CreatePreviewPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "PreviewPanel", new Vector2(0f, 0.14f), new Vector2(0.42f, 0.82f), PanelColor);

        TextMeshProUGUI label = CreateText(panel, "PreviewLabel", "Visualizacao", 22, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -12f);
        labelRect.sizeDelta = new Vector2(0f, 32f);

        GameObject previewImageObject = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
        previewImageObject.transform.SetParent(panel, false);
        RectTransform previewRect = previewImageObject.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.08f, 0.34f);
        previewRect.anchorMax = new Vector2(0.92f, 0.92f);
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;
        RawImage previewImage = previewImageObject.GetComponent<RawImage>();
        previewImage.texture = previewRenderTexture;
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        CreateCategoryTabs(panel);

        summaryText = CreateText(panel, "Summary", string.Empty, 13, TextAlignmentOptions.Center);
        RectTransform summaryRect = summaryText.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 0f);
        summaryRect.anchorMax = new Vector2(1f, 0f);
        summaryRect.pivot = new Vector2(0.5f, 0f);
        summaryRect.anchoredPosition = new Vector2(0f, 6f);
        summaryRect.sizeDelta = new Vector2(-24f, 72f);
        summaryText.color = new Color(0.8f, 0.85f, 0.95f, 1f);
    }

    private void CreateCategoryTabs(RectTransform panel)
    {
        GameObject tabsObject = new GameObject("CategoryTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabsObject.transform.SetParent(panel, false);
        RectTransform tabsRect = tabsObject.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0.05f, 0.14f);
        tabsRect.anchorMax = new Vector2(0.95f, 0.3f);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = tabsObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        categoryButtons.Clear();
        foreach (CharacterCustomizationCategory category in CharacterCustomizationCategoryUtility.Order)
        {
            CharacterCustomizationCategory captured = category;
            Button button = CreateButton(tabsObject.transform, CharacterCustomizationCategoryUtility.Label(category), CellColor, () => SelectCategory(captured));
            categoryButtons[category] = button;
        }
    }

    private void SelectCategory(CharacterCustomizationCategory category)
    {
        activeCategory = category;
        activeStyleGroupKey = null;
        RefreshCategoryButtons();
        RebuildOptionsPanel();
    }

    private void RefreshCategoryButtons()
    {
        foreach (KeyValuePair<CharacterCustomizationCategory, Button> pair in categoryButtons)
        {
            if (pair.Value == null)
                continue;

            Image image = pair.Value.GetComponent<Image>();
            if (image != null)
                image.color = pair.Key == activeCategory ? SelectedColor : CellColor;
        }
    }

    private void CreateOptionsPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "OptionsPanel", new Vector2(0.44f, 0.14f), new Vector2(0.98f, 0.82f), OptionsPanelColor);

        optionsHeaderText = CreateText(panel, "OptionsHeader", string.Empty, 20, TextAlignmentOptions.MidlineLeft);
        RectTransform headerRect = optionsHeaderText.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -10f);
        headerRect.sizeDelta = new Vector2(-24f, 34f);
        optionsHeaderText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

        ScrollRect scroll = CreateScrollView(panel, out optionsContent);
        RectTransform scrollRect = scroll.transform as RectTransform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(12f, 12f);
        scrollRect.offsetMax = new Vector2(-12f, -48f);
    }

    private void RebuildOptionsPanel()
    {
        if (optionsContent == null || library == null)
            return;

        ClearChildren(optionsContent);

        if (!string.IsNullOrEmpty(activeStyleGroupKey))
            BuildColorVariantGrid();
        else if (CharacterCustomizationCategoryUtility.UsesDirectColorGrid(activeCategory))
            BuildSkinColorGrid();
        else
            BuildStylePreviewGrid();
    }

    private void BuildSkinColorGrid()
    {
        optionsHeaderText.text = "Escolha o tom de pele";
        CharacterLayerType[] layers = CharacterCustomizationCategoryUtility.Layers(activeCategory);
        List<CharacterSpriteLibrary.SheetEntry> entries = CharacterCustomizationCatalog.BuildFlatEntries(library, layers);

        RectTransform grid = CreateGridContainer("SkinGrid", GridColumns);
        foreach (CharacterSpriteLibrary.SheetEntry entry in entries)
        {
            bool selected = GetSelectedId(CharacterLayerType.Skin) == entry.id;
            CreateColorSwatchCell(
                grid,
                CharacterCustomizationCatalog.GetEntrySwatchColor(entry, library),
                selected,
                () => SelectEntry(CharacterLayerType.Skin, entry.id));
        }
    }

    private void BuildStylePreviewGrid()
    {
        optionsHeaderText.text = $"Escolha o estilo - {CharacterCustomizationCategoryUtility.Label(activeCategory)}";
        CharacterLayerType[] layers = CharacterCustomizationCategoryUtility.Layers(activeCategory);
        CharacterLayerType? currentLayer = null;
        RectTransform currentGrid = null;

        foreach (CharacterCustomizationCatalog.StyleGroup group in CharacterCustomizationCatalog.BuildStyleGroups(library, layers))
        {
            if (currentLayer != group.layer)
            {
                currentLayer = group.layer;
                if (CharacterLayerDefinitions.AllowNone(group.layer))
                    currentGrid = CreateLayerSection(group.layer, includeNone: true);
                else
                    currentGrid = CreateLayerSection(group.layer, includeNone: false);
            }

            if (currentGrid == null)
                continue;

            bool selected = group.variants.Any(variant => GetSelectedId(group.layer) == variant.id);
            CreatePreviewCell(
                currentGrid,
                ResolvePreviewSprite(group),
                selected,
                () => OpenStyleColorPicker(group),
                PreviewCellSize);
        }
    }

    private void BuildColorVariantGrid()
    {
        CharacterCustomizationCatalog.StyleGroup group = FindActiveStyleGroup();
        if (group == null)
        {
            activeStyleGroupKey = null;
            BuildStylePreviewGrid();
            return;
        }

        optionsHeaderText.text = $"Cores - {group.title}";

        GameObject backRow = new GameObject("BackRow", typeof(RectTransform));
        backRow.transform.SetParent(optionsContent, false);
        LayoutElement backLayout = backRow.AddComponent<LayoutElement>();
        backLayout.minHeight = 40f;
        backLayout.preferredHeight = 40f;

        Button backButton = CreateButton(backRow.transform, "< Voltar aos estilos", CellColor, () =>
        {
            activeStyleGroupKey = null;
            RebuildOptionsPanel();
        });
        RectTransform backRect = backButton.transform as RectTransform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;

        bool singleVariant = group.variants.Count == 1;
        float variantCellSize = singleVariant ? PreviewCellSize : SwatchSize;
        int variantColumns = singleVariant ? PreviewGridColumns : GridColumns;
        RectTransform grid = CreateGridContainer("VariantGrid", variantColumns, variantCellSize);
        if (CharacterLayerDefinitions.AllowNone(group.layer))
        {
            CreateNoneCell(grid, group.layer, IsSelected(group.layer, string.Empty), () => SelectEntry(group.layer, string.Empty), variantCellSize);
        }

        foreach (CharacterSpriteLibrary.SheetEntry variant in group.variants)
        {
            bool selected = GetSelectedId(group.layer) == variant.id;
            if (singleVariant)
            {
                CreatePreviewCell(
                    grid,
                    ResolvePreviewSprite(variant),
                    selected,
                    () => SelectEntry(group.layer, variant.id),
                    variantCellSize);
            }
            else
            {
                CreateColorSwatchCell(
                    grid,
                    CharacterCustomizationCatalog.GetEntrySwatchColor(variant, library),
                    selected,
                    () => SelectEntry(group.layer, variant.id));
            }
        }
    }

    private CharacterCustomizationCatalog.StyleGroup FindActiveStyleGroup()
    {
        CharacterLayerType[] layers = CharacterCustomizationCategoryUtility.Layers(activeCategory);
        return CharacterCustomizationCatalog.BuildStyleGroups(library, layers)
            .FirstOrDefault(group => group.groupKey == activeStyleGroupKey);
    }

    private void OpenStyleColorPicker(CharacterCustomizationCatalog.StyleGroup group)
    {
        if (group.variants.Count == 1)
        {
            SelectEntry(group.layer, group.variants[0].id);
            return;
        }

        activeStyleGroupKey = group.groupKey;
        RebuildOptionsPanel();
    }

    private RectTransform CreateLayerSection(CharacterLayerType layer, bool includeNone)
    {
        TextMeshProUGUI header = CreateText(optionsContent, $"Header_{layer}", CharacterLayerDefinitions.SectionTitle(layer), 18, TextAlignmentOptions.MidlineLeft);
        LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
        headerLayout.minHeight = 30f;
        headerLayout.preferredHeight = 30f;
        header.color = new Color(0.82f, 0.88f, 0.98f, 1f);

        RectTransform grid = CreateGridContainer($"Grid_{layer}", PreviewGridColumns, PreviewCellSize);
        if (includeNone)
            CreateNoneCell(grid, layer, IsSelected(layer, string.Empty), () => SelectEntry(layer, string.Empty), PreviewCellSize);

        return grid;
    }

    private RectTransform CreateGridContainer(string name, int columns, float cellSize)
    {
        GameObject gridObject = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(optionsContent, false);

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;

        LayoutElement layout = gridObject.AddComponent<LayoutElement>();
        layout.minHeight = cellSize + 8f;
        layout.flexibleWidth = 1f;

        ContentSizeFitter fitter = gridObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return gridObject.GetComponent<RectTransform>();
    }

    private RectTransform CreateGridContainer(string name, int columns)
    {
        return CreateGridContainer(name, columns, SwatchSize);
    }

    private void CreateColorSwatchCell(RectTransform parent, Color swatchColor, bool selected, System.Action onClick, float cellSize = SwatchSize)
    {
        Button button = CreateIconButton(parent, selected, onClick, cellSize);
        float fillPadding = Mathf.Max(8f, cellSize * 0.12f);
        Image fill = CreateChildImage(button.transform, "Fill", swatchColor);
        Stretch(fill.rectTransform, fillPadding);

        Image border = CreateChildImage(button.transform, "Border", selected ? SelectedBorder : new Color(0f, 0f, 0f, 0.25f));
        Stretch(border.rectTransform, 0f);
        border.type = Image.Type.Sliced;
    }

    private void CreatePreviewCell(RectTransform parent, Sprite sprite, bool selected, System.Action onClick, float cellSize = PreviewCellSize)
    {
        Button button = CreateIconButton(parent, selected, onClick, cellSize);

        GameObject clipObject = new GameObject("PreviewClip", typeof(RectTransform), typeof(RectMask2D));
        clipObject.transform.SetParent(button.transform, false);
        Stretch(clipObject.GetComponent<RectTransform>(), PreviewClipPadding);

        Image preview = CreateChildImage(clipObject.transform, "Preview", Color.white);
        ConfigureCenteredPreviewImage(preview, sprite);

        if (sprite == null)
        {
            int fontSize = Mathf.RoundToInt(24f * (cellSize / SwatchSize));
            TextMeshProUGUI fallback = CreateText(button.transform, "Fallback", "?", fontSize, TextAlignmentOptions.Center);
            Stretch(fallback.rectTransform, 0f);
        }
    }

    private Sprite ResolvePreviewSprite(CharacterCustomizationCatalog.StyleGroup group)
    {
        if (group?.variants == null || group.variants.Count == 0)
            return group?.previewSprite;

        return ResolvePreviewSprite(group.variants[0]);
    }

    private Sprite ResolvePreviewSprite(CharacterSpriteLibrary.SheetEntry entry)
    {
        if (entry == null)
            return null;

        return CharacterSwatchColorSampler.PickSampleSprite(library, entry) ?? entry.referenceSprite;
    }

    private static void ConfigureCenteredPreviewImage(Image preview, Sprite sprite)
    {
        RectTransform rect = preview.rectTransform;
        Stretch(rect, PreviewSpriteInset);
        rect.pivot = new Vector2(0.5f, 0.5f);

        preview.sprite = sprite;
        preview.preserveAspect = true;
        preview.type = Image.Type.Simple;
        preview.raycastTarget = false;
        preview.maskable = true;
    }

    private void CreateNoneCell(RectTransform parent, CharacterLayerType layer, bool selected, System.Action onClick, float cellSize = PreviewCellSize)
    {
        Button button = CreateIconButton(parent, selected, onClick, cellSize);
        int fontSize = Mathf.RoundToInt(28f * (cellSize / SwatchSize));
        TextMeshProUGUI label = CreateText(button.transform, "Label", "X", fontSize, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 0f);
        label.color = new Color(0.9f, 0.55f, 0.55f, 1f);
        button.gameObject.name = $"Option_{layer}_";
    }

    private Button CreateIconButton(Transform parent, bool selected, System.Action onClick, float cellSize)
    {
        GameObject buttonObject = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.minWidth = cellSize;
        layout.minHeight = cellSize;
        layout.preferredWidth = cellSize;
        layout.preferredHeight = cellSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? SelectedColor : CellColor;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            onClick?.Invoke();
            RefreshPreview();
            RebuildOptionsPanel();
        });

        return button;
    }

    private static Image CreateChildImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private void SelectEntry(CharacterLayerType layer, string id)
    {
        SetLayerSelection(layer, id);
        RefreshPreview();
    }

    private void SetLayerSelection(CharacterLayerType layer, string id)
    {
        selectedIds[layer] = id ?? string.Empty;
        CharacterCapePairing.EnforcePairedCapes(selectedIds);
    }

    private void CreateFooter(RectTransform root)
    {
        RectTransform footer = CreatePanel(root, "Footer", new Vector2(0f, 0f), new Vector2(1f, 0.14f), new Color(0.06f, 0.07f, 0.1f, 1f));
        footer.SetAsLastSibling();

        Button saveButton = CreateButton(footer, "Salvar", new Color(0.18f, 0.62f, 0.38f, 1f), OnSave);
        RectTransform buttonRect = saveButton.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(280f, 64f);
        buttonRect.anchoredPosition = new Vector2(120f, 0f);

        Button backButton = CreateButton(footer, "Voltar", new Color(0.35f, 0.35f, 0.42f, 1f), OnBackToMenu);
        RectTransform backRect = backButton.transform as RectTransform;
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = new Vector2(220f, 64f);
        backRect.anchoredPosition = new Vector2(-180f, 0f);

        Outline outline = saveButton.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.35f, 0.2f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI buttonLabel = saveButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonLabel != null)
        {
            buttonLabel.fontSize = 22;
            buttonLabel.fontStyle = FontStyles.Bold;
        }
    }

    private void RefreshPreview()
    {
        if (previewAnimator != null)
            previewAnimator.PreviewAppearance(selectedIds);

        if (summaryText == null)
            return;

        StringBuilder builder = new();
        builder.AppendLine($"Nome: {GetCharacterName()}");
        builder.AppendLine($"Genero: {CharacterGenderUtility.GetDisplayName(selectedGender)}");

        foreach (CharacterLayerType layer in CharacterLayerDefinitions.CustomizationOrder)
        {
            string id = GetSelectedId(layer);
            if (string.IsNullOrEmpty(id) && layer != CharacterLayerType.Skin)
                continue;

            string label = CharacterLayerDefinitions.SummaryLabel(layer);
            string value = layer == CharacterLayerType.Skin || !string.IsNullOrEmpty(id)
                ? GetDisplayName(id)
                : "Nenhum";

            builder.AppendLine($"{label}: {value}");
        }

        summaryText.text = builder.ToString().TrimEnd();
    }

    private string GetSelectedId(CharacterLayerType layer)
    {
        return selectedIds.TryGetValue(layer, out string id) ? id : string.Empty;
    }

    private bool IsSelected(CharacterLayerType layer, string id)
    {
        return GetSelectedId(layer) == id;
    }

    private string GetDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id) || library == null)
            return "-";

        CharacterSpriteLibrary.SheetEntry entry = library.Entries.FirstOrDefault(item => item.id == id);
        return entry != null ? entry.displayName : id;
    }

    private string GetCharacterName()
    {
        string name = nameInput != null ? nameInput.text : CharacterProfileData.LoadName();
        return string.IsNullOrWhiteSpace(name) ? "Aventureiro" : name.Trim();
    }

    private void OnBackToMenu()
    {
        GameFlowState.StartNewCharacter = false;
        SceneManager.LoadScene(GameScenes.MainMenu);
    }

    private void OnSave()
    {
        CharacterCapePairing.EnforcePairedCapes(selectedIds);
        GameSaveData saveData = GameSaveData.FromSelection(GetCharacterName(), selectedGender, selectedIds);
        CharacterAppearanceData.Save(selectedIds);
        CharacterProfileData.Save(saveData.characterName, selectedGender);

        GameFlowState.PendingSave = saveData;
        GameFlowState.SaveSlotsPurpose = SaveSlotsPurpose.SaveCharacter;
        GameFlowState.StartNewCharacter = false;
        SceneManager.LoadScene(GameScenes.SaveSlots);
    }

    private static RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = color;
        return rect;
    }

    private static ScrollRect CreateScrollView(RectTransform parent, out Transform content)
    {
        GameObject scrollObject = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        scrollObject.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 1f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = ScrollWheelSensitivity;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;

        content = contentObject.transform;
        return scroll;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Text", label, 18, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
