
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawn;
    [SerializeField] private float ZBorderReset;
    [SerializeField] private float Speed;
    [SerializeField] private bool IsObstacle;
    private void Update()
    {
        transform.Translate(Vector3.back * Speed * Time.deltaTime);
        if(transform.position.z <= ZBorderReset & !IsObstacle)
        {
            Debug.Log("Paparazi");
            transform.position = new Vector3(transform.position.x, transform.position.y, ZBorderSpawn);
        }
    }
}
