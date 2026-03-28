using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.VisualScripting;

public class MoveInput : MonoBehaviour
{
    [SerializeField] private bool isGrounded; 
    private void OnEnable()
    {
        InputManager.Instance.InputMapActions.Player.Movement.performed += Movement;
        EventManager.PlayerJumped += OnPlayerJumped;
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
        Vector2 ourValue = ctx.ReadValue<Vector2>();
        Vector3 translation = new Vector3(ourValue.x, 0f, 0f) * Time.deltaTime;
        Vector3 newPosition = transform.position + translation;
        newPosition.x = Mathf.Clamp(newPosition.x, -3.2f, 3.2f);
        transform.position = newPosition;
        
        //Debug.Log($"OnTry, {ourValue}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.gameObject.CompareTag("Pipe"))
        {
            Debug.Log("isGrounded");
            isGrounded = true;
        }
        else
        {
            Debug.Log("isNOTGrounded");
            isGrounded = false;
        }
    }
    private void OnPlayerJumped()
    {
        isGrounded = false;
    }
}
