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

    public virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[EventManager] 场景中存在多个实例，将销毁重复对象。");
            Destroy(gameObject);
            return;
        }

        instance = (T)this;
    }
    // public void Start()
    // {
    //     DontDestroyOnLoad(gameObject);
    // }
}
