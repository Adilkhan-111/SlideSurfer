using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class SpawnObstacles : MonoBehaviour
{
    [Header("Obstacle prefabs")]
    public GameObject sw_prefab;
    public GameObject dw_prefab;
    public GameObject gw_prefab;
    public GameObject shark_prefab;
    [Header("Positions")]
    [SerializeField] float StartPosZ;
    [SerializeField] float EndPosZ;
    [SerializeField] List<float> SwPositionsX;
    [SerializeField] List<float> DwPositionsX;
    [SerializeField] List<float> GwPositionsX;
    [SerializeField] List<float> SharkPositionsX;

    [Header("GameSet, jumpNB and Sec between")]
    public float SecBetween = 5;
    public JumpNearBorder jnb;
    public GameSettings gameSettings;

    // Pacing follows the game design doc: almost nothing in the first seconds, casual until a minute,
    // ramp-up from a minute, peak around two minutes, hardcore from four. Time = seconds of the run (same as the score).
    [Header("Pacing")]
    [Tooltip("Slide speed over the run (seconds -> units/sec)")]
    public AnimationCurve SpeedOverTime = Linear(new Vector2(0f, 11f), new Vector2(60f, 17f), new Vector2(120f, 25f), new Vector2(240f, 33f), new Vector2(360f, 40f));
    [Tooltip("Seconds between obstacles over the run")]
    public AnimationCurve IntervalOverTime = Linear(new Vector2(0f, 2.4f), new Vector2(60f, 1.7f), new Vector2(120f, 1.25f), new Vector2(240f, 0.95f), new Vector2(360f, 0.8f));
    [Tooltip("No obstacles at the very start, only coins")]
    public float WarmUpTime = 6f;
    [Tooltip("The first full-width barrier comes around here, to teach the jump")]
    public float FirstGreatWallAt = 22f;
    [Tooltip("Score from which sharks start to appear; the first one is guaranteed right then")]
    public int SharkFromScore = 70;
    [Tooltip("Zig-zags (a second obstacle right behind, on another lane) start here...")]
    public float ZigZagFromTime = 100f;
    [Tooltip("...and become this likely by the hardcore phase")]
    [Range(0f, 1f)] public float ZigZagMaxChance = 0.35f;
    [Tooltip("Distance between the two obstacles of a zig-zag")]
    public float ZigZagGap = 14f;
    [Tooltip("Every so often the stream pauses and a coin trail rolls in instead (seconds between pauses)")]
    public Vector2 BreatherEvery = new Vector2(22f, 32f);
    public float BreatherLength = 3.5f;
    [Tooltip("Write every spawn to the Console, to check the rhythm")]
    public bool LogPacing;

    private enum Kind { Single, Double, Great, Shark }

    private float nextSpawnIn;
    private float nextBreatherIn;
    private float lastGreatAt = -999f;
    private bool firstSharkDone;
    private bool firstGreatDone;
    private Kind? lastKind, kindBeforeLast;
    private CoinSpawner coins;

    #region Object pools
    private ObjectPool swPool;
    private ObjectPool dwPool;
    private ObjectPool gwPool;
    private ObjectPool sharkPool;
    #endregion
    #region Position generators
    private PosGenerate swGen;
    private PosGenerate dwGen;
    private PosGenerate gwGen;
    private PosGenerate sharkGen;
    #endregion
    #region list for traking objects
    private List<(GameObject obj, ObjectPool pool)> tempObj;
    #endregion
    #region is player DEAD, has the game started
    private bool IsPlayerDead;
    private bool isGameHasStarted;
    #endregion
    #region var for traking time
    private float TempTemp;
    private float Score;
    #endregion
    private void OnEnable()
    {
        EventManager.PlayerDied += OnPlayerDied;
        EventManager.GameHasStarted += OnGameHasStarted;
    }
    private void OnDisable()
    {
        EventManager.PlayerDied -= OnPlayerDied;
        EventManager.GameHasStarted -= OnGameHasStarted;
    }
    private void Awake()
    {
        gameSettings.ResetValues();
        tempObj = new List<(GameObject obj, ObjectPool pool)>();
        sw_prefab = Resources.Load<GameObject>("Prefabs/SingleWall");
        dw_prefab = Resources.Load<GameObject>("Prefabs/DoubleWall");
        gw_prefab = Resources.Load<GameObject>("Prefabs/GreatWall");
        shark_prefab = Resources.Load<GameObject>("Prefabs/Shark");
        swPool = new ObjectPool(sw_prefab, 3);
        dwPool = new ObjectPool(dw_prefab, 3);
        gwPool = new ObjectPool(gw_prefab, 3);
        sharkPool = new ObjectPool(shark_prefab, 3);
        
        swGen = new PosGenerate(SwPositionsX);
        dwGen = new PosGenerate(DwPositionsX);
        gwGen = new PosGenerate(GwPositionsX);
        sharkGen = new PosGenerate(SharkPositionsX);
        
        coins = GetComponent<CoinSpawner>();
        nextSpawnIn = WarmUpTime;
        nextBreatherIn = Random.Range(BreatherEvery.x, BreatherEvery.y);
    }


    private void Update()
    {
        if (IsPlayerDead || !isGameHasStarted) return;

        TempTemp += Time.deltaTime;
        Score = Mathf.FloorToInt(TempTemp);

        // the slide's speed and the spawn rhythm come from the pacing curves
        gameSettings.currentSpeed = SpeedOverTime.Evaluate(TempTemp);
        gameSettings.currentSB = IntervalOverTime.Evaluate(TempTemp);

        Check();

        nextSpawnIn -= Time.deltaTime;
        if (nextSpawnIn > 0f) return;

        // a short breather now and then: no obstacles, a coin trail instead
        nextBreatherIn -= gameSettings.currentSB;
        if (nextBreatherIn <= 0f && TempTemp > WarmUpTime + 10f)
        {
            nextBreatherIn = Random.Range(BreatherEvery.x, BreatherEvery.y);
            nextSpawnIn = BreatherLength;
            if (coins != null) coins.SpawnTrailNow();
            if (LogPacing) Debug.Log($"[Pacing] {TempTemp:F1}s breather");
            return;
        }

        Kind kind = PickKind();
        float extra = 0f;
        switch (kind)
        {
            case Kind.Single:
                if (Random.value < ZigZagChance()) { SpawnZigZag(); extra = 0.35f; }
                else GenSWobs();
                break;
            case Kind.Double: GenDWobs(); break;
            case Kind.Great: GenGWobs(); extra = 0.6f; break;   // room to land after the jump
            case Kind.Shark: GenSHARKobs(); extra = 0.4f; break;
        }
        kindBeforeLast = lastKind;
        lastKind = kind;
        nextSpawnIn = gameSettings.currentSB + extra;
        if (LogPacing) Debug.Log($"[Pacing] {TempTemp:F1}s {kind} speed {gameSettings.currentSpeed:F1} next in {nextSpawnIn:F2}s");
    }

    private Kind PickKind()
    {
        // introduce each new threat once, at its moment, so the player notices it
        if (!firstGreatDone && TempTemp >= FirstGreatWallAt && GreatWallFair()) { firstGreatDone = true; return Kind.Great; }
        if (!firstSharkDone && TempTemp >= SharkFromScore) { firstSharkDone = true; return Kind.Shark; }

        float t = Mathf.InverseLerp(0f, 240f, TempTemp);
        float single = Mathf.Lerp(1f, 0.55f, t);
        float dbl = Mathf.Lerp(0.3f, 0.6f, t);
        float great = firstGreatDone && GreatWallFair() ? 0.18f : 0f;
        float shark = TempTemp >= SharkFromScore ? Mathf.Lerp(0.25f, 0.4f, Mathf.InverseLerp(SharkFromScore, SharkFromScore + 150f, TempTemp)) : 0f;

        // never the same kind three times in a row
        if (lastKind.HasValue && lastKind == kindBeforeLast)
        {
            switch (lastKind.Value)
            {
                case Kind.Single: single = 0f; break;
                case Kind.Double: dbl = 0f; break;
                case Kind.Great: great = 0f; break;
                case Kind.Shark: shark = 0f; break;
            }
        }

        float r = Random.value * (single + dbl + great + shark);
        if ((r -= single) < 0f) return Kind.Single;
        if ((r -= dbl) < 0f) return Kind.Double;
        if ((r -= great) < 0f) return Kind.Great;
        return Kind.Shark;
    }

    // a full-width barrier is only fair if the jump will be ready by the time it arrives
    private bool GreatWallFair()
    {
        if (TempTemp - lastGreatAt < jnb.JumpCooldown + 4f) return false;
        float travelTime = (StartPosZ + 4f) / Mathf.Max(1f, gameSettings.currentSpeed);
        return jnb.CooldownLeft < travelTime - 1f;
    }

    private float ZigZagChance()
    {
        if (TempTemp < ZigZagFromTime) return 0f;
        return ZigZagMaxChance * Mathf.InverseLerp(ZigZagFromTime, 240f, TempTemp);
    }

    // two single obstacles one after another on different lanes: dodge one way, then the other
    private void SpawnZigZag()
    {
        int first = Random.Range(0, swGen.Count);
        int second = (first + Random.Range(1, swGen.Count)) % swGen.Count;
        GenSWobsAt(first, 0f);
        GenSWobsAt(second, ZigZagGap);
    }

    // a curve made of straight segments through the given (time, value) points
    private static AnimationCurve Linear(params Vector2[] points)
    {
        var keys = new Keyframe[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float inSlope = i > 0 ? (points[i].y - points[i - 1].y) / (points[i].x - points[i - 1].x) : 0f;
            float outSlope = i < points.Length - 1 ? (points[i + 1].y - points[i].y) / (points[i + 1].x - points[i].x) : 0f;
            keys[i] = new Keyframe(points[i].x, points[i].y, inSlope, outSlope);
        }
        return new AnimationCurve(keys);
    }


    public void GenSWobs()
    {
        
        GameObject obj = swPool.Get();
        float posX = swGen.GeneratePos();
        float rotZ = swGen.GetRotationForWall();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles =  new Vector3(obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, rotZ);
        
        
        tempObj.Add((obj, swPool));
    }
    public void GenSWobsAt(int index, float extraZ)
    {
        GameObject obj = swPool.Get();
        float posX = swGen.PosAt(index);
        float rotZ = swGen.GetRotationForWall();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ + extraZ);
        obj.transform.eulerAngles = new Vector3(obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, rotZ);

        tempObj.Add((obj, swPool));
    }
    public void GenDWobs()
    {
        
        GameObject obj = dwPool.Get();
        float posX = dwGen.GeneratePos();
        float rotZ = dwGen.GetRotationForWall();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles = new Vector3(obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, rotZ);
        
        tempObj.Add((obj, dwPool));
    }
    public void GenGWobs()
    {
        //spawn a great wall
        GameObject obj = gwPool.Get();
        float posX = gwGen.GeneratePos();
        float rotZ = gwGen.GetRotationForWall();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles = new Vector3(obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, rotZ);

        tempObj.Add((obj, gwPool));
        // the next one waits for the jump cooldown (see GreatWallFair)
        lastGreatAt = TempTemp;
    }
    public void GenSHARKobs()
    {
        GameObject obj = sharkPool.Get();

        // which side it comes from: negative X = leaps out of the left wall, 0 = swims down the middle, positive = out of the right
        float posX = sharkGen.GeneratePos();
        int side = posX < -0.01f ? -1 : posX > 0.01f ? 1 : 0;
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.GetComponent<SharkMover>().Launch(side);

        tempObj.Add((obj, sharkPool));
    }



    public void Check()
    {
        for (int i = tempObj.Count - 1; i >= 0; i--)
        {
            var (obj, pool) = tempObj[i];
            if (obj.transform.position.z <= EndPosZ)
            {
                pool.Release(obj);
                tempObj.RemoveAt(i);
            }
        }
    }
    public void OnPlayerDied()
    {
        IsPlayerDead = true;
    }
    public void OnGameHasStarted()
    {
        isGameHasStarted = true;
    }
}
