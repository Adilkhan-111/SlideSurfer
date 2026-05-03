using System;
using System.Collections;
using System.Collections.Generic;
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
}
