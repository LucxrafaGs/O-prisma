using UnityEngine;

public static class GameSettings
{
    private const string MasterVolumeKey = "prisma_master_volume";
    private const string MusicVolumeKey = "prisma_music_volume";
    private const string SfxVolumeKey = "prisma_sfx_volume";
    private const string FullscreenKey = "prisma_fullscreen";
    private const string QualityKey = "prisma_quality";
    private const string VsyncKey = "prisma_vsync";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, PlayerPrefs.GetFloat("engrenum_master_volume", 1f));
        set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, PlayerPrefs.GetFloat("engrenum_music_volume", 0.8f));
        set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, PlayerPrefs.GetFloat("engrenum_sfx_volume", 1f));
        set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, PlayerPrefs.GetInt("engrenum_fullscreen", Screen.fullScreen ? 1 : 0)) == 1;
        set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    public static int QualityLevel
    {
        get => PlayerPrefs.GetInt(QualityKey, PlayerPrefs.GetInt("engrenum_quality", QualitySettings.GetQualityLevel()));
        set => PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, QualitySettings.names.Length - 1));
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(VsyncKey, PlayerPrefs.GetInt("engrenum_vsync", QualitySettings.vSyncCount > 0 ? 1 : 0)) == 1;
        set => PlayerPrefs.SetInt(VsyncKey, value ? 1 : 0);
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        QualitySettings.SetQualityLevel(QualityLevel, applyExpensiveChanges: true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Screen.fullScreen = Fullscreen;
        PlayerPrefs.Save();
    }
}
