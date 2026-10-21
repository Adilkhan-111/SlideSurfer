using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lays trails of coins down the slide. Trails are never straight: they snake from side to side,
/// sweep across the slide in an arc, or curl in and out, always following the curved floor of the pipe.
/// Coins move toward the player like the obstacles (GoPipeGo on the prefab) and come back to a pool.
/// </summary>
public class CoinSpawner : MonoBehaviour
{
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private Material sparkleMaterial;

    [Header("Where")]
    [SerializeField] private float spawnZ = 85f;
    [SerializeField] private float despawnZ = -30f;
    [SerializeField] private float coinSpacing = 2.4f;
    [Tooltip("Coins stay within this distance from the middle (the player reaches 3.2)")]
    [SerializeField] private float maxX = 2.7f;
    [Tooltip("Coin centre above the slide surface; the player's centre is 1 m up")]
    [SerializeField] private float heightAboveSurface = 1f;
    [Tooltip("Z where the shape of the slide is measured (the player's lane)")]
    [SerializeField] private float probeZ = -4f;
    [SerializeField] private string pipeTag = "Pipe";

    [Header("When")]
    [SerializeField] private float firstDelay = 2.5f;
    [SerializeField] private Vector2 secondsBetweenTrails = new Vector2(2.5f, 5f);
    [SerializeField] private Vector2Int coinsPerTrail = new Vector2Int(7, 13);

    private ObjectPool pool;
    private readonly List<GameObject> active = new List<GameObject>();
    private readonly List<Vector2> profile = new List<Vector2>();   // (x, surface y) across the slide
    private ParticleSystem sparkle;
    private bool running;
    private bool dead;
    private float timer;

    private void Awake()
    {
        CoinWallet.StartRun();
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Prefabs/Coin");
        pool = new ObjectPool(coinPrefab, 30);
        sparkle = CreateSparkle();
    }

    private void OnEnable()
    {
        EventManager.GameHasStarted += OnGameHasStarted;
        EventManager.PlayerDied += OnPlayerDied;
        Coin.Removed += OnCoinRemoved;
        running = EventManager.GameStarted;
        timer = firstDelay;
    }

    private void OnDisable()
    {
        EventManager.GameHasStarted -= OnGameHasStarted;
        EventManager.PlayerDied -= OnPlayerDied;
        Coin.Removed -= OnCoinRemoved;
    }

    private void Start() => MeasureSlide();

    private void OnGameHasStarted() => running = true;

    private void OnPlayerDied()
    {
        dead = true;
        CoinWallet.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) CoinWallet.Save();
    }

    private void OnApplicationQuit() => CoinWallet.Save();

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].transform.position.z < despawnZ) Release(i);
        }

        if (!running || dead) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        SpawnTrail();
        timer = Random.Range(secondsBetweenTrails.x, secondsBetweenTrails.y);
    }

    // ---------------------------------------------------------------- trails

    private void SpawnTrail()
    {
        int count = Random.Range(coinsPerTrail.x, coinsPerTrail.y + 1);
        int shape = Random.Range(0, 3);
        float side = Random.value < 0.5f ? -1f : 1f;
        float amp = Random.Range(0.9f, maxX);
        float freq = Random.Range(0.35f, 0.6f);          // radians per coin: how tight the curve is
        float phase = Random.Range(0f, Mathf.PI * 2f);
        float centre = Random.Range(-0.4f, 0.4f) * maxX;

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            float x;
            switch (shape)
            {
                case 0:   // snake: a gentle S from side to side
                    x = centre + amp * Mathf.Sin(phase + i * freq);
                    break;
                case 1:   // sweep: one smooth arc from one wall to the other
                    x = side * maxX * 0.85f * Mathf.Cos(t * Mathf.PI);
                    break;
                default:  // curl: tight swings that settle toward the middle
                    x = centre * t + amp * (1f - t * 0.6f) * Mathf.Sin(phase + i * freq * 1.6f);
                    break;
            }
            x = Mathf.Clamp(x, -maxX, maxX);
            float z = spawnZ + i * coinSpacing;
            Place(new Vector3(x, SurfaceY(x) + heightAboveSurface, z));
        }
    }

    private void Place(Vector3 position)
    {
        GameObject coin = pool.Get();
        coin.transform.SetPositionAndRotation(position, Quaternion.identity);
        active.Add(coin);
    }

    private void OnCoinRemoved(Coin coin, bool collected)
    {
        if (collected) sparkle.Emit(new ParticleSystem.EmitParams { position = coin.transform.position, applyShapeToPosition = true }, 18);
        int i = active.IndexOf(coin.gameObject);
        if (i >= 0) Release(i);
    }

    private void Release(int index)
    {
        pool.Release(active[index]);
        active.RemoveAt(index);
    }

    // ---------------------------------------------------------------- slide shape

    // shoot rays down across the slide once, so coins can hug its curved floor
    private void MeasureSlide()
    {
        profile.Clear();
        for (float x = -maxX - 0.5f; x <= maxX + 0.5f; x += 0.25f)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 100f, probeZ), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag(pipeTag) && hit.point.y > best) best = hit.point.y;
            }
            if (best > float.MinValue) profile.Add(new Vector2(x, best));
        }
        if (profile.Count == 0)
        {
            Debug.LogWarning("CoinSpawner: no '" + pipeTag + "' colliders under z=" + probeZ + ", coins use a flat floor", this);
            profile.Add(new Vector2(0f, 0.6f));
        }
    }

    private float SurfaceY(float x)
    {
        if (x <= profile[0].x) return profile[0].y;
        for (int i = 1; i < profile.Count; i++)
        {
            if (x <= profile[i].x)
                return Mathf.Lerp(profile[i - 1].y, profile[i].y, Mathf.InverseLerp(profile[i - 1].x, profile[i].x, x));
        }
        return profile[profile.Count - 1].y;
    }

    // ---------------------------------------------------------------- pickup sparkle

    private ParticleSystem CreateSparkle()
    {
        var go = new GameObject("CoinSparkle");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 1f, 0.8f));
        main.gravityModifier = 0.4f;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
