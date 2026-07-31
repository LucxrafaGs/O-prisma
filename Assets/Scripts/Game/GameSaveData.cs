using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public string characterName = string.Empty;
    public int gender;
    public string savedAt = string.Empty;
    public string skin = CharacterAppearanceData.DefaultSkinId;
    public string back = string.Empty;
    public string outfit = string.Empty;
    public string cloak = string.Empty;
    public string face = string.Empty;
    public string hair = string.Empty;
    public string hat = string.Empty;
    public string tool = string.Empty;
    public string offHand = string.Empty;

    // Mundo / progresso
    public int year = 1;
    public int dayOfMonth = 1;
    public int season;
    public float dayElapsedRealSeconds;
    public bool sleepPending;
    public float playerX;
    public float playerY;
    public bool hasPlayerPosition;
    public bool isRaining;
    public bool isFoggy;
    public int hotbarIndex;
    public int saveVersion = 2;

    public bool IsEmpty => string.IsNullOrWhiteSpace(characterName);

    public string ProgressSummary
    {
        get
        {
            string seasonLabel = ((GameTimeClock.Season)season) switch
            {
                GameTimeClock.Season.Verao => "Verão",
                GameTimeClock.Season.Outono => "Outono",
                GameTimeClock.Season.Inverno => "Inverno",
                _ => "Primavera"
            };

            int totalMinutes = EstimateMinutesFromElapsed(dayElapsedRealSeconds);
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;
            return $"Ano {year} · Dia {dayOfMonth} · {seasonLabel} · {hour:00}:{minute:00}";
        }
    }

    public Dictionary<CharacterLayerType, string> ToAppearanceSelection()
    {
        return new Dictionary<CharacterLayerType, string>
        {
            [CharacterLayerType.Skin] = skin ?? CharacterAppearanceData.DefaultSkinId,
            [CharacterLayerType.Back] = back ?? string.Empty,
            [CharacterLayerType.Outfit] = outfit ?? string.Empty,
            [CharacterLayerType.Cloak] = cloak ?? string.Empty,
            [CharacterLayerType.Face] = face ?? string.Empty,
            [CharacterLayerType.Hair] = hair ?? string.Empty,
            [CharacterLayerType.Hat] = hat ?? string.Empty,
            [CharacterLayerType.Tool] = tool ?? string.Empty,
            [CharacterLayerType.OffHand] = offHand ?? string.Empty
        };
    }

    public static GameSaveData FromSelection(string name, CharacterGender genderValue, Dictionary<CharacterLayerType, string> selection)
    {
        selection ??= CharacterLayerDefinitions.CreateDefaultSelection();

        return new GameSaveData
        {
            characterName = name?.Trim() ?? string.Empty,
            gender = (int)genderValue,
            savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            skin = GetLayer(selection, CharacterLayerType.Skin),
            back = GetLayer(selection, CharacterLayerType.Back),
            outfit = GetLayer(selection, CharacterLayerType.Outfit),
            cloak = GetLayer(selection, CharacterLayerType.Cloak),
            face = GetLayer(selection, CharacterLayerType.Face),
            hair = GetLayer(selection, CharacterLayerType.Hair),
            hat = GetLayer(selection, CharacterLayerType.Hat),
            tool = GetLayer(selection, CharacterLayerType.Tool),
            offHand = GetLayer(selection, CharacterLayerType.OffHand),
            year = 1,
            dayOfMonth = 1,
            season = (int)GameTimeClock.Season.Primavera,
            dayElapsedRealSeconds = 0f,
            saveVersion = 2
        };
    }

    private static int EstimateMinutesFromElapsed(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / GameTimeClock.RealSecondsPerDay);
        int fromWake = Mathf.FloorToInt(t * GameTimeClock.WakeGameMinutes);
        int fromMidnight = GameTimeClock.WakeHour * 60 + fromWake;
        if (fromMidnight >= 24 * 60)
            fromMidnight -= 24 * 60;
        return fromMidnight;
    }

    private static string GetLayer(Dictionary<CharacterLayerType, string> selection, CharacterLayerType layer)
    {
        return selection.TryGetValue(layer, out string id) ? id ?? string.Empty : string.Empty;
    }
}
