using System;
using UnityEngine;

/// <summary>
/// A coin riding down the slide (moved by GoPipeGo like the obstacles). Spins its model, is picked up by the player,
/// and quietly disappears if an obstacle ends up on top of it.
/// Needs a trigger collider and a kinematic Rigidbody on the same object.
/// </summary>
public class Coin : MonoBehaviour
{
    [Tooltip("The spinning part; the root must not rotate because GoPipeGo moves it in local space")]
    [SerializeField] private Transform model;
    [SerializeField] private float spinSpeed = 220f;
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private int value = 1;

    /// <summary>The coin left play: true when the player picked it up, false when it was hidden by an obstacle.</summary>
    public static event Action<Coin, bool> Removed;

    private Vector3 modelBase;
    private float phase;
    private bool removed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Removed = null;

    private void Awake()
    {
        if (model != null) modelBase = model.localPosition;
    }

    private void OnEnable()
    {
        removed = false;
        phase = UnityEngine.Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (model == null) return;
        model.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        model.localPosition = modelBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (removed) return;
        if (other.CompareTag("Player"))
        {
            removed = true;
            CoinWallet.Add(value);
            Removed?.Invoke(this, true);
        }
        else if (other.CompareTag("Obstacle"))
        {
            removed = true;
            Removed?.Invoke(this, false);
        }
    }
}
