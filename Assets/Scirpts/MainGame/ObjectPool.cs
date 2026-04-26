

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
        var obj = _objects.FirstOrDefault(x => !x.activeInHierarchy);
        if (obj == null)
        {
            obj = Create();
        }
        obj.SetActive(true);
        return obj;
    }

    public void Release(GameObject obj)
    {
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
