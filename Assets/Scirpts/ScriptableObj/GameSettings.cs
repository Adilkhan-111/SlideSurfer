using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu]
public class GameSettings : ScriptableObject
{
    public float initialSpeed;
    public float initialSB;

    public float maxSpeed;
    public float minSB;

    [System.NonSerialized]
    public float currentSpeed;
    [System.NonSerialized]
    public float currentSB;

    public void ResetValues()
    {
        
        currentSpeed = initialSpeed;
        currentSB = initialSB;
    }
}
