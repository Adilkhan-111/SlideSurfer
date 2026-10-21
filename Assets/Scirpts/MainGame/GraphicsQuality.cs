using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Graphics quality level: saved choice, extra per-level switches and a safety net.
///   post-processing (bloom, vignette, tonemapping) only on the highest level - full-screen passes are costly on phones,
///   with "Stop NaN" on, so one broken pixel can't be smeared by bloom over the whole screen;
///   the cheaper water shader variant on the lowest level.
/// Safety net: some phone GPUs draw a black, flickering screen on a level, and the player can't reach the settings
/// to undo it. So after start and after every change the level is "on trial": a few frames are checked, and if they
/// are mostly black (the game never is) the level drops by one. If the app is killed before the check finishes,
/// the next launch goes back to the default level.
/// </summary>
public static class GraphicsQuality
{
    private const string WaterLiteKeyword = "_WATER_LITE";
    // new key names: values saved by older builds (and restored by Android backup after reinstalling) are ignored
    private const string LevelKey = "graphics_level";
    private const string TrialKey = "graphics_level_trial";

    private static Checker checker;

    // apply the saved quality before the first scene starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSaved()
    {
        if (PlayerPrefs.HasKey(TrialKey))
        {
            // the last launch never confirmed its level (black screen and the app was closed): back to the default
            Debug.LogWarning("GraphicsQuality: level " + PlayerPrefs.GetInt(LevelKey, -1) + " was not confirmed last time, using the default");
            PlayerPrefs.DeleteKey(LevelKey);
            PlayerPrefs.DeleteKey(TrialKey);
            PlayerPrefs.Save();
            return;
        }
        int level = PlayerPrefs.GetInt(LevelKey, -1);
        if (level >= 0 && level < QualitySettings.names.Length) QualitySettings.SetQualityLevel(level, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
        StartTrial();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    /// <summary>Switch to a level from the settings, save it and check that it draws.</summary>
    public static void SetLevel(int level)
    {
        level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(level, true);
        PlayerPrefs.SetInt(LevelKey, level);
        Apply();
        StartTrial();
    }

    public static void Apply()
    {
        int level = QualitySettings.GetQualityLevel();
        int highest = QualitySettings.names.Length - 1;

        Camera cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = level >= highest;
                data.stopNaN = data.renderPostProcessing;
            }
        }

        if (level == 0) Shader.EnableKeyword(WaterLiteKeyword);
        else Shader.DisableKeyword(WaterLiteKeyword);
    }

    private static void StartTrial()
    {
        PlayerPrefs.SetInt(TrialKey, 1);
        PlayerPrefs.Save();
        if (checker == null)
        {
            var go = new GameObject("GraphicsQualityCheck");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            checker = go.AddComponent<Checker>();
        }
        checker.StopAllCoroutines();
        checker.StartCoroutine(checker.Check());
    }

    private static void Confirm()
    {
        PlayerPrefs.DeleteKey(TrialKey);
        PlayerPrefs.Save();
    }

    private static void StepDown()
    {
        int level = QualitySettings.GetQualityLevel();
        Debug.LogWarning("GraphicsQuality: level " + level + " draws a black screen, lowering it");
        if (level == 0)
        {
            // nothing lower to try
            Confirm();
            return;
        }
        SetLevel(level - 1);
    }

    private class Checker : MonoBehaviour
    {
        private const int Samples = 5;
        private const float FirstDelay = 1.5f;
        private const float Spacing = 0.4f;
        // part of the frame that has to be near-black to call it broken
        private const float BlackShare = 0.6f;

        // one odd capture (some drivers hand back an empty frame now and then) must not change the level
        public IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(FirstDelay);
            int black = 0;
            for (int i = 0; i < Samples; i++)
            {
                yield return new WaitForEndOfFrame();
                if (FrameIsBlack()) black++;
                yield return new WaitForSecondsRealtime(Spacing);
            }
            if (black > Samples / 2) StepDown();
            else Confirm();
        }

        private static bool FrameIsBlack()
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return false;
            const int grid = 24;
            int dark = 0;
            for (int y = 0; y < grid; y++)
            {
                for (int x = 0; x < grid; x++)
                {
                    Color c = shot.GetPixel((x * 2 + 1) * shot.width / (grid * 2), (y * 2 + 1) * shot.height / (grid * 2));
                    if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.06f) dark++;
                }
            }
            Destroy(shot);
            return dark > grid * grid * BlackShare;
        }
    }
}
