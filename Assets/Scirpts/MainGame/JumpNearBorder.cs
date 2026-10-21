using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class JumpNearBorder : MonoBehaviour
{
    public int JumpCooldown = 8;
    [SerializeField] private float jumpForce;
    private bool canJump = true;
    private bool readyForAn;
    
    private float toBeSureCounter = 11;
    private float readyTime;

    /// <summary>Seconds until the jump can be used again (0 when it is ready).</summary>
    public float CooldownLeft => Mathf.Max(0f, readyTime - Time.time);

    private void Start()
    {
        JumpCooldown = 8;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") & canJump)
        {
            
            
            
            Rigidbody player_rb = other.gameObject.GetComponent<Rigidbody>();
            player_rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            readyForAn = true;
            toBeSureCounter = 0;
            readyTime = Time.time + JumpCooldown;
            StartCoroutine(CooldownCoroutine());
            
        }
    }

    private void Update()
    {
        if (toBeSureCounter <= 10 || readyForAn)
        {
            
            EventManager.OnPlayerJumped();
            readyForAn = false;
            toBeSureCounter++;
        }
    }

    private IEnumerator CooldownCoroutine()
    {
        canJump = false;
        
        yield return new  WaitForSeconds(JumpCooldown);
        canJump = true;
    }

}
