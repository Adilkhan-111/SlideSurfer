using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class ScoreCounter : MonoBehaviour
{

    public int WorkScore;
    public int Score;
    private float TempTemp;
    
    
    public GameSettings gameSettings;

    private bool isAllowed = true;
    private bool isGameHasStarted = false;

    private void OnEnable()
    {
        EventManager.GameHasStarted += OnGameHasStarted;
        EventManager.PlayerDied += OnPlayerDied;
        
    }
    
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
        EventManager.GameHasStarted -= OnGameHasStarted;
    }
    // Update is called once per frame


    
    void Update()
    {
        if (!isAllowed || !isGameHasStarted) return;
        
        

        TempTemp += Time.deltaTime;
        
        Score = Mathf.FloorToInt(TempTemp);
        // speed and spawn interval are set by SpawnObstacles from its pacing curves
    }


    public void OnPlayerDied()
    {
        isAllowed = false;
    }
    public void OnGameHasStarted()
    {
        isGameHasStarted = true;
    }
}
