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
    private float _SpeedMultiplier;
    private float _SecMultiplier;

    private bool isAllowed = true;
    private bool isGameHasStarted = false;
    private bool isReachedMaxValue;

    private void OnEnable()
    {
         _SpeedMultiplier = 0.222222f;
        _SecMultiplier = 0.0138888f;
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
        if (!isReachedMaxValue)
        {
            gameSettings.currentSpeed = Mathf.Min(gameSettings.currentSpeed += _SpeedMultiplier * Time.deltaTime, gameSettings.maxSpeed);
            gameSettings.currentSB = Mathf.Max(gameSettings.currentSB -= _SecMultiplier * Time.deltaTime, gameSettings.minSB);
            CheckForMaxOrMin();
        }
        
        
        
        
        

        
    }

    public void CheckForMaxOrMin()
    {
        if (gameSettings.currentSpeed >= gameSettings.maxSpeed || gameSettings.currentSB <= gameSettings.minSB)
        {
            gameSettings.currentSpeed = gameSettings.maxSpeed;
            gameSettings.currentSB = gameSettings.minSB;
            isReachedMaxValue = true;
        }
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
