
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawn;
    [SerializeField] private float ZBorderReset;
    [SerializeField] private float Speed;
    private void Update()
    {
        transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        if(transform.position.z == ZBorderReset)
        {
            transform.position = new Vector3(transform.position.x, transform.position.x, ZBorderSpawn);
        }
    }
}
