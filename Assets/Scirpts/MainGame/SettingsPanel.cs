using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings window: music and sound effects on/off with their volumes, and graphics quality.
/// Opening it during a run pauses the game. Everything is saved in PlayerPrefs.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Slider sfxSlider;
    [Tooltip("One button per quality level, lowest first")]
    [SerializeField] private Button[] qualityButtons;
    [SerializeField] private int[] qualityLevels = { 0, 1, 2 };
    [SerializeField] private Color qualitySelected = new Color(0.15f, 0.65f, 0.75f);
    [SerializeField] private Color qualityNormal = new Color(0.86f, 0.9f, 0.92f);
    [SerializeField] private Button closeButton;

    private const string QualityKey = "quality_level";
    private bool pausedGame;
    private bool refreshing;

    // apply the saved graphics quality before the first scene starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedQuality()
    {
        if (!PlayerPrefs.HasKey(QualityKey)) return;
        int level = PlayerPrefs.GetInt(QualityKey);
        if (level >= 0 && level < QualitySettings.names.Length) QualitySettings.SetQualityLevel(level, true);
    }

    private void Awake()
    {
        musicToggle.onValueChanged.AddListener(on => { if (refreshing) return; Click(); if (Audio != null) Audio.MusicOn = on; RefreshInteractable(); });
        sfxToggle.onValueChanged.AddListener(on => { if (refreshing) return; if (Audio != null) Audio.SfxOn = on; Click(); RefreshInteractable(); });
        musicSlider.onValueChanged.AddListener(v => { if (!refreshing && Audio != null) Audio.MusicVolume = v; });
        sfxSlider.onValueChanged.AddListener(v => { if (!refreshing && Audio != null) Audio.SfxVolume = v; });
        for (int i = 0; i < qualityButtons.Length; i++)
        {
            int index = i;
            qualityButtons[i].onClick.AddListener(() => SetQuality(index));
        }
        closeButton.onClick.AddListener(Close);
    }

    private static AudioManager Audio => AudioManager.Instance;

    private void OnEnable()
    {
        UIInputGuard.PanelOpened();
        // during a run the settings work as a pause menu
        if (EventManager.GameStarted && Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            pausedGame = true;
        }
        if (title != null) title.text = pausedGame ? "PAUSED" : "SETTINGS";
        Refresh();
    }

    private void OnDisable()
    {
        UIInputGuard.PanelClosed();
        if (pausedGame)
        {
            Time.timeScale = 1f;
            pausedGame = false;
        }
        PlayerPrefs.Save();
    }

    private void Refresh()
    {
        refreshing = true;
        if (Audio != null)
        {
            musicToggle.isOn = Audio.MusicOn;
            sfxToggle.isOn = Audio.SfxOn;
            musicSlider.value = Audio.MusicVolume;
            sfxSlider.value = Audio.SfxVolume;
        }
        refreshing = false;
        RefreshInteractable();
        RefreshQuality();
    }

    // a muted channel greys out its slider
    private void RefreshInteractable()
    {
        musicSlider.interactable = musicToggle.isOn;
        sfxSlider.interactable = sfxToggle.isOn;
    }

    private void SetQuality(int index)
    {
        Click();
        int level = Mathf.Clamp(qualityLevels[index], 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(level, true);
        PlayerPrefs.SetInt(QualityKey, level);
        RefreshQuality();
    }

    private void RefreshQuality()
    {
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < qualityButtons.Length; i++)
        {
            var img = qualityButtons[i].GetComponent<Image>();
            if (img != null) img.color = qualityLevels[i] == current ? qualitySelected : qualityNormal;
            var label = qualityButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = qualityLevels[i] == current ? Color.white : new Color(0.2f, 0.25f, 0.3f);
        }
    }

    private void Close()
    {
        Click();
        gameObject.SetActive(false);
    }

    private static void Click()
    {
        if (Audio != null) Audio.PlayClick();
    }
}
