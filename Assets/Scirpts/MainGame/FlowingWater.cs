using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sheet of water lying in the pipe. The cross-section is taken from the pipe itself (rays shot down
/// onto colliders tagged "Pipe"), extruded along Z, and the water pattern scrolls toward the player
/// at the current game speed through the global _WaterFlowOffset used by the FlowingWater shader.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlowingWater : MonoBehaviour
{
    public GameSettings gameSettings;

    [Header("Flow")]
    [Tooltip("Flow speed before the run starts and after the player dies (units/sec)")]
    [SerializeField] private float idleFlowSpeed = 3f;

    [Header("Shape")]
    [SerializeField] private string pipeTag = "Pipe";
    [Tooltip("Z where the pipe shape is measured (the player's lane)")]
    [SerializeField] private float probeZ = -4f;
    [SerializeField] private float probeHalfWidth = 8f;
    [SerializeField] private int profileSamples = 64;
    [Tooltip("How high up the walls the water reaches, above the lowest point of the pipe")]
    [SerializeField] private float maxRise = 2.2f;
    [Tooltip("Water surface height above the pipe surface")]
    [SerializeField] private float surfaceLift = 0.04f;
    [SerializeField] private float startZ = -20f;
    [SerializeField] private float endZ = 150f;
    [SerializeField] private float segmentLength = 2f;

    [Header("Wake")]
    [Tooltip("Board bottom relative to the Player transform (the capsule is 2 tall)")]
    [SerializeField] private float boardOffsetY = -1f;
    [Tooltip("How fast the wake fades in and out when the board leaves or touches the water")]
    [SerializeField] private float wakeFadeSpeed = 6f;

    private static readonly int FlowOffsetId = Shader.PropertyToID("_WaterFlowOffset");
    private static readonly int PlayerPosId = Shader.PropertyToID("_WaterPlayerPos");

    private Transform player;
    private PlayerAnimator playerAnimator;
    private float wake;
    private float flowOffset;
    private bool running;
    private bool dead;
    private Mesh mesh;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EventManager.GameHasStarted += OnGameHasStarted;
            EventManager.PlayerDied += OnPlayerDied;
            running = EventManager.GameStarted;
            dead = false;
        }
        Build();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            EventManager.GameHasStarted -= OnGameHasStarted;
            EventManager.PlayerDied -= OnPlayerDied;
        }
        if (mesh != null) DestroyImmediate(mesh);
        mesh = null;
    }

    private void Update()
    {
        float speed = idleFlowSpeed;
        if (Application.isPlaying && running && !dead && gameSettings != null)
            speed = gameSettings.currentSpeed;

        flowOffset += speed * Time.deltaTime;
        Shader.SetGlobalFloat(FlowOffsetId, flowOffset);
        UpdateWake();
    }

    // tell the shader where the board is so it can draw the wake; the wake fades while the board is in the air
    private void UpdateWake()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go == null)
            {
                Shader.SetGlobalVector(PlayerPosId, new Vector4(0f, -1000f, 0f, 0f));
                return;
            }
            player = go.transform;
            playerAnimator = go.GetComponent<PlayerAnimator>();
        }

        bool touching = player.gameObject.activeInHierarchy && (playerAnimator == null || !playerAnimator.IsAirborne);
        float target = touching ? 1f : 0f;
        wake = Application.isPlaying ? Mathf.MoveTowards(wake, target, wakeFadeSpeed * Time.deltaTime) : target;

        Vector3 p = player.position + Vector3.up * boardOffsetY;
        Shader.SetGlobalVector(PlayerPosId, new Vector4(p.x, p.y, p.z, wake));
    }

    private void OnGameHasStarted() => running = true;
    private void OnPlayerDied() => dead = true;

    // ---------------------------------------------------------------- mesh

    [ContextMenu("Rebuild Water")]
    public void Build()
    {
        List<Vector3> points;
        List<Vector3> normals;
        if (!SampleProfile(out points, out normals))
        {
            Debug.LogWarning("FlowingWater: no colliders tagged '" + pipeTag + "' under z=" + probeZ + ", using a flat sheet", this);
            points = new List<Vector3> { new Vector3(-3f, 0.6f, 0f), new Vector3(3f, 0.6f, 0f) };
            normals = new List<Vector3> { Vector3.up, Vector3.up };
        }

        // u runs 0..1 along the cross-section by arc length, so foam at the edges has an even width
        int n = points.Count;
        var u = new float[n];
        float total = 0f;
        for (int i = 1; i < n; i++)
        {
            total += Vector2.Distance(new Vector2(points[i].x, points[i].y), new Vector2(points[i - 1].x, points[i - 1].y));
            u[i] = total;
        }
        for (int i = 0; i < n; i++) u[i] = total > 0f ? u[i] / total : (float)i / (n - 1);

        int rows = Mathf.Max(2, Mathf.CeilToInt((endZ - startZ) / segmentLength) + 1);
        var verts = new Vector3[rows * n];
        var norms = new Vector3[rows * n];
        var uvs = new Vector2[rows * n];
        for (int r = 0; r < rows; r++)
        {
            float z = Mathf.Lerp(startZ, endZ, (float)r / (rows - 1));
            for (int i = 0; i < n; i++)
            {
                Vector3 world = new Vector3(points[i].x, points[i].y, z) + normals[i] * surfaceLift;
                verts[r * n + i] = transform.InverseTransformPoint(world);
                norms[r * n + i] = transform.InverseTransformDirection(normals[i]);
                uvs[r * n + i] = new Vector2(u[i], z);
            }
        }

        var tris = new int[(rows - 1) * (n - 1) * 6];
        int t = 0;
        for (int r = 0; r < rows - 1; r++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                int a = r * n + i, b = a + 1, c = a + n, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "FlowingWater", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
        }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        Bounds b2 = mesh.bounds;
        b2.Expand(0.5f);   // room for the waves the shader adds
        mesh.bounds = b2;
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // walk across the pipe and keep the continuous stretch of surface around its lowest point
    private bool SampleProfile(out List<Vector3> points, out List<Vector3> normals)
    {
        points = new List<Vector3>();
        normals = new List<Vector3>();
        Physics.SyncTransforms();

        var hitPoints = new Vector3?[profileSamples];
        var hitNormals = new Vector3[profileSamples];
        int lowest = -1;
        for (int i = 0; i < profileSamples; i++)
        {
            float x = Mathf.Lerp(-probeHalfWidth, probeHalfWidth, (float)i / (profileSamples - 1));
            var origin = new Vector3(x, 100f, probeZ);
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag(pipeTag) || hit.point.y <= best) continue;
                best = hit.point.y;
                hitPoints[i] = hit.point;
                hitNormals[i] = hit.normal;
            }
            if (hitPoints[i].HasValue && (lowest < 0 || hitPoints[i].Value.y < hitPoints[lowest].Value.y)) lowest = i;
        }
        if (lowest < 0) return false;

        float limit = hitPoints[lowest].Value.y + maxRise;
        int from = lowest, to = lowest;
        while (from > 0 && hitPoints[from - 1].HasValue && hitPoints[from - 1].Value.y <= limit) from--;
        while (to < profileSamples - 1 && hitPoints[to + 1].HasValue && hitPoints[to + 1].Value.y <= limit) to++;
        if (to - from < 1) return false;

        for (int i = from; i <= to; i++)
        {
            points.Add(hitPoints[i].Value);
            normals.Add(hitNormals[i].normalized);
        }
        return true;
    }
}
