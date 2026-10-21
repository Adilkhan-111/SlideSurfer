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
    }
    private void Update()
    {
        // rebuilding TMP text every frame allocates and regenerates the mesh, so only touch it when the score changes
        if (ScoreCt != null && ScoreTXT != null && ScoreCt.Score != shownScore)
        {
            shownScore = ScoreCt.Score;
            ScoreTXT.text = $"Score: {shownScore}";
        }
    }


    private void OnPlayerDied()
    {
        if (defeatImage != null)
            defeatImage.gameObject.SetActive(true);
    }

    public void Replay()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
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
