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

    private bool IsAllowed = true;
    
    private void OnEnable()
    {
         _SpeedMultiplier = 0.166666f;
        _SecMultiplier = 0.01875f;
        
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
        if(gameSettings.currentSpeed >= 50 || gameSettings.currentSB <= 0.5f)
        {
            return;
        }
        gameSettings.currentSpeed += _SpeedMultiplier * Time.deltaTime;
        gameSettings.currentSB -= _SecMultiplier * Time.deltaTime;
        Debug.Log(gameSettings.currentSB);
        
        


    }

    
    public void OnPlayerDied()
    {
        IsAllowed = false;
    }
}
