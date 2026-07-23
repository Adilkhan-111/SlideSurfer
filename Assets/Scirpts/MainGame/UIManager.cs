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
        if (ScoreCt != null && ScoreTXT != null)
        {
            ScoreTXT.text = $"Score: {ScoreCt.Score}";
        }
    }


    private void OnPlayerDied()
    {
        if (defeatImage != null)
            defeatImage.gameObject.SetActive(true);
    }

    public void Replay()
    {
        SceneManager.LoadScene(0);
    }

    public void OpenSettings()
    {
        
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
