using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop button in the bottom-right corner. For now it only says the shop is coming.
/// It is there while waiting to start and hides itself as soon as the run begins.
/// </summary>
[RequireComponent(typeof(Button))]
public class ShopButton : MonoBehaviour
{
    [SerializeField] private MessagePopup popup;
    [SerializeField] private string title = "SHOP";
    [SerializeField, TextArea] private string message = "Will be added soon!";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    private void OnEnable()
    {
        EventManager.GameHasStarted += Hide;
        if (EventManager.GameStarted) gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        EventManager.GameHasStarted -= Hide;
    }

    private void Open()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        if (popup != null) popup.Show(title, message);
    }

    private void Hide() => gameObject.SetActive(false);
}
