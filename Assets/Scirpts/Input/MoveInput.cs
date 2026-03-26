using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class MoveInput : MonoBehaviour
{
    private void OnEnable()
    {
        InputManager.Instance.InputMapActions.Player.Movement.performed += Movement;
    }
    private void OnDisable()
    {
        if (InputManager.Instance == null) return;  
        InputManager.Instance.InputMapActions.Player.Movement.performed -= Movement;
    }
    private void Movement(InputAction.CallbackContext ctx)
    {
        Vector2 ourValue = ctx.ReadValue<Vector2>();
        Vector3 translation = new Vector3(ourValue.x, 0f, 0f) * Time.deltaTime;
        Vector3 newPosition = transform.position + translation;
        newPosition.x = Mathf.Clamp(newPosition.x, -3.2f, 3.2f);
        transform.position = newPosition;
        
        //Debug.Log($"OnTry, {ourValue}");
    }

    
}
