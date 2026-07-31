using UnityEngine;

/// <summary>
/// Keeps play-mode statics clean when Enter Play Mode Options disables Domain Reload.
/// </summary>
public static class PlayModeStaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GameFlowState.ClearForDomainReload();
        GameTimeClock.ClearInstanceForDomainReload();
        GameTimeClock.ClearStaticEventsForDomainReload();
        DayNightLighting.ClearWeatherForDomainReload();
        RainWeatherSystem.ClearInstanceForDomainReload();
        WeatherDirector.ClearInstanceForDomainReload();
        GameSessionSave.ClearInstanceForDomainReload();
        DappledSunLighting.ClearInstanceForDomainReload();
        RainPixelTextures.ClearForDomainReload();
        PrismaBackpackMenu.ClearOpenFlag();
        DevModeController.ClearOpenFlag();
    }
}
