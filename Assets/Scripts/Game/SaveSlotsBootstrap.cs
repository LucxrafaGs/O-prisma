using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveSlotsBootstrap : MonoBehaviour
{
    private void Awake()
    {
        GameSettings.Apply();
        PrismaUIBuilder.EnsureEventSystem(transform);
        PrismaUIBuilder.EnsureCanvas(transform);
        CharacterLibraryAccess.WarmUp();

        if (GetComponent<SaveSlotsUI>() == null)
            gameObject.AddComponent<SaveSlotsUI>();
    }
}

public class SaveSlotsUI : MonoBehaviour
{
    private static readonly Color TitleGold = new(0.96f, 0.88f, 0.62f, 1f);
    private static readonly Color InkMuted = new(0.72f, 0.78f, 0.88f, 1f);
    private static readonly Color SlotFilled = new(0.2f, 0.4f, 0.62f, 1f);
    private static readonly Color SlotEmpty = new(0.2f, 0.23f, 0.3f, 1f);
    private static readonly Color SlotDisabled = new(0.14f, 0.15f, 0.18f, 1f);
    private static readonly Color Back = new(0.28f, 0.32f, 0.4f, 1f);

    private TextMeshProUGUI subtitleText;

    private void Start()
    {
        PrismaUIBuilder.EnsureEventSystem(transform);
        BuildInterface();
    }

    private void BuildInterface()
    {
        Canvas canvas = PrismaUIBuilder.EnsureCanvas(transform);
        RectTransform root = canvas.transform as RectTransform;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        Image bg = root.gameObject.GetComponent<Image>();
        if (bg == null)
            bg = root.gameObject.AddComponent<Image>();
        bg.sprite = PrismaUISprites.White;
        bg.color = new Color(0.05f, 0.07f, 0.11f, 1f);
        bg.raycastTarget = false;

        TextMeshProUGUI title = PrismaUIBuilder.CreateText(root, "Title", "Seus Saves", 52, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -64f);
        titleRect.sizeDelta = new Vector2(800f, 70f);
        title.color = TitleGold;
        title.fontStyle = FontStyles.Bold;

        subtitleText = PrismaUIBuilder.CreateText(root, "Subtitle", GetSubtitle(), 22, TextAlignmentOptions.Center);
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -140f);
        subtitleRect.sizeDelta = new Vector2(900f, 40f);
        subtitleText.color = InkMuted;

        RectTransform card = PrismaUIBuilder.CreateCard(root, "SlotsCard", new Vector2(920f, 520f), new Color(0.08f, 0.1f, 0.14f, 0.78f));
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, -10f);

        GameObject slotsPanel = new GameObject("SlotsPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
        slotsPanel.transform.SetParent(card, false);
        RectTransform slotsRect = slotsPanel.GetComponent<RectTransform>();
        slotsRect.anchorMin = Vector2.zero;
        slotsRect.anchorMax = Vector2.one;
        slotsRect.offsetMin = new Vector2(28f, 28f);
        slotsRect.offsetMax = new Vector2(-28f, -28f);

        VerticalLayoutGroup layout = slotsPanel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        for (int slot = 0; slot < GameSaveSystem.MaxSlots; slot++)
            CreateSlotButton(slotsRect, slot);

        Button back = PrismaUIBuilder.CreateStyledButton(root, "Voltar ao Menu", Back, OnBackToMenu, new Vector2(320f, 64f));
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 48f);
    }

    private string GetSubtitle()
    {
        return GameFlowState.SaveSlotsPurpose switch
        {
            SaveSlotsPurpose.SaveCharacter => "Escolha um slot para salvar seu personagem",
            SaveSlotsPurpose.SaveGame => "Escolha um slot para salvar seu progresso",
            _ => "Escolha um save para carregar"
        };
    }

    private void CreateSlotButton(RectTransform parent, int slotIndex)
    {
        GameSaveData data = GameSaveSystem.LoadSlot(slotIndex);
        bool hasSave = GameSaveSystem.SlotExists(slotIndex);
        bool saveMode = GameFlowState.SaveSlotsPurpose == SaveSlotsPurpose.SaveCharacter
            || GameFlowState.SaveSlotsPurpose == SaveSlotsPurpose.SaveGame;

        string label = hasSave
            ? $"Slot {slotIndex + 1}  ·  {data.characterName}\n{data.ProgressSummary}\n{data.savedAt}"
            : $"Slot {slotIndex + 1}  ·  Vazio";

        Color color = hasSave ? SlotFilled : SlotEmpty;
        if (!saveMode && !hasSave)
            color = SlotDisabled;

        Button button = PrismaUIBuilder.CreateStyledButton(parent, label, color, () => OnSlotSelected(slotIndex), new Vector2(0f, 120f));
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 120f;
        layout.preferredHeight = 120f;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(28f, 8f, 28f, 8f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -8f;
        }

        button.interactable = saveMode || hasSave;
    }

    private void OnSlotSelected(int slotIndex)
    {
        if (GameFlowState.SaveSlotsPurpose == SaveSlotsPurpose.SaveCharacter
            || GameFlowState.SaveSlotsPurpose == SaveSlotsPurpose.SaveGame)
        {
            GameSaveData saveData = GameFlowState.PendingSave;
            if (saveData == null || saveData.IsEmpty)
            {
                Debug.LogError("Prisma: nenhum personagem pendente para salvar. Volte e confirme o personagem.");
                return;
            }

            if (GameFlowState.SaveSlotsPurpose == SaveSlotsPurpose.SaveGame
                && GameSessionSave.Instance != null)
            {
                GameSaveData live = GameSessionSave.CaptureCurrent();
                if (!live.IsEmpty)
                    saveData = live;
            }

            GameSaveSystem.SaveSlot(slotIndex, saveData);
            GameSaveSystem.ApplyToActiveProfile(saveData);
            GameSessionSave.SetActiveSlot(slotIndex);
            GameFlowState.PendingSave = null;
            GameFlowState.PendingLoad = saveData;
            SceneManager.LoadScene(GameScenes.Game);
            return;
        }

        if (!GameSaveSystem.SlotExists(slotIndex))
            return;

        GameSaveData loaded = GameSaveSystem.LoadSlot(slotIndex);
        GameSaveSystem.ApplyToActiveProfile(loaded);
        GameSessionSave.SetActiveSlot(slotIndex);
        GameFlowState.PendingLoad = loaded;
        SceneManager.LoadScene(GameScenes.Game);
    }

    private void OnBackToMenu()
    {
        SceneManager.LoadScene(GameScenes.MainMenu);
    }
}
