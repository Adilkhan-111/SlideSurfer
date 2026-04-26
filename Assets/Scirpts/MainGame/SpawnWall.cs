using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SpawnWall : MonoBehaviour
{
    private GameObject sw_prefab;
    private GameObject dw_prefab;
    private ObjectPool swPool;
    private ObjectPool dwPool;
    [SerializeField] float StartPosZ;
    [SerializeField] float EndPosZ;
    [SerializeField] List<float> SwPositionsX;
    [SerializeField] List<float> DwPositionsX;
    [SerializeField] List<GameObject> tempObj;
    private float count;
    [SerializeField] float SecBetween;
    private PosGenerate swGen;
    private bool isReady;

    private void Awake()
    {
        tempObj = new List<GameObject>();
        sw_prefab = Resources.Load<GameObject>("Prefabs/SingleWall");
        dw_prefab = Resources.Load<GameObject>("Prefabs/DoubleWall");
        swPool = new ObjectPool(sw_prefab, 3);
        dwPool = new ObjectPool(dw_prefab, 3);
        swGen = new PosGenerate(SwPositionsX);
        
        
        isReady = true;
    }

    
    private void Update()
    {
        
        if (isReady)
        {
            StartCoroutine(DelayCoroutine());
        }
        Check();

        /*GameObject obj = swPool.Get();
        float posX = swGen.GeneratePos();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        if(obj.transform.position.z <= EndPosZ)
        {
            swPool.Release(obj);
        }*/


    }


    private IEnumerator DelayCoroutine()
    {

        /*GameObject obj = swPool.Get();
        float posX = swGen.GeneratePos();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        tempObj.Add(obj);*/
        GenObj();


        isReady = false;
        yield return new WaitForSeconds(SecBetween);
        isReady = true;
        
    }


    public void GenObj()
    {
        //Debug.Log("attempt");
        
        GameObject obj = swPool.Get();
        float posX = swGen.GeneratePos();
        float rotZ = swGen.GetRotation();
        obj.transform.position = new Vector3(posX, obj.transform.position.y, StartPosZ);
        GetComponentInChildren<Transform>().localEulerAngles =  new Vector3(obj.transform.rotation.x, obj.transform.rotation.y, rotZ);
        //obj.transform.eulerAngles = new Vector3(obj.transform.rotation.x, obj.transform.rotation.y, rotZ);
        Debug.Log(rotZ);
        tempObj.Add(obj);
    }
    public void Check()
    {
        for (int i = 0; i < tempObj.Count; i++)
        {
            if (tempObj[i].transform.position.z <= EndPosZ)
            {

                swPool.Release(tempObj[i]);
                tempObj.RemoveAt(i);
            }
        }
    }
}
