using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a Toggle look like a phone-style switch: the knob slides right and the track lights up when on.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ToggleSwitch : MonoBehaviour
{
    [SerializeField] private Image track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Color onColor = new Color(0.15f, 0.65f, 0.75f);
    [SerializeField] private Color offColor = new Color(0.75f, 0.79f, 0.82f);
    [SerializeField] private float knobTravel = 40f;
    [Tooltip("Switch animation speed (full slides per second)")]
    [SerializeField] private float speed = 8f;

    private Toggle toggle;
    private float t;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        t = toggle.isOn ? 1f : 0f;
        Apply();
    }

    private void OnEnable()
    {
        if (toggle != null) { t = toggle.isOn ? 1f : 0f; Apply(); }
    }

    private void Update()
    {
        float target = toggle.isOn ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        // unscaled: the switch also works while the game is paused
        t = Mathf.MoveTowards(t, target, speed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply()
    {
        if (track != null) track.color = Color.Lerp(offColor, onColor, t);
        if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(-knobTravel, knobTravel, Mathf.SmoothStep(0f, 1f, t)), 0f);
    }
}
