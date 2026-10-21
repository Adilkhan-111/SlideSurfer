using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A simple message window ("Will be added soon!" and the like) with an OK button.
/// Tapping OK or the dimmed background closes it. Pops in with a small bounce.
/// </summary>
public class MessagePopup : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button okButton;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private float popTime = 0.25f;

    private float shownFor;

    private void Awake()
    {
        okButton.onClick.AddListener(Hide);
        if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
    }

    public void Show(string titleText, string messageText)
    {
        title.text = titleText;
        message.text = messageText;
        shownFor = 0f;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        gameObject.SetActive(false);
    }

    private void OnEnable() => UIInputGuard.PanelOpened();
    private void OnDisable() => UIInputGuard.PanelClosed();

    private void Update()
    {
        if (card == null) return;
        shownFor += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(shownFor / popTime);
        // overshoot a little, then settle
        float s = 1f + 0.12f * Mathf.Sin(t * Mathf.PI) * (1f - t) * 2f;
        card.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, t)) * s;
    }
}
