using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIanimations : MonoBehaviour
{
    [SerializeField] float amplitude = 1f;
    [SerializeField] float frequency = 1f;
    [SerializeField] float speed = 1f;
    [SerializeField] float Xsetter = 100f;
    // Update is called once per frame
    void Update()
    {
        float x = Mathf.Sin((Time.time * speed) * frequency) * amplitude;
        transform.position = new Vector3(x + Xsetter, transform.position.y, transform.position.z);
    }
}
