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

  /// <summary>板块界面 → 车辆界面过渡开始（参数为省份名）。</summary>
    public event Action<string> OnPlateToVehicleViewTransitionStarted;

    /// <summary>板块界面 → 车辆界面过渡全部完成（参数为省份名）。</summary>
    public event Action<string> OnPlateToVehicleViewTransitionCompleted;

    /// <summary>车辆界面 → 板块界面过渡开始（参数为二维地图省份名）。</summary>
    public event Action<string> OnVehicleToPlateViewTransitionStarted;

    /// <summary>车辆界面 → 板块界面过渡全部完成（参数为二维地图省份名）。</summary>
    public event Action<string> OnVehicleToPlateViewTransitionCompleted;

    /// <summary>车辆 → 零件过渡开始（参数为零件名）。</summary>
    public event Action<string> OnVehicleToPartTransitionStarted;

    /// <summary>车辆 → 零件过渡全部完成（参数为零件名）。</summary>
    public event Action<string> OnVehicleToPartTransitionCompleted;

    /// <summary>零件 → 车辆过渡倒播全部完成（参数为零件名）。</summary>
    public event Action<string> OnVehicleToPartTransitionReverseCompleted;

    #region 板块-车辆过渡动画
    /// <summary>AllPlateMap → GaodeMap 过渡开始（参数为省份名）。</summary>
    public event Action<string> OnPlateToGaodeMapTransitionStarted;

    /// <summary>AllPlateMap → GaodeMap 过渡全部完成（参数为省份名）。</summary>
    public event Action<string> OnPlateToGaodeMapTransitionCompleted;

    /// <summary>GaodeMap → AllPlateMap 倒放过渡开始（参数为省份名）。</summary>
    public event Action<string> OnGaodeMapToPlateTransitionStarted;

    /// <summary>GaodeMap → AllPlateMap 倒放过渡全部完成（参数为省份名）。</summary>
    public event Action<string> OnGaodeMapToPlateTransitionCompleted;

    /// <summary>GaodeMap → City-Maker 第二阶段过渡开始。</summary>
    public event Action OnGaodeMapToCityTransitionStarted;

    /// <summary>GaodeMap → City-Maker 第二阶段过渡全部完成。</summary>
    public event Action OnGaodeMapToCityTransitionCompleted;

    /// <summary>City-Maker → GaodeMap 第二阶段倒放开始。</summary>
    public event Action OnCityToGaodeMapTransitionReverseStarted;

    /// <summary>City-Maker → GaodeMap 第二阶段倒放全部完成。</summary>
    public event Action OnCityToGaodeMapTransitionReverseCompleted;

    /// <summary>请求正播两阶段过渡：板块 → GaodeMap → City-Maker（参数为省份名，可空）。</summary>
    public event Action<string> OnPlateToCityMapTransitionPlay;

    /// <summary>两阶段正播开始（参数为省份名）。</summary>
    public event Action<string> OnPlateToCityMapTransitionStarted;

    /// <summary>两阶段正播全部完成（参数为省份名）。</summary>
    public event Action<string> OnPlateToCityMapTransitionCompleted;

    /// <summary>请求倒播两阶段过渡：City-Maker → GaodeMap → 板块（参数为省份名，可空）。</summary>
    public event Action<string> OnCityToPlateMapTransitionReverse;

    /// <summary>两阶段倒播开始（参数为省份名）。</summary>
    public event Action<string> OnCityToPlateMapTransitionReverseStarted;

    /// <summary>两阶段倒播全部完成（参数为省份名）。</summary>
    public event Action<string> OnCityToPlateMapTransitionReverseCompleted;

    /// <summary>RealyCar → KJ_Car 车辆溶解切换全部完成。</summary>
    public event Action OnCarSwitchToKjCarCompleted;

    /// <summary>KJ_Car → RealyCar 车辆溶解切换全部完成。</summary>
    public event Action OnCarSwitchToRealyCarCompleted;



    #endregion

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

    public void TriggerPlateToGaodeMapTransitionStarted(string provinceName)
    {
        OnPlateToGaodeMapTransitionStarted?.Invoke(provinceName);
    }

    public void TriggerPlateToGaodeMapTransitionCompleted(string provinceName)
    {
        OnPlateToGaodeMapTransitionCompleted?.Invoke(provinceName);
    }

    public void TriggerGaodeMapToPlateTransitionStarted(string provinceName)
    {
        OnGaodeMapToPlateTransitionStarted?.Invoke(provinceName);
    }

    public void TriggerGaodeMapToPlateTransitionCompleted(string provinceName)
    {
        OnGaodeMapToPlateTransitionCompleted?.Invoke(provinceName);
    }

    public void TriggerGaodeMapToCityTransitionStarted()
    {
        OnGaodeMapToCityTransitionStarted?.Invoke();
    }

    public void TriggerGaodeMapToCityTransitionCompleted()
    {
        OnGaodeMapToCityTransitionCompleted?.Invoke();
    }

    public void TriggerCityToGaodeMapTransitionReverseStarted()
    {
        OnCityToGaodeMapTransitionReverseStarted?.Invoke();
    }

    public void TriggerCityToGaodeMapTransitionReverseCompleted()
    {
        OnCityToGaodeMapTransitionReverseCompleted?.Invoke();
    }

    public void TriggerPlateToCityMapTransitionPlay(string provinceName = null)
    {
        OnPlateToCityMapTransitionPlay?.Invoke(provinceName);
    }

    public void TriggerPlateToCityMapTransitionStarted(string provinceName)
    {
        OnPlateToCityMapTransitionStarted?.Invoke(provinceName);
    }

    public void TriggerPlateToCityMapTransitionCompleted(string provinceName)
    {
        OnPlateToCityMapTransitionCompleted?.Invoke(provinceName);
    }

    public void TriggerCityToPlateMapTransitionReverse(string provinceName = null)
    {
        OnCityToPlateMapTransitionReverse?.Invoke(provinceName);
    }

    public void TriggerCityToPlateMapTransitionReverseStarted(string provinceName)
    {
        OnCityToPlateMapTransitionReverseStarted?.Invoke(provinceName);
    }

    public void TriggerCityToPlateMapTransitionReverseCompleted(string provinceName)
    {
        OnCityToPlateMapTransitionReverseCompleted?.Invoke(provinceName);
    }

    public void TriggerCarSwitchToKjCarCompleted()
    {
        OnCarSwitchToKjCarCompleted?.Invoke();
    }

    public void TriggerCarSwitchToRealyCarCompleted()
    {
        OnCarSwitchToRealyCarCompleted?.Invoke();
    }

    public void TriggerVehicleToPartTransitionStarted(string partName)
    {
        OnVehicleToPartTransitionStarted?.Invoke(partName);
    }

    public void TriggerVehicleToPartTransitionCompleted(string partName)
    {
        OnVehicleToPartTransitionCompleted?.Invoke(partName);
    }

    public void TriggerVehicleToPartTransitionReverseCompleted(string partName)
    {
        OnVehicleToPartTransitionReverseCompleted?.Invoke(partName);
    }

    public void TriggerPlateToVehicleViewTransitionStarted(string provinceName)
    {
        OnPlateToVehicleViewTransitionStarted?.Invoke(provinceName);
    }

    public void TriggerPlateToVehicleViewTransitionCompleted(string provinceName)
    {
        OnPlateToVehicleViewTransitionCompleted?.Invoke(provinceName);
    }

    public void TriggerVehicleToPlateViewTransitionStarted(string provinceName)
    {
        OnVehicleToPlateViewTransitionStarted?.Invoke(provinceName);
    }

    public void TriggerVehicleToPlateViewTransitionCompleted(string provinceName)
    {
        OnVehicleToPlateViewTransitionCompleted?.Invoke(provinceName);
    }
}
