using UnityEngine;

public static class FrameRate
{
    // mobile builds default to 30 fps, so ask for 60 explicitly (ignored when vSync is on)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Application.targetFrameRate = 60;
    }
}
