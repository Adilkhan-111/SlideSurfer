using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class JumpNearBorder : MonoBehaviour
{
    public float JumpCooldown;
    [SerializeField] private float jumpForce;
    private bool canJump = true;
    private bool readyForAn;
    private float jumpCount = 0;
    private float toBeSureCounter = 11;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") & canJump)
        {
            
            jumpCount += 1;
            
            Rigidbody player_rb = other.gameObject.GetComponent<Rigidbody>();
            player_rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            readyForAn = true;
            toBeSureCounter = 0;
            StartCoroutine(CooldownCoroutine());
            
        }
    }

    private void Update()
    {
        if (toBeSureCounter <= 10 || readyForAn)
        {
            Debug.Log($"tobesureCounter: {toBeSureCounter}");
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
