using UnityEngine;

/// <summary>
/// Eventos de áudio do mundo (clima + eco).
/// </summary>
public static class WorldAudioEvents
{
    public static event System.Action<bool> Thunder;
    public static event System.Action RainStarted;
    public static event System.Action RainStopped;
    public static event System.Action EchoAppeared;

    public static void NotifyThunder(bool strong)
    {
        Thunder?.Invoke(strong);
    }

    public static void NotifyRainStarted()
    {
        RainStarted?.Invoke();
    }

    public static void NotifyRainStopped()
    {
        RainStopped?.Invoke();
    }

    public static void NotifyEchoAppeared()
    {
        EchoAppeared?.Invoke();
    }
}
