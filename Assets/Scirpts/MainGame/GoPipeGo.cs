
using UnityEngine;

public class GoPipeGo : MonoBehaviour
{
    [SerializeField] private float ZBorderSpawnPipe;
    [SerializeField] private float ZBorderResetPipe;
    public GameSettings gameSettings;
    [SerializeField] private bool IsObstacle;
    [SerializeField] private bool IsShark;
    [SerializeField] private bool IsPlayerDead;
    [SerializeField] private float InitialHeight;
    
    
    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
        InitialHeight = 14f;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
    }
    private void Update()
    {
        if (IsPlayerDead) return;
        if(gameSettings != null & !IsShark)
        {
            transform.Translate(Vector3.back * gameSettings.currentSpeed  * Time.deltaTime);
        }
        else if (gameSettings != null & IsShark)
        {
            transform.Translate(Vector3.back * gameSettings.currentSpeed * 2 * Time.deltaTime);
        }
        //change pos for pipe not obstacle
        if (transform.position.z <= ZBorderResetPipe & !IsObstacle)
        {

            transform.position = new Vector3(transform.position.x, transform.position.y, ZBorderSpawnPipe);
        }
    }


    public void JumpForward()
    {
        Rigidbody rigid = transform.GetComponentInChildren<Rigidbody>();
        float jumpCofficient = (InitialHeight * InitialHeight) / gameSettings.currentSpeed;
        rigid.AddForce(Vector3.up * jumpCofficient, ForceMode.Impulse);
    }
    public void OnPlayerDied()
    {
        IsPlayerDead = true;
    }
}
