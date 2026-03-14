using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public InputMap InputMapActions;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("InputManager");
        Instance = go.AddComponent<InputManager>();
        DontDestroyOnLoad(go);
    }
    private void Awake()
    {
        if(Instance != null & Instance != this)
        {
            Destroy(Instance);
            return;
        }
        InputMapActions = new InputMap();
        InputMapActions.Enable();
    }

    private void OnDestroy()
    {
        if(Instance == this)
        {
            InputMapActions.Disable();
            InputMapActions = null;
            Instance = null;
        }
    }
    
    public void ChangeInputMap(InputType inputType)
    {
        switch (inputType)
        {
            case InputType.Player:
                InputMapActions.Player.Enable();
                InputMapActions.UI.Disable();
                break;
            case InputType.UI:
                InputMapActions.UI.Enable();
                InputMapActions.Player.Disable();
                break;
        }
    }
    public enum InputType
    {
        Player,UI
    }
}
