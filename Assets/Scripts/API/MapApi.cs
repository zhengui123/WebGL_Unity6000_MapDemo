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

    /// <summary>高亮指定板块模块，其余板块取消高亮（压低透明度）。</summary>
    public bool HighlightPlateMapModule(string moduleName)
    {
        PlateMapHighlightController controller = PlateMapHighlightController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapHighlightController，无法高亮模块。");
            return false;
        }

        return controller.HighlightModule(moduleName);
    }

    /// <summary>取消全部板块高亮。</summary>
    public void ClearPlateMapHighlight()
    {
        PlateMapHighlightController controller = PlateMapHighlightController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapHighlightController，无法取消高亮。");
            return;
        }

        controller.ClearHighlight();
    }

    /// <summary>正播完整阶段：板块 → 车辆视图。</summary>
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

    /// <summary>倒播完整阶段：车辆视图 → 板块。</summary>
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

    /// <summary>打开车辆 UI 与连线（仅 VehicleLevel 生效）。</summary>
    /// <param name="start3DObjectName">三维物体名称，对应 GridLine 配置 Key。</param>
    public void OpenCarUI(string start3DObjectName = null)
    {
        CarPanelManager manager = CarPanelManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[MapApi] 未找到 CarPanelManager，无法打开车辆 UI。");
            return;
        }

        manager.OpenCarUI(start3DObjectName);
    }

    /// <summary>关闭车辆 UI 与连线（仅 VehicleLevel 生效）。</summary>
    public void CloseCarUI()
    {
        CarPanelManager manager = CarPanelManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[MapApi] 未找到 CarPanelManager，无法关闭车辆 UI。");
            return;
        }

        manager.CloseCarUI();
    }

    /// <summary>正播：车辆 → 零件过渡。</summary>
    /// <param name="partName">零件 GameObject 名；为空时使用过渡控制器列表第一项。</param>
    public bool TransitionVehicleToPart(string partName = null)
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放车辆 → 零件过渡。");
            return false;
        }

        return controller.PlayTransition(partName);
    }

    /// <summary>倒播：零件 → 车辆过渡。</summary>
    /// <param name="partName">零件名；为空时使用上次正播记录的零件。</param>
    public bool TransitionPartToVehicle(string partName = null)
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放零件 → 车辆过渡。");
            return false;
        }

        return controller.PlayTransitionReverse(partName);
    }
}
