
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawn;
    [SerializeField] private float ZBorderReset;
    public GameSettings gameSettings;
    [SerializeField] private bool IsObstacle;
    [SerializeField] private bool IsPlayerDead;
    
    private void Awake()
    {
        
    }
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
        if(gameSettings != null)
        {
            transform.Translate(Vector3.back * gameSettings.currentSpeed  * Time.deltaTime);
        }
        
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
