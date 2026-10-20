using UnityEngine;

/// <summary>
/// Drives the surfer's Animator from the player's physics state:
/// "Lean" follows sideways speed, "Airborne"/"JumpSpeed" follow the jump over the pipe edge.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Sideways speed (units/sec) that gives the full lean")]
    [SerializeField] private float maxLeanSpeed = 4f;
    [SerializeField] private float leanSmoothTime = 0.12f;
    [Tooltip("Upward speed needed to count as a jump takeoff")]
    [SerializeField] private float takeoffVelocity = 1f;
    [Tooltip("Extra ground check distance below the capsule")]
    [SerializeField] private float groundCheckExtra = 0.3f;
    [SerializeField] private LayerMask groundMask = ~0;

    private static readonly int LeanId = Animator.StringToHash("Lean");
    private static readonly int AirborneId = Animator.StringToHash("Airborne");
    private static readonly int JumpSpeedId = Animator.StringToHash("JumpSpeed");
    private static readonly int TrickId = Animator.StringToHash("Trick");

    [Tooltip("How many jump tricks the animator has (Trick = 0..count-1)")]
    [SerializeField] private int trickCount = 3;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private float lastX;
    private float targetLean;
    private float lean;
    private float leanVelocity;
    private bool airborne;
    private int lastTrick = -1;
    private bool warned;

    /// <summary>True while the board is off the water during a jump.</summary>
    public bool IsAirborne => airborne;
    public event System.Action Jumped;
    public event System.Action Landed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        lastX = transform.position.x;
        targetLean = 0f;
        lean = 0f;
        leanVelocity = 0f;
        airborne = false;
    }

    // movement is applied with Rigidbody.MovePosition, so sample the real displacement per physics step
    private void FixedUpdate()
    {
        float x = rb.position.x;
        float vx = (x - lastX) / Time.fixedDeltaTime;
        lastX = x;
        targetLean = Mathf.Clamp(vx / maxLeanSpeed, -1f, 1f);
    }

    private void Update()
    {
        if (animator == null) return;
        if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            // say exactly what is wrong once instead of spamming SetFloat errors every frame
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("PlayerAnimator: animator '" + animator.name + "' cannot play: activeAndEnabled="
                    + animator.isActiveAndEnabled + ", controller=" + animator.runtimeAnimatorController, animator);
            }
            return;
        }

        lean =Mathf.SmoothDamp(lean, targetLean, ref leanVelocity, leanSmoothTime);
        animator.SetFloat(LeanId, lean);

        bool grounded = IsGrounded();
        if (!airborne)
        {
            // only an upward launch counts, so the drop at the start of a run doesn't play the trick
            if (!grounded && rb.linearVelocity.y > takeoffVelocity) StartJump();
        }
        else if (grounded && rb.linearVelocity.y <= 0.1f)
        {
            EndJump();
        }
    }

    private void StartJump()
    {
        airborne = true;
        // stretch the 1 second jump clip over the real flight time (up and back down)
        float airTime = 2f * rb.linearVelocity.y / Mathf.Abs(Physics.gravity.y);
        animator.SetFloat(JumpSpeedId, Mathf.Clamp(1f / Mathf.Max(airTime, 0.01f), 0.35f, 3f));
        animator.SetInteger(TrickId, PickTrick());
        animator.SetBool(AirborneId, true);
        Jumped?.Invoke();
    }

    // random trick, but never the same one twice in a row
    private int PickTrick()
    {
        int count = Mathf.Max(1, trickCount);
        int trick = Random.Range(0, count);
        if (count > 1 && trick == lastTrick) trick = (trick + 1 + Random.Range(0, count - 1)) % count;
        lastTrick = trick;
        return trick;
    }

    private void EndJump()
    {
        airborne = false;
        animator.SetBool(AirborneId, false);
        Landed?.Invoke();
    }

    private bool IsGrounded()
    {
        float scale = Mathf.Abs(transform.lossyScale.y);
        float radius = capsule.radius * Mathf.Abs(transform.lossyScale.x) * 0.9f;
        float halfHeight = Mathf.Max(capsule.height * 0.5f * scale, radius);
        // the pipe is curved, so cast a sphere instead of a ray to still hit the sloped sides
        return Physics.SphereCast(transform.position, radius, Vector3.down, out _,
            halfHeight - radius + groundCheckExtra, groundMask, QueryTriggerInteraction.Ignore);
    }
}
