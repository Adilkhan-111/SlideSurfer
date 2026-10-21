using UnityEngine;

/// <summary>
/// Sense of speed: the camera's field of view widens as the slide speeds up
/// (from about a minute and a half into the run, as the design doc asks).
/// </summary>
[RequireComponent(typeof(Camera))]
public class SpeedFov : MonoBehaviour
{
    public GameSettings gameSettings;
    [Tooltip("Speed where the effect starts")]
    [SerializeField] private float startSpeed = 20f;
    [Tooltip("Speed where the effect is at full strength")]
    [SerializeField] private float fullSpeed = 38f;
    [Tooltip("Extra field of view at full strength, degrees")]
    [SerializeField] private float extraFov = 9f;
    [SerializeField] private float smoothing = 2f;

    private Camera cam;
    private float baseFov;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
    }

    private void Update()
    {
        if (gameSettings == null) return;
        float k = Mathf.InverseLerp(startSpeed, fullSpeed, gameSettings.currentSpeed);
        float target = baseFov + extraFov * k * k;   // eases in, so the first change is gentle
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
