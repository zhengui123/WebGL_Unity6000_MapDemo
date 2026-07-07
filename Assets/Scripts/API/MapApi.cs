using System;
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
    public bool TransitionVehicleToPart(string partName = null, string partId = null)
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放车辆 → 零件过渡。");
            return false;
        }

        return controller.PlayTransition(partName, partId);
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

    /// <summary>正播：车辆 → 攻击路径过渡。</summary>
    public bool TransitionVehicleToAttackPath()
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放车辆 → 攻击路径过渡。");
            return false;
        }

        return controller.PlayVehicleToAttackPathTransition();
    }

    /// <summary>倒播：攻击路径 → 车辆过渡。</summary>
    public bool TransitionAttackPathToVehicle()
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放攻击路径 → 车辆过渡。");
            return false;
        }

        return controller.PlayAttackPathToVehicleTransition();
    }

    /// <summary>正播：攻击路径 → 零件过渡（直接隐藏攻击路径并播放两段零件动画）。</summary>
    public bool TransitionAttackPathToPart(string partName = null, string partId = null)
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 VehicleToPartTransitionController，无法播放攻击路径 → 零件过渡。");
            return false;
        }

        return controller.PlayAttackPathToPartTransition(partName, partId);
    }

    /// <summary>
    /// 从当前 GameManager 逻辑操控级别，按邻接图逐步过渡到目标级别。
    /// 主干：地球 → 国家 → 省级 → 车辆；车辆下并列分支：零件 / 攻击路径（须经车辆级衔接）。
    /// </summary>
    /// <param name="targetState">
    /// 目标操控级别：0 地球级、1 国家级、2 省级、3 车辆级、4 零件级、5 攻击路径级。
    /// </param>
    /// <param name="provinceName">
    /// 省级行政区名称，用于省级 ↔ 车辆 阶段的高德地图聚焦（如「山东」「广东」）。
    /// 须为已配置的省名；
    /// 为 null 使用默认配置。
    /// </param>
    /// <param name="provinceModuleName">
    /// 3D 板块模型模块名（场景中 GameObject 名，如 polySurface3），用于国家 → 省级 的板块聚焦。
    /// 为 null 使用默认配置。
    /// </param>
    /// <param name="partName">
    /// 车辆零件 GameObject 名，用于车辆 ↔ 零件 过渡；为空或 null 时使用过渡控制器默认/列表首项。
    /// 为 null 使用默认配置。
    /// </param>
    /// <param name="partId">
    /// 业务零部件ID。仅当当前已在零件级且触发零件 → 零件切换时生效；其它场景忽略。
    /// </param>
    /// </param>
    /// <param name="useInstantTransition">
    /// 是否启用瞬时过渡。为 true 时，跳转期间临时将各过渡控制器动画时长置 0，结束后自动恢复，不修改 Inspector 原始配置。
    /// </param>
    /// <returns>已成功启动跳转协程返回 true；控制器不存在、正在跳转中或 targetState 无效时返回 false。</returns>
    public bool TransitionToControlState(
        int targetState,
        string provinceName = null,
        string provinceModuleName = null,
        string partName = null,
        string partId = null,
        bool useInstantTransition = false)
    {
        if (!Enum.IsDefined(typeof(GameManager.ControlState), targetState))
        {
            Debug.LogWarning($"[MapApi] 无效的 targetState：{targetState}，有效范围为 0~5。");
            return false;
        }

        ControlStateHierarchyTransitionController controller =
            ControlStateHierarchyTransitionController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 ControlStateHierarchyTransitionController，无法执行层级跳转。");
            return false;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null
            && manager.CurrentState == GameManager.ControlState.PartLevel
            && targetState == (int)GameManager.ControlState.PartLevel)
        {
            return TransitionVehicleToPart(partName, partId);
        }

        if (manager != null
            && manager.CurrentState == GameManager.ControlState.AttackPathLevel
            && targetState == (int)GameManager.ControlState.PartLevel)
        {
            return TransitionAttackPathToPart(partName, partId);
        }

        return controller.TransitionToState(
            useInstantTransition,
            (GameManager.ControlState)targetState,
            provinceName,
            provinceModuleName,
            partName,
            partId,
            false);
    }

    /// <summary>进入层级下一级（与双击操作一致）。</summary>
    public bool TransitionToNextControlState()
    {
        ControlStateHierarchyInputNavigation navigation =
            ControlStateHierarchyInputNavigation.FindFromTransitionController();
        if (navigation == null)
        {
            Debug.LogWarning("[MapApi] 未找到 ControlStateHierarchyInputNavigation。");
            return false;
        }

        return navigation.TryTransitionToNextLevel();
    }

    /// <summary>返回层级上一级（与 Escape / Android 返回键一致）。</summary>
    public bool TransitionToPreviousControlState()
    {
        ControlStateHierarchyInputNavigation navigation =
            ControlStateHierarchyInputNavigation.FindFromTransitionController();
        if (navigation == null)
        {
            Debug.LogWarning("[MapApi] 未找到 ControlStateHierarchyInputNavigation。");
            return false;
        }

        return navigation.TryTransitionToPreviousLevel();
    }

    /// <summary>开启或关闭四个大屏自动轮播（默认间隔 2 分钟）。</summary>
    public bool SetBigScreenAutoCarouselEnabled(bool enabled)
    {
        BigScreenCarouselController controller = BigScreenCarouselController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 BigScreenCarouselController。");
            return false;
        }

        controller.SetAutoCarouselEnabled(enabled);
        return true;
    }

    /// <summary>是否已开启大屏自动轮播。</summary>
    public bool IsBigScreenAutoCarouselEnabled()
    {
        BigScreenCarouselController controller = BigScreenCarouselController.Instance;
        return controller != null && controller.IsAutoCarouselEnabled;
    }
}
