using UnityEngine;

/// <summary>Scales the object in with a little bounce every time it is shown.</summary>
public class PopIn : MonoBehaviour
{
    [SerializeField] private float time = 0.3f;
    private float shownFor;

    private void OnEnable()
    {
        shownFor = 0f;
        transform.localScale = Vector3.one * 0.5f;
    }

    private void Update()
    {
        if (shownFor >= time) return;
        shownFor += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(shownFor / time);
        float s = Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, t)) + 0.15f * Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.one * s;
    }
}
