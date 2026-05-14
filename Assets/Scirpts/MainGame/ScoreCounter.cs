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
        gameSettings.currentSpeed += _SpeedMultiplier * Time.deltaTime;
        Debug.Log(gameSettings.currentSpeed);
        
        


    }

    
    public void OnPlayerDied()
    {
        IsAllowed = false;
    }
}
