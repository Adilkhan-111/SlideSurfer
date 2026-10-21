using UnityEngine;

/// <summary>Steering sensitivity, set in the settings window and kept between sessions.</summary>
public static class ControlSettings
{
    private const string Key = "sensitivity";
    public const float Min = 0.5f;
    public const float Max = 2f;

    private static float? cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    /// <summary>1 = default; 2 = the surfer moves twice as far for the same swipe.</summary>
    public static float Sensitivity
    {
        get
        {
            if (!cached.HasValue) cached = Mathf.Clamp(PlayerPrefs.GetFloat(Key, 1f), Min, Max);
            return cached.Value;
        }
        set
        {
            cached = Mathf.Clamp(value, Min, Max);
            PlayerPrefs.SetFloat(Key, cached.Value);
        }
    }
}
