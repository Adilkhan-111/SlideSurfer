using UnityEngine;

/// <summary>The player's best score, kept between sessions.</summary>
public static class HighScore
{
    private const string Key = "best_score";

    public static int Best => PlayerPrefs.GetInt(Key, 0);

    /// <summary>Saves the score if it beats the best one; returns true for a new record.</summary>
    public static bool Submit(int score)
    {
        if (score <= Best) return false;
        PlayerPrefs.SetInt(Key, score);
        PlayerPrefs.Save();
        return true;
    }
}
