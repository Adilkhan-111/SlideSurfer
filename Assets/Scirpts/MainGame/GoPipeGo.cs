
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawn;
    [SerializeField] private float ZBorderReset;
    [SerializeField] private float Speed;
    [SerializeField] private bool IsObstacle;
    [SerializeField] private bool IsPlayerDead;
    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
    }
    private void Update()
    {
        if (IsPlayerDead) return;

        transform.Translate(Vector3.back * Speed * Time.deltaTime);
        if(transform.position.z <= ZBorderReset & !IsObstacle)
        {
            
            transform.position = new Vector3(transform.position.x, transform.position.y, ZBorderSpawn);
        }
    }

    public void OnPlayerDied()
    {
        IsPlayerDead = true;
    }
}
