using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    public Image defeatImage;
    public TextMeshProUGUI ScoreTXT;
    public GameObject SettingsUI;
    public GameObject InputGuideUI;
    private ScoreCounter ScoreCt;
    private int shownScore = -1;

    [Header("Best score")]
    [Tooltip("Small 'BEST 123' line under the score")]
    public TextMeshProUGUI BestTXT;
    [Tooltip("'NEW RECORD!' flash shown once when the best score is passed during a run")]
    public GameObject NewRecordBanner;
    [Tooltip("Score and best score on the defeat window")]
    public TextMeshProUGUI ResultTXT;
    [SerializeField] private float bannerTime = 2f;

    private int bestAtStart;
    private bool recordAnnounced;
    private float bannerLeft;


    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
        EventManager.GameHasStarted += OnGameHasStarted;
        ScoreCt = gameObject.GetComponentInChildren<ScoreCounter>();


    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
        EventManager.GameHasStarted -= OnGameHasStarted;
    }
    private void Start()
    {
        if (UIappearance.Instance.isGuidenceRevealed)
        {
            InputGuideUI.SetActive(false);
        }
        bestAtStart = HighScore.Best;
        if (BestTXT != null) BestTXT.text = $"BEST {bestAtStart}";
        if (NewRecordBanner != null) NewRecordBanner.SetActive(false);
    }
    private void Update()
    {
        // rebuilding TMP text every frame allocates and regenerates the mesh, so only touch it when the score changes
        if (ScoreCt != null && ScoreTXT != null && ScoreCt.Score != shownScore)
        {
            shownScore = ScoreCt.Score;
            ScoreTXT.text = $"Score: {shownScore}";
            OnScoreChanged(shownScore);
        }

        if (bannerLeft > 0f)
        {
            bannerLeft -= Time.unscaledDeltaTime;
            if (bannerLeft <= 0f && NewRecordBanner != null) NewRecordBanner.SetActive(false);
        }
    }

    // the best line follows the score once it is passed, and the first time that happens there is a little celebration
    private void OnScoreChanged(int score)
    {
        if (score <= bestAtStart) return;
        if (BestTXT != null) BestTXT.text = $"BEST {score}";
        if (!recordAnnounced && bestAtStart > 0 && NewRecordBanner != null)
        {
            recordAnnounced = true;
            NewRecordBanner.SetActive(true);
            bannerLeft = bannerTime;
        }
    }


    private void OnPlayerDied()
    {
        int score = ScoreCt != null ? ScoreCt.Score : 0;
        bool newRecord = HighScore.Submit(score);
        if (ResultTXT != null)
        {
            ResultTXT.text = newRecord && bestAtStart > 0
                ? $"NEW RECORD!\n<size=80%>SCORE {score}</size>"
                : $"SCORE {score}\n<size=80%>BEST {HighScore.Best}</size>";
        }
        if (defeatImage != null)
            defeatImage.gameObject.SetActive(true);
    }

    public void Replay()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        Time.timeScale = 1f;   // in case the run was paused from the settings
        EventManager.ResetState();
        SceneManager.LoadScene(0);
    }

    public void OpenSettings()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
        if (SettingsUI != null)
        {
            SettingsUI.SetActive(!SettingsUI.activeInHierarchy);
        }
    }
    public void OnGameHasStarted()
    {
        if (InputGuideUI != null )
        {
            InputGuideUI.SetActive(false);
        }
    }
    
}
