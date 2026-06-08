using UnityEngine;

/// <summary>
/// 地图对外 API：触发地球/板块过渡，并可通过 <see cref="EventManager"/> 订阅动画结束事件。
/// </summary>
public class MapApi : UnitySingle<MapApi>
{

    /// <summary>播放地球 → 板块过渡。</summary>
    public void TransitionToPlateMap()
    {
        Debug.Log("[MapApi] TransitionToPlateMap");
        EarthTransition earthTransition = EarthTransition.Instance;
        if (earthTransition == null)
        {
            Debug.LogWarning("[MapApi] 未找到 EarthTransition，无法切换到板块地图。");
            return;
        }

        earthTransition.TransitionToPlateMap();
    }

    /// <summary>播放板块 → 地球过渡。</summary>
    public void TransitionToEarth()
    {
        EarthTransition earthTransition = EarthTransition.Instance;
        if (earthTransition == null)
        {
            Debug.LogWarning("[MapApi] 未找到 EarthTransition，无法切换到地球。");
            return;
        }

        earthTransition.TransitionToEarth();
    }

    /// <summary>
    /// 聚焦指定板块模块,默认对应场景中 GameObject 名，如 polySurface2）。
    /// </summary>
    public bool FocusPlateMapModule(string moduleName)
    {
        PlateMapDisplayController controller = PlateMapDisplayController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapDisplayController，无法聚焦模块。");
            return false;
        }

        return controller.FocusModule(moduleName);
    }

    /// <summary>还原板块相机至首次聚焦前的位姿。</summary>
    public bool RestorePlateMapCamera()
    {
        PlateMapDisplayController controller = PlateMapDisplayController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapDisplayController，无法还原相机。");
            return false;
        }

        return controller.RestoreCameraPosition();
    }

    /// <summary>正播两阶段：板块 → GaodeMap → City-Maker。</summary>
    public bool TransitionPlateMapToCity(string provinceName = null)
    {
        PlateToCityMapTransitionOrchestrator orchestrator = PlateToCityMapTransitionOrchestrator.Instance;
        if (orchestrator == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateToCityMapTransitionOrchestrator。");
            return false;
        }

        return orchestrator.PlayFullTransition(provinceName);
    }

    /// <summary>倒播两阶段：City-Maker → GaodeMap → 板块。</summary>
    public bool TransitionCityToPlateMap(string provinceName = null)
    {
        PlateToCityMapTransitionOrchestrator orchestrator = PlateToCityMapTransitionOrchestrator.Instance;
        if (orchestrator == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateToCityMapTransitionOrchestrator。");
            return false;
        }

        return orchestrator.PlayFullTransitionReverse(provinceName);
    }

    /// <summary>通过事件请求正播两阶段过渡。</summary>
    public void RequestPlateToCityMapTransitionPlay(string provinceName = null)
    {
        EventManager.Instance?.TriggerPlateToCityMapTransitionPlay(provinceName);
    }

    /// <summary>通过事件请求倒播两阶段过渡。</summary>
    public void RequestCityToPlateMapTransitionReverse(string provinceName = null)
    {
        EventManager.Instance?.TriggerCityToPlateMapTransitionReverse(provinceName);
    }


}
