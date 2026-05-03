using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    public Image defeatImage;


    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
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
