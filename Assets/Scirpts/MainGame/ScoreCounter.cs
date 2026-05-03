using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ScoreCounter : MonoBehaviour
{

    public int WorkScore;
    public int Score;
    private float TempTemp;
    
    private bool IsAllowed = true;
    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
    }
    // Update is called once per frame
    void Update()
    {
        if (!IsAllowed) return;
        TempTemp += Time.deltaTime;
        Score = Mathf.FloorToInt(TempTemp);        
    }
    public void OnPlayerDied()
    {
        IsAllowed = false;
    }
}
