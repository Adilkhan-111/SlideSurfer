using UnityEngine;

/// <summary>
/// Water spray thrown up by the board while riding, a small splash on take-off and a big one on landing.
/// The particle systems are built in code so nothing has to be set up by hand; only a material is needed.
/// </summary>
[RequireComponent(typeof(PlayerAnimator))]
public class SurferSpray : MonoBehaviour
{
    [SerializeField] private Material sprayMaterial;
    public GameSettings gameSettings;

    [Tooltip("Board bottom relative to the Player transform (the capsule is 2 tall)")]
    [SerializeField] private float boardOffsetY = -1f;
    [Tooltip("Spray particles per second at the starting speed")]
    [SerializeField] private float sprayRate = 45f;
    [SerializeField] private int takeoffBurst = 25;
    [SerializeField] private int landingBurst = 70;

    private PlayerAnimator playerAnimator;
    private ParticleSystem spray;
    private ParticleSystem splash;
    private bool running;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        spray = CreateSpray();
        splash = CreateSplash();
    }

    private void OnEnable()
    {
        playerAnimator.Jumped += OnJumped;
        playerAnimator.Landed += OnLanded;
        EventManager.GameHasStarted += OnGameHasStarted;
        running = EventManager.GameStarted;
    }

    private void OnDisable()
    {
        playerAnimator.Jumped -= OnJumped;
        playerAnimator.Landed -= OnLanded;
        EventManager.GameHasStarted -= OnGameHasStarted;
    }

    private void OnGameHasStarted() => running = true;

    private void Update()
    {
        // more spray the faster the water runs, none while the board is in the air
        float speedFactor = 0.4f;
        if (running && gameSettings != null && gameSettings.initialSpeed > 0f)
            speedFactor = gameSettings.currentSpeed / gameSettings.initialSpeed;

        var emission = spray.emission;
        emission.rateOverTime = playerAnimator.IsAirborne ? 0f : sprayRate * speedFactor;
    }

    private void OnJumped() => splash.Emit(takeoffBurst);
    private void OnLanded() => splash.Emit(landingBurst);

    // ---------------------------------------------------------------- particle systems

    private ParticleSystem NewSystem(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, boardOffsetY + 0.05f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // drops stay behind and wash away with the water
        main.gravityModifier = 1.2f;
        main.maxParticles = 400;
        main.playOnAwake = true;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sprayMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // a steady fan of drops off both rails of the board, kicked up and washed back by the current
    private ParticleSystem CreateSpray()
    {
        ParticleSystem ps = NewSystem("BoardSpray");
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 0.02f, 1.6f);   // the board footprint (long axis along Z)

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
        vel.y = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        vel.z = new ParticleSystem.MinMaxCurve(-6f, -3f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        ps.Play();
        return ps;
    }

    // bursts only: a crown of water around the board
    private ParticleSystem CreateSplash()
    {
        ParticleSystem ps = NewSystem("BoardSplash");
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.7f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // cone opens upward

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-4f, -2f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        ps.Play();
        return ps;
    }
}
