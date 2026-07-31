public enum SaveSlotsPurpose
{
    LoadGame,
    SaveCharacter,
    SaveGame
}

public static class GameFlowState
{
    public static bool StartNewCharacter;
    public static SaveSlotsPurpose SaveSlotsPurpose = SaveSlotsPurpose.LoadGame;
    public static GameSaveData PendingSave;
    public static GameSaveData PendingLoad;
    public static int ActiveSaveSlot = -1;

    public static void ClearForDomainReload()
    {
        StartNewCharacter = false;
        SaveSlotsPurpose = SaveSlotsPurpose.LoadGame;
        PendingSave = null;
        PendingLoad = null;
        ActiveSaveSlot = -1;
    }
}
