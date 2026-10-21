using UnityEngine;

/// <summary>
/// Moves the shark toward the player (faster than the slide) along a controlled path instead of physics:
///   centre lane: swims straight down the middle of the slide, half out of the water;
///   side lane:   lurks hidden below the ground outside the slide, then leaps over the wall in an arc
///                and lands on the opposite side of the slide, where it keeps swimming at the player.
///                (Out of the left -> lands on the right, so the player dodges to the left or jumps.)
/// The path is tied to the distance in front of the player, so the leap always happens in view.
/// </summary>
public class SharkMover : MonoBehaviour
{
    public GameSettings gameSettings;

    [Header("Model")]
    [Tooltip("Whole body, swings side to side while swimming")]
    [SerializeField] private Transform model;
    [Tooltip("Tail fin, beats faster than the body")]
    [SerializeField] private Transform tail;
    [SerializeField] private float wiggleAngle = 7f;
    [SerializeField] private float tailAngle = 28f;
    [SerializeField] private float wiggleSpeed = 7f;

    [Header("Movement")]
    [Tooltip("Shark speed relative to the slide")]
    [SerializeField] private float speedMultiplier = 2f;
    [Tooltip("Body centre above the slide surface: about half the shark is in the water")]
    [SerializeField] private float rideHeight = 0.35f;

    [Header("Side leap")]
    [Tooltip("Where the shark waits outside the slide, distance from the middle")]
    [SerializeField] private float outsideX = 7.5f;
    [Tooltip("Waiting depth, below the ground so it can't be seen")]
    [SerializeField] private float hiddenY = -2f;
    [Tooltip("Where it lands on the other side of the slide, distance from the middle")]
    [SerializeField] private float landX = 2.2f;
    [Tooltip("Leap starts this far in front of the player")]
    [SerializeField] private float leapStartAhead = 48f;
    [Tooltip("...and lands this far in front of the player")]
    [SerializeField] private float leapEndAhead = 20f;
    [Tooltip("Extra height of the arc, enough to clear the slide wall")]
    [SerializeField] private float arcHeight = 7f;
    [Tooltip("How quickly the body turns to follow its path")]
    [SerializeField] private float turnSpeed = 10f;

    /// <summary>A shark started its run (for the warning sound).</summary>
    public static event System.Action Appeared;
    /// <summary>A side shark burst out of the water (for the splash sound).</summary>
    public static event System.Action Leaped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Appeared = null;
        Leaped = null;
    }

    private enum State { Swim, Lurk, Leap, Ride }

    private State state = State.Swim;
    private int lane;
    private float playerZ = -4f;
    private Vector2 leapFrom;
    private bool started;
    private bool dead;
    private float wigglePhase;

    private void OnEnable()
    {
        EventManager.GameHasStarted += OnGameHasStarted;
        EventManager.PlayerDied += OnPlayerDied;
        started = EventManager.GameStarted;
        dead = false;
        wigglePhase = Random.value * 10f;
    }

    private void OnDisable()
    {
        EventManager.GameHasStarted -= OnGameHasStarted;
        EventManager.PlayerDied -= OnPlayerDied;
    }

    private void OnGameHasStarted() => started = true;
    private void OnPlayerDied() => dead = true;

    /// <summary>Start a run from the current Z. side: -1 = out of the left, 0 = down the middle, +1 = out of the right.</summary>
    public void Launch(int side)
    {
        lane = side;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerZ = player.transform.position.z;

        Vector3 p = transform.position;
        if (lane == 0)
        {
            state = State.Swim;
            p.x = 0f;
            p.y = SlideProfile.SurfaceY(0f) + rideHeight;
        }
        else
        {
            state = State.Lurk;
            p.x = lane * outsideX;
            p.y = hiddenY;
        }
        transform.position = p;
        transform.rotation = SwimRotation(p.x);
        Appeared?.Invoke();
    }

    private void Update()
    {
        Wiggle();
        if (dead || !started) return;

        float speed = (gameSettings != null ? gameSettings.currentSpeed : 10f) * speedMultiplier;
        Vector3 from = transform.position;
        Vector3 p = from;
        p.z -= speed * Time.deltaTime;
        Quaternion wanted = transform.rotation;

        switch (state)
        {
            case State.Swim:
            case State.Ride:
            {
                float x = state == State.Swim ? 0f : -lane * landX;
                p.x = x;
                p.y = SlideProfile.SurfaceY(x) + rideHeight;
                wanted = SwimRotation(x);
                break;
            }
            case State.Lurk:
            {
                if (p.z <= playerZ + leapStartAhead)
                {
                    state = State.Leap;
                    leapFrom = new Vector2(p.x, p.y);
                    Leaped?.Invoke();
                }
                break;
            }
            case State.Leap:
            {
                float s = Mathf.InverseLerp(playerZ + leapStartAhead, playerZ + leapEndAhead, p.z);
                float toX = -lane * landX;
                float toY = SlideProfile.SurfaceY(toX) + rideHeight;
                p.x = Mathf.Lerp(leapFrom.x, toX, s);
                p.y = Mathf.Lerp(leapFrom.y, toY, s) + 4f * arcHeight * s * (1f - s);
                // nose follows the arc: up out of the water, level at the top, down into the landing
                Vector3 velocity = p - from;
                if (velocity.sqrMagnitude > 1e-6f) wanted = Quaternion.LookRotation(velocity, Vector3.up);
                if (s >= 1f) state = State.Ride;
                break;
            }
        }

        transform.position = p;
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    // facing the player, belly flat on the slide's curved surface
    private static Quaternion SwimRotation(float x) => Quaternion.LookRotation(Vector3.back, SlideProfile.SurfaceNormal(x));

    private void Wiggle()
    {
        float t = Time.time * wiggleSpeed + wigglePhase;
        if (model != null) model.localRotation = Quaternion.Euler(0f, Mathf.Sin(t) * wiggleAngle, 0f);
        if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t - 1.2f) * tailAngle, 0f);
    }
}
