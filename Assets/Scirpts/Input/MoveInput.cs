using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.VisualScripting;

public class MoveInput : MonoBehaviour
{
    [SerializeField] private bool isGrounded;
    
    private Rigidbody PlayerRb;
    [Tooltip("How far the surfer moves for a swipe across the whole screen width (at sensitivity 1)")]
    [SerializeField] private float unitsPerScreenWidth = 18f;
    private const float minX = -3.2f;
    private const float maxX = 3.2f;
    // finger movement collected between physics steps
    private float pendingPixels;

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
        // touches on menus and buttons are not steering
        if (UIInputGuard.Blocked) return;

        // a touch delta is already "pixels moved since last time": no Time.deltaTime here, or the surfer would
        // move further on a slow phone. Several deltas can arrive per frame, so they are summed and applied in FixedUpdate.
        Vector2 input = ctx.ReadValue<Vector2>();
        if (Mathf.Approximately(input.x, 0f)) return;
        pendingPixels += input.x;
        EventManager.OnGameHasStarted();
    }

    private void FixedUpdate()
    {
        if (pendingPixels == 0f) return;
        // in fractions of the screen width, so it feels the same on every phone
        float dx = pendingPixels / Mathf.Max(1, Screen.width) * unitsPerScreenWidth * ControlSettings.Sensitivity;
        pendingPixels = 0f;
        Vector3 targetPosition = PlayerRb.position + new Vector3(dx, 0f, 0f);
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        PlayerRb.MovePosition(targetPosition);
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
