
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawnPipe;
    [SerializeField] private float ZBorderResetPipe;
    public GameSettings gameSettings;
    [SerializeField] private bool isObstacle;
    [SerializeField] private bool isShark;
    [SerializeField] private bool isPlayerDead;
    [SerializeField] private bool isGameHasStarted = false;
    [SerializeField] private float initialHeight;
    
    
    private void OnEnable()
    {
        
        EventManager.PlayerDied += OnPlayerDied;
        EventManager.GameHasStarted += OnGameHasStarted;
        // pooled objects are enabled after the start event has already fired
        isGameHasStarted = EventManager.GameStarted;
        initialHeight = 14f;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
        EventManager.GameHasStarted -= OnGameHasStarted;
    }
    private void Update()
    {
        if (isPlayerDead || !isGameHasStarted) return;
        if(gameSettings != null & !isShark)
        {
            transform.Translate(Vector3.back * gameSettings.currentSpeed  * Time.deltaTime);
        }
        else if (gameSettings != null & isShark)
        {
            transform.Translate(Vector3.back * gameSettings.currentSpeed * 2 * Time.deltaTime);
        }
        //change pos for pipe not obstacle
        if (transform.position.z <= ZBorderResetPipe & !isObstacle)
        {

            transform.position = new Vector3(transform.position.x, transform.position.y, ZBorderSpawnPipe);
        }
    }


    public void JumpForward()
    {
        Rigidbody rigid = transform.GetComponentInChildren<Rigidbody>();
        float jumpCofficient = (initialHeight * initialHeight) / gameSettings.currentSpeed;
        rigid.AddForce(Vector3.up * jumpCofficient, ForceMode.Impulse);
    }
    public void OnPlayerDied()
    {
        isPlayerDead = true;
    }
    public void OnGameHasStarted()
    {
        isGameHasStarted = true;
    }
}
