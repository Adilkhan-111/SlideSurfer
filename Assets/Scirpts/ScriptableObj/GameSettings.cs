using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu]
public class GameSettings : ScriptableObject
{
    public float initialSpeed;

    [System.NonSerialized]
    public float currentSpeed;

    public void Reset()
    {
        currentSpeed = initialSpeed;
    }
}
