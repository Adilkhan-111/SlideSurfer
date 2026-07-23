using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIappearance : MonoBehaviour
{
    public static UIappearance Instance;

    public bool isGuidenceRevealed = false;
    private void OnEnable()
    {
        EventManager.GameHasStarted += GuidenceHasStarted;
    }
    private void OnDisable()
    {
        EventManager.GameHasStarted -= GuidenceHasStarted;
    }
    private void Awake()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void GuidenceHasStarted()
    {
        isGuidenceRevealed = true;
    }
}
