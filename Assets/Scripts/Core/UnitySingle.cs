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
                // 优先按组件在场景中查找，避免 GameObject 名称与类名不一致时误创建空实例。
                instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);

                if (instance == null)
                {
                    string className = typeof(T).Name;
                    GameObject obj = GameObject.Find(className);
                    if (obj != null)
                    {
                        instance = obj.GetComponent<T>();
                    }
                }

                if (instance == null)
                {
                    string className = typeof(T).Name;
                    GameObject obj = new GameObject(className);
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
            Debug.LogWarning($"[{typeof(T).Name}] 场景中存在多个实例，将销毁重复对象：{gameObject.name}");
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
