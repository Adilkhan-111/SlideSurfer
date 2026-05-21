using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu]
public class GameSettings : ScriptableObject
{
    public float initialSpeed;
    public float initialSB;

    [System.NonSerialized]
    public float currentSpeed;
    [System.NonSerialized]
    public float currentSB;

    public void Reset()
    {
        currentSpeed = initialSpeed;
        currentSB = initialSB;
    }
}
