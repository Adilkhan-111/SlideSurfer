using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectPool
{
    private GameObject _prefab;
    private List<GameObject> _objects;

    public ObjectPool(GameObject prefab, int prewarmValue)
    {
        _prefab = prefab;
        _objects = new List<GameObject>();
        for(int i = 0; i < prewarmValue; i++)
        {
            GameObject obj = GameObject.Instantiate(prefab);
            obj.SetActive(false);
            _objects.Add(obj);
        }
    }

    public GameObject Get()
    {
        GameObject obj = null;
        for (int i = 0; i < _objects.Count; i++)
        {
            if (!_objects[i].activeInHierarchy)
            {
                obj = _objects[i];
                break;
            }
        }
        if (obj == null)
        {
            obj = Create();
        }
        obj.SetActive(true);
        return obj;
    }

    public void Release(GameObject obj)
    {
        // Reset Values
        var rb = obj.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            
            rb.isKinematic = true;
        }

        obj.SetActive(false);
    }

    public GameObject Create()
    {
        var obj = GameObject.Instantiate(_prefab);
        obj.SetActive(true);
        _objects.Add(obj);
        return obj;
    }
}
