using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PosGenerate
{
    private List<float> _positions;
    private int rIndex;
    public PosGenerate(List<float> positions)
    {
        _positions = positions;
    }


    public float GeneratePos()
    {

        rIndex = Random.Range(0, _positions.Count);
        return _positions[rIndex];
    }

    public float GetRotation()
    {
        if(_positions.Count == 3)
        {
            
            switch (rIndex)
            {
                case 0:
                    return -20f;
                    
                case 1:
                    return 0f;
                    
                case 2:
                    return 20f;
                    
                default:
                    return 0f;
                    
            }

                
        }
        else if(_positions.Count == 2)
        {
            switch (rIndex)
            {
                case 0:
                    return -45f;
                case 1:
                    return 0f;
                default:
                    return 0f;
            }
        }
        else if(_positions.Count == 1)
        {
            switch (rIndex)
            {
                case 0:
                    return 0f;
                default:
                    return 0f;

            }
        }
        else
        {
            return 0f;
        }
        

    }
}
