using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Saves em arquivos JSON sob persistentDataPath (sobrevive a rename do produto / reopen Unity).
/// Migra slots antigos de PlayerPrefs (Prisma e Engrenum).
/// </summary>
public static class GameSaveSystem
{
    public const int MaxSlots = 3;

    private const string SlotKeyPrefix = "prisma_save_slot_";
    private const string LegacySlotKeyPrefix = "engrenum_save_slot_";
    private const string MigratedFlagKey = "prisma_saves_migrated_v1";

    private static bool migrationAttempted;

    private static string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    private static string GetSlotPath(int slot) => Path.Combine(SavesDirectory, $"slot_{slot}.json");

    public static bool SlotExists(int slot)
    {
        ValidateSlot(slot);
        EnsureMigrated();
        GameSaveData data = LoadSlot(slot);
        return data != null && !data.IsEmpty;
    }

    public static GameSaveData LoadSlot(int slot)
    {
        ValidateSlot(slot);
        EnsureMigrated();

        string path = GetSlotPath(slot);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                    if (data != null)
                        return data;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Prisma: falha ao ler save do slot {slot}. {exception.Message}");
            }
        }

        return new GameSaveData();
    }

    public static void SaveSlot(int slot, GameSaveData data)
    {
        ValidateSlot(slot);
        EnsureMigrated();

        if (data == null || data.IsEmpty)
        {
            Debug.LogError("Prisma: tentativa de salvar slot vazio ignorada.");
            return;
        }

        data.savedAt = string.IsNullOrEmpty(data.savedAt)
            ? DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            : data.savedAt;

        try
        {
            Directory.CreateDirectory(SavesDirectory);
            string path = GetSlotPath(slot);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Prisma: falha ao gravar save do slot {slot}. {exception.Message}");
            return;
        }

        // Espelho em PlayerPrefs (compat), sem depender só disso.
        PlayerPrefs.SetString(GetSlotKey(slot), JsonUtility.ToJson(data));
        PlayerPrefs.DeleteKey(LegacySlotKeyPrefix + slot);
        PlayerPrefs.Save();
    }

    public static void ApplyToActiveProfile(GameSaveData data)
    {
        if (data == null || data.IsEmpty)
            return;

        CharacterAppearanceData.Save(data.ToAppearanceSelection());
        CharacterProfileData.Save(data.characterName, (CharacterGender)data.gender);
    }

    public static void DeleteSlot(int slot)
    {
        ValidateSlot(slot);
        EnsureMigrated();

        string path = GetSlotPath(slot);
        if (File.Exists(path))
            File.Delete(path);

        PlayerPrefs.DeleteKey(GetSlotKey(slot));
        PlayerPrefs.DeleteKey(LegacySlotKeyPrefix + slot);
        PlayerPrefs.Save();
    }

    private static void EnsureMigrated()
    {
        if (migrationAttempted)
            return;

        migrationAttempted = true;
        Directory.CreateDirectory(SavesDirectory);

        bool already = PlayerPrefs.GetInt(MigratedFlagKey, 0) == 1;
        bool anyFile = false;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (File.Exists(GetSlotPath(i)))
                anyFile = true;
        }

        if (already && anyFile)
            return;

        for (int slot = 0; slot < MaxSlots; slot++)
        {
            if (File.Exists(GetSlotPath(slot)))
                continue;

            string json = PlayerPrefs.GetString(GetSlotKey(slot), string.Empty);
            if (string.IsNullOrEmpty(json))
                json = PlayerPrefs.GetString(LegacySlotKeyPrefix + slot, string.Empty);

            if (string.IsNullOrEmpty(json))
                json = TryReadFromLegacyPrefsFile(slot);

            if (string.IsNullOrEmpty(json))
                continue;

            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null || data.IsEmpty)
                continue;

            try
            {
                File.WriteAllText(GetSlotPath(slot), JsonUtility.ToJson(data, true), Encoding.UTF8);
                Debug.Log($"Prisma: save do slot {slot + 1} migrado para disco.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Prisma: migração do slot {slot} falhou. {exception.Message}");
            }
        }

        PlayerPrefs.SetInt(MigratedFlagKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Lê prefs XML do produto Engrenum (Linux) se o rename perdeu os slots.
    /// </summary>
    private static string TryReadFromLegacyPrefsFile(int slot)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return string.Empty;

        string[] candidates =
        {
            Path.Combine(home, ".config", "unity3d", "DefaultCompany", "Engrenum", "prefs"),
            Path.Combine(home, ".config", "unity3d", "DefaultCompany", "Prisma", "prefs")
        };

        string[] keyNames =
        {
            SlotKeyPrefix + slot,
            LegacySlotKeyPrefix + slot
        };

        for (int c = 0; c < candidates.Length; c++)
        {
            string path = candidates[c];
            if (!File.Exists(path))
                continue;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            for (int k = 0; k < keyNames.Length; k++)
            {
                string decoded = ExtractPrefString(text, keyNames[k]);
                if (!string.IsNullOrEmpty(decoded))
                    return decoded;
            }
        }

        return string.Empty;
    }

    private static string ExtractPrefString(string prefsXml, string prefName)
    {
        // <pref name="prisma_save_slot_0" type="string">BASE64</pref>
        Match match = Regex.Match(
            prefsXml,
            $"<pref\\s+name=\"{Regex.Escape(prefName)}\"\\s+type=\"string\">([^<]*)</pref>",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return string.Empty;

        string raw = match.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        // Linux Editor grava strings em Base64.
        try
        {
            byte[] bytes = Convert.FromBase64String(raw);
            string decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.Contains("characterName") || decoded.StartsWith("{"))
                return decoded;
        }
        catch
        {
            // Pode já ser JSON puro.
        }

        return raw.StartsWith("{") ? raw : string.Empty;
    }

    private static string GetSlotKey(int slot) => SlotKeyPrefix + slot;

    private static void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), $"Slot must be between 0 and {MaxSlots - 1}.");
    }
}
