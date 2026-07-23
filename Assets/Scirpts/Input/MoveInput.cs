using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.VisualScripting;

public class MoveInput : MonoBehaviour
{
    [SerializeField] private bool isGrounded;
    
    private Rigidbody PlayerRb;
    [SerializeField] private float speed = 5f;
    private const float minX = -3.2f;
    private const float maxX = 3.2f;

    private void OnEnable()
    {
        InputManager.Instance.InputMapActions.Player.Movement.performed += Movement;
        EventManager.PlayerJumped += OnPlayerJumped;
        PlayerRb = gameObject.GetComponent<Rigidbody>();
        isGrounded = true;
    }
    private void OnDisable()
    {
        if (InputManager.Instance == null) return;  
        InputManager.Instance.InputMapActions.Player.Movement.performed -= Movement;
        EventManager.PlayerJumped -= OnPlayerJumped;
    }
    private void Movement(InputAction.CallbackContext ctx)
    {
        if (!isGrounded) return;
        
        Vector2 input = ctx.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0f, 0f) * speed * Time.deltaTime;
        Vector3 targetPosition = PlayerRb.position + move;
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        PlayerRb.MovePosition(targetPosition);
        EventManager.OnGameHasStarted();
        Debug.Log("asd" + Time.deltaTime);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.gameObject.CompareTag("Pipe"))
        {
            
            isGrounded = true;
        }
        if (collision.collider.gameObject.CompareTag("Obstacle"))
        {
            
            EventManager.OnPlayerDied();
            gameObject.SetActive(false);
        }
    }
    
    private void OnPlayerJumped()
    {
        
        isGrounded = false;
    }
}
