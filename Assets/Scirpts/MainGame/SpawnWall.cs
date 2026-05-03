using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.VisualScripting;
using UnityEngine;

public class SpawnWall : MonoBehaviour
{
    private GameObject sw_prefab;
    private GameObject dw_prefab;
    private GameObject gw_prefab;
    private ObjectPool swPool;
    private ObjectPool dwPool;
    private ObjectPool gwPool;
    [SerializeField] float StartPosZ;
    [SerializeField] float EndPosZ;
    [SerializeField] List<float> SwPositionsX;
    [SerializeField] List<float> DwPositionsX;
    [SerializeField] List<float> GwPositionsX;
    [SerializeField] List<GameObject> tempObj;
    
    [SerializeField] float SecBetween;
    private PosGenerate swGen;
    private PosGenerate dwGen;
    private PosGenerate gwGen;
    [SerializeField] bool isAwaibleForG;
    private bool isReady;

    public JumpNearBorder jnb;
    

    private void Awake()
    {
        tempObj = new List<GameObject>();
        sw_prefab = Resources.Load<GameObject>("Prefabs/SingleWall");
        dw_prefab = Resources.Load<GameObject>("Prefabs/DoubleWall");
        gw_prefab = Resources.Load<GameObject>("Prefabs/GreatWall");
        swPool = new ObjectPool(sw_prefab, 3);
        dwPool = new ObjectPool(dw_prefab, 3);
        gwPool = new ObjectPool(gw_prefab, 3);
        
        swGen = new PosGenerate(SwPositionsX);
        dwGen = new PosGenerate(DwPositionsX);
        gwGen = new PosGenerate(GwPositionsX);

        isAwaibleForG = true;
        isReady = true;
    }

    
    private void Update()
    {
        
        if (isReady)
        {
            StartCoroutine(DelayCoroutine());
            
        }
        
        Check();

        


    }


    private IEnumerator DelayCoroutine()
    {
        int typeOfWall = Random.Range(0, 3);
        if (!isAwaibleForG)
        {
            //switch up to other type of wall cause we are waiting for jump cd complete
            typeOfWall = Random.Range(1, 3);

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
       
        isReady = false;
        yield return new WaitForSeconds(SecBetween);
        isReady = true;
        
    }


    public void GenSWobs()
    {
        
        GameObject obj = swPool.Get();
        float posX = swGen.GeneratePos();
        float rotZ = swGen.GetRotation();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles =  new Vector3(obj.transform.rotation.x, obj.transform.rotation.y, rotZ);
        
        
        tempObj.Add(obj);
    }
    public void GenDWobs()
    {
        
        GameObject obj = dwPool.Get();
        float posX = dwGen.GeneratePos();
        float rotZ = dwGen.GetRotation();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles = new Vector3(obj.transform.rotation.x, obj.transform.rotation.y, rotZ);
        
        tempObj.Add(obj);
    }
    public void GenGWobs()
    {
        //spawn a great wall
        GameObject obj = gwPool.Get();
        float posX = gwGen.GeneratePos();
        float rotZ = gwGen.GetRotation();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        obj.transform.eulerAngles = new Vector3(obj.transform.rotation.x, obj.transform.rotation.y, rotZ);

        tempObj.Add(obj);
        //and then do coroutine cause we need to wait until jump cd is over
        StartCoroutine(WaitForCd());
    }

    private IEnumerator WaitForCd()
    {
        //some coroutine for jump cd
        isAwaibleForG = false;
        yield return new WaitForSeconds(jnb.JumpCooldown);
        isAwaibleForG = true;
    }
    public void Check()
    {
        for (int i = 0; i < tempObj.Count; i++)
        {
            if (tempObj[i].transform.position.z <= EndPosZ)
            {

                swPool.Release(tempObj[i]);
                dwPool.Release(tempObj[i]);
                gwPool.Release(tempObj[i]);
                tempObj.RemoveAt(i);
            }
        }
    }
}
