using TMPro;
using UnityEngine;

/// <summary>
/// Coin counter at the bottom of the screen: how many coins this run, with a little bounce on every pickup.
/// </summary>
public class CoinCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("What bounces when a coin is picked up (usually the whole counter)")]
    [SerializeField] private RectTransform bounceTarget;
    [SerializeField] private float bounceScale = 0.3f;
    [SerializeField] private float bounceTime = 0.25f;

    private int shown = -1;
    private float bounce;

    private void OnEnable()
    {
        CoinWallet.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        CoinWallet.Changed -= Refresh;
    }

    private void Refresh()
    {
        int coins = CoinWallet.RunCoins;
        if (shown >= 0 && coins > shown) bounce = 1f;
        if (coins == shown) return;
        shown = coins;
        if (label != null) label.text = coins.ToString();
    }

    private void Update()
    {
        if (bounce <= 0f || bounceTarget == null) return;
        bounce = Mathf.Max(0f, bounce - Time.unscaledDeltaTime / bounceTime);
        // quick pop up, ease back down
        float s = 1f + bounceScale * Mathf.Sin(bounce * Mathf.PI) * bounce;
        bounceTarget.localScale = Vector3.one * s;
    }
}
