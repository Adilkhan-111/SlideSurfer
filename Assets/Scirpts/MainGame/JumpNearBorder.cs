using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class JumpNearBorder : MonoBehaviour
{
    [SerializeField] private float JumpCooldown;
    [SerializeField] private float jumpForce;
    private bool canJump = true;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") & canJump)
        {
            Debug.Log("trigger");
            Rigidbody player_rb = other.gameObject.GetComponent<Rigidbody>();
            player_rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            EventManager.OnPlayerJumped();
            StartCoroutine(CooldownCoroutine());
        }    
    }

    private IEnumerator CooldownCoroutine()
    {
        canJump = false;
        yield return new  WaitForSeconds(JumpCooldown);
        canJump = true;
    }

}
