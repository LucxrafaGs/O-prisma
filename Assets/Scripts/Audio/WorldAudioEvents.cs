using System;
using UnityEngine;

/// <summary>
/// Eventos de áudio do mundo (clima). <see cref="SoundsOfTheWorld"/> escuta estes sinais.
/// </summary>
public static class WorldAudioEvents
{
    public static event Action<bool> Thunder;
    public static event Action RainStarted;
    public static event Action RainStopped;

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
}
