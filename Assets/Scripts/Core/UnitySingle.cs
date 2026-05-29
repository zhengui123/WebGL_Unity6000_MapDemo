using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitySingle<T> : MonoBehaviour where T : UnitySingle<T>
{
    private static T instance;
    public static T Instance
    {
        set
        {
            instance = value;
        }
        get
        {
            if (instance == null)
            {
                string className = typeof(T).Name;
                GameObject obj = GameObject.Find(className);
                if (obj == null)
                {
                    obj = new GameObject(className);
                    instance = obj.AddComponent<T>();
                }

                instance = obj.GetComponent<T>();
                if (instance == null)
                {
                    instance = obj.AddComponent<T>();
                }
            }
            
            return instance;
        }
    }

    // public void Start()
    // {
    //     DontDestroyOnLoad(gameObject);
    // }
}
