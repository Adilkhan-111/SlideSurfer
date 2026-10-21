using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Keeps touches meant for the UI away from the game: the run starts on any finger movement, so dragging a slider
/// or tapping the shop button must not steer the surfer or start the run. Blocks game input while a menu window is
/// open and for a touch that began on a UI element. Put one on the Canvas.
/// </summary>
public class UIInputGuard : MonoBehaviour
{
    private static int openPanels;
    private static bool touchOnUI;

    public static bool Blocked => openPanels > 0 || touchOnUI;

    public static void PanelOpened() => openPanels++;
    public static void PanelClosed() => openPanels = Mathf.Max(0, openPanels - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openPanels = 0;
        touchOnUI = false;
    }

    private void Awake()
    {
        // a fresh scene (restart) starts with no windows open
        openPanels = 0;
        touchOnUI = false;
    }

    // after the UI has handled this frame's pointers, so it knows what the finger is over
    private void LateUpdate()
    {
        Touchscreen touch = Touchscreen.current;
        if (touch == null) return;
        var primary = touch.primaryTouch;
        if (primary.press.wasPressedThisFrame)
            touchOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        else if (!primary.press.isPressed)
            touchOnUI = false;
    }
}
