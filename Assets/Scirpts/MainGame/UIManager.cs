using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    public Image defeatImage;
    public TextMeshProUGUI ScoreTXT;
    private ScoreCounter ScoreCt;
    

    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
        ScoreCt = gameObject.GetComponentInChildren<ScoreCounter>();
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
    }
    private void Update()
    {
        ScoreTXT.text = $"Score: {ScoreCt.Score}";
    }


    private void OnPlayerDied()
    {
        defeatImage.gameObject.SetActive(true);
    }

    public void Replay()
    {
        SceneManager.LoadScene(0);
    }
}
