using System;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static  event Action PlayerJumped;
    public static void OnPlayerJumped()
    {
        PlayerJumped?.Invoke();
    }

    public static event Action PlayerDied;
    public static void OnPlayerDied()
    {
        PlayerDied?.Invoke();
    }

    public static bool GameStarted { get; private set; }
    public static event Action GameHasStarted;
    public static void OnGameHasStarted()
    {
        // called on every movement input, so fire the event only once per run
        if (GameStarted) return;
        GameStarted = true;
        GameHasStarted?.Invoke();
    }

    public static void ResetState()
    {
        GameStarted = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GameStarted = false;
        PlayerJumped = null;
        PlayerDied = null;
        GameHasStarted = null;
    }
}
