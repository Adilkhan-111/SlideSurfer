using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PosGenerate : MonoBehaviour
{
    private List<float> _positions;

    public PosGenerate(List<float> positions)
    {
        _positions = positions;
    }


    public float GeneratePos()
    {

        int rnum = Random.Range(0, _positions.Count);
        return _positions[rnum];
    }
}
