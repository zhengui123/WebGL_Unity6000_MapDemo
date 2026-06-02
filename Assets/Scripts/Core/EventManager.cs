using System;
using UnityEngine;

/// <summary>全局事件总线（单例）。</summary>
public class EventManager : UnitySingle<EventManager>
{
    /// <summary>板块模块开始聚焦（相机动画开始时，参数为模块名 / GameObject 名）。</summary>
    public event Action<string> OnPlateMapDisplayFocus;

    /// <summary>板块模块聚焦相机动画全部播放完毕。</summary>
    public event Action<string> OnPlateMapFocusModuleCompleted;

    /// <summary>板块相机还原动画全部播放完毕。</summary>
    public event Action OnPlateMapRestoreCameraCompleted;

    /// <summary>地球 → 板块过渡动画全部播放完毕。</summary>
    public event Action OnTransitionToPlateMapCompleted;

    /// <summary>板块 → 地球过渡动画全部播放完毕。</summary>
    public event Action OnTransitionToEarthCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EventManager] 场景中存在多个实例，将销毁重复对象。");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TriggerPlateMapDisplayFocus(string moduleName)
    {
        OnPlateMapDisplayFocus?.Invoke(moduleName);
    }

    public void TriggerPlateMapFocusModuleCompleted(string moduleName)
    {
        OnPlateMapFocusModuleCompleted?.Invoke(moduleName);
    }

    public void TriggerPlateMapRestoreCameraCompleted()
    {
        OnPlateMapRestoreCameraCompleted?.Invoke();
    }

    public void TriggerTransitionToPlateMapCompleted()
    {
        OnTransitionToPlateMapCompleted?.Invoke();
    }

    public void TriggerTransitionToEarthCompleted()
    {
        OnTransitionToEarthCompleted?.Invoke();
    }
}
