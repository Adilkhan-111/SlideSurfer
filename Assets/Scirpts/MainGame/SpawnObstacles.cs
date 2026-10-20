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
    #region Is awaible for GREAT WALL//SHARK, is player DEAD and is ready for JUMP bools
    private bool isAwaibleForG;
    private bool isAwaibleForS;
    private bool isReady;
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
        
        isAwaibleForG = true;
        isAwaibleForS = false;
        isReady = true;
    }

    
    private void Update()
    {
        if (IsPlayerDead || !isGameHasStarted) return;

        TempTemp += Time.deltaTime;

        Score = Mathf.FloorToInt(TempTemp);

        if(Score >= 150) 
        {
            isAwaibleForG = false;
        }

        if(Score >= 150 & !isAwaibleForS) // 100 actually
        {
            isAwaibleForS = true;
        }

        if (isReady)
        {
            StartCoroutine(DelayCoroutine());
            
        }
        
        Check();

        


    }


    private IEnumerator DelayCoroutine()
    {
        int typeOfWall = Random.Range(0, 4);
        
        if (!isAwaibleForG)
        {
            //switch up to other type of wall cause we are waiting for jump cd complete
            typeOfWall = Random.Range(1, 4);

        }
        if (!isAwaibleForS)
        {
            typeOfWall = Random.Range(0, 3);
        }

        if(typeOfWall == 0 & isAwaibleForG)
        {
            //do it only when allowed
            
            GenGWobs();
           
        }
        
        else if(typeOfWall == 1)
        {
            GenSWobs();
            
        }

        else if(typeOfWall == 2)
        {
            GenDWobs();
            
        }
        else if(typeOfWall == 3 & isAwaibleForS)
        {
            GenSHARKobs();
        }

            isReady = false;
        yield return new WaitForSeconds(gameSettings.currentSB);
        isReady = true;
        
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
        //and then do coroutine cause we need to wait until jump cd is over
        StartCoroutine(WaitForCd());
    }
    public void GenSHARKobs()
    {
        GameObject obj = sharkPool.Get();

        
        Rigidbody objRB = obj.GetComponentInChildren<Rigidbody>();

        // Reset
        if (objRB != null)
        {
            //objRB.velocity = Vector3.zero;
            //objRB.angularVelocity = Vector3.zero;
            
            objRB.isKinematic = true;
        }

        float posX = sharkGen.GeneratePos();
        float rotY = sharkGen.GetRotationForShark();

        obj.transform.eulerAngles = new Vector3(obj.transform.eulerAngles.x, rotY, obj.transform.eulerAngles.z);
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        


        GoPipeGo objGPG = obj.GetComponent<GoPipeGo>();

        if (posX == SharkPositionsX[0] || posX == SharkPositionsX[2])
        {
            if (objRB != null)
            {
                
                objRB.isKinematic = false;
                objRB.linearVelocity = Vector3.zero;
                objRB.angularVelocity = Vector3.zero;
            }
            objGPG.JumpForward();
        }
        else
        {
            if (objRB != null)
            {
                obj.transform.position = new Vector3(posX, 2, StartPosZ);
                objRB.isKinematic = true;
            }
        }
        
        tempObj.Add((obj, sharkPool));
    }



    private IEnumerator WaitForCd()
    {
        //some coroutine for jump cd
        isAwaibleForG = false;
        //multiply by 2 because cooldown on Great wall doesnt work proper, and seems like it waits 2 times less
        int cForG = jnb.JumpCooldown * 2;
        yield return new WaitForSeconds(cForG);
        
        isAwaibleForG = true;

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
