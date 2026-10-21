using System;
using UnityEngine;

/// <summary>
/// The player's coins. <see cref="Total"/> is kept between sessions (PlayerPrefs) so the shop can spend it later;
/// <see cref="RunCoins"/> counts what was picked up in the current run.
/// Saving writes to disk, so it happens on death, when the app is paused or closed and after spending, not on every pickup.
/// </summary>
public static class CoinWallet
{
    private const string TotalKey = "coins_total";

    public static int Total { get; private set; }
    public static int RunCoins { get; private set; }

    /// <summary>Raised whenever <see cref="Total"/> or <see cref="RunCoins"/> changes.</summary>
    public static event Action Changed;

    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        loaded = false;
        RunCoins = 0;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        Total = PlayerPrefs.GetInt(TotalKey, 0);
        loaded = true;
    }

    public static void StartRun()
    {
        EnsureLoaded();
        RunCoins = 0;
        Changed?.Invoke();
    }

    public static void Add(int amount)
    {
        EnsureLoaded();
        RunCoins += amount;
        Total += amount;
        Changed?.Invoke();
    }

    /// <summary>For the shop: takes the coins and saves right away if there are enough.</summary>
    public static bool TrySpend(int amount)
    {
        EnsureLoaded();
        if (amount < 0 || Total < amount) return false;
        Total -= amount;
        Save();
        Changed?.Invoke();
        return true;
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.SetInt(TotalKey, Total);
        PlayerPrefs.Save();
    }
}
