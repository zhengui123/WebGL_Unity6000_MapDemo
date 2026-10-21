using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 操控层级跳转控制器：按 <see cref="GameManager.ControlState"/> 从逻辑当前状态逐步过渡到目标状态。
/// 主干：地球 → 国家 → 省级 → 车辆；车辆下并列分支：零件 / 攻击路径（互不直连）。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class ControlStateHierarchyTransitionController : UnitySingle<ControlStateHierarchyTransitionController>
{
    [Header("开局跳转")]
    [SerializeField] private bool _applyOnPlay = true;
    [SerializeField] private GameManager.ControlState _startState = GameManager.ControlState.CountryLevel;
    [Tooltip("Play 进入时先将 GameManager 逻辑状态对齐为地球级。关闭则从当前状态直接走到开局目标（默认国家级）。")]
    [SerializeField] private bool _ensureEarthBaselineOnPlay = false;
    [Tooltip("勾选后将过渡动画时长临时置 0，跳转结束后自动恢复")]
    [SerializeField] private bool _useInstantTransition = true;

    [Header("过渡参数")]
    [SerializeField] private string _partId;
    [Tooltip("零部件标号 1~5；0 表示未指定，走 partId/名称回退")]
    [SerializeField] private int _partType;

    [Header("启动时机")]
    [SerializeField] private int _warmupFrames = 2;
    [SerializeField] private float _stepTimeoutSeconds = 30f;

    private const int MaxTransitionSteps = 16;

    private struct PendingTransitionRequest
    {
        public bool UseInstantTransition;
        public GameManager.ControlState TargetState;
        public string ProvinceCode;
        public string PartId;
        public int PartType;
        public bool EnsureEarthBaseline;
    }

    private bool _isBootstrapping;
    public bool IsBootstrapping => _isBootstrapping;

    private bool _stepDone;
    private bool _restartRequested;
    private bool _hasPendingTransitionRequest;
    private PendingTransitionRequest _pendingTransitionRequest;

    /// <summary>本次跳转使用的省名（调用参数优先，否则 GameManager 默认）。</summary>
    private string _activeProvinceName;

    /// <summary>本次跳转使用的省/国家 code（解析后的单元 code）。</summary>
    private string _activeProvinceCode;

    /// <summary>本次跳转使用的板块模块名（调用参数优先，否则由默认省 code 解析）。</summary>
    private string _activeProvinceModuleName;

    /// <summary>
    /// 车辆→省级倒播使用的「当前物理省」显示名（换省前捕获）。
    /// 空则回退 <see cref="_activeProvinceName"/>。
    /// </summary>
    private string _vehicleToProvinceReverseName;

    public override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (!_applyOnPlay)
        {
            return;
        }

        ResolveActiveProvinceParameters(null);
        StartCoroutine(BootstrapAfterWarmup(_ensureEarthBaselineOnPlay, _startState));
    }

    public void ApplyStartStateNow()
    {
        if (_isBootstrapping)
        {
            LogManager.LogFeatureWarning("[ControlStateHierarchyTransitionController] 正在跳转中，请稍候。");
            return;
        }

        ResolveActiveProvinceParameters(null);
        StartCoroutine(BootstrapAfterWarmup(false, _startState));
    }

    /// <summary>进入层级下一级（委托给输入导航组件）。</summary>
    public bool TryTransitionToNextLevel()
    {
        ControlStateHierarchyInputNavigation navigation = ControlStateHierarchyInputNavigation.FindFromTransitionController();
        return navigation != null && navigation.TryTransitionToNextLevel();
    }

    /// <summary>返回层级上一级（委托给输入导航组件）。</summary>
    public bool TryTransitionToPreviousLevel()
    {
        ControlStateHierarchyInputNavigation navigation = ControlStateHierarchyInputNavigation.FindFromTransitionController();
        return navigation != null && navigation.TryTransitionToPreviousLevel();
    }

    /// <summary>
    /// 从当前 <see cref="GameManager"/> 逻辑状态逐步过渡到目标级别。
    /// </summary>
    /// <param name="useInstantTransition">是否启用瞬时过渡（临时将动画时长置 0）。</param>
    /// <param name="targetState">目标操控级别。</param>
    /// <param name="provinceCode">省/国家 code；空/null 时使用当前默认单元。</param>
    /// <param name="partId">显示名；为 null 时沿用 Inspector 配置（零部件级返回车辆时忽略）。</param>
    /// <param name="ensureEarthBaseline">是否先将逻辑状态对齐为地球级。</param>
    /// <param name="partType">零部件标号 1~5；&lt;=0 时沿用 Inspector / 回退显示名。</param>
    /// <returns>已启动跳转返回 true；打断并改派最新目标时也返回 true。</returns>
    public bool TransitionToState(
        bool useInstantTransition,
        GameManager.ControlState targetState,
        string provinceCode = null,
        string partId = null,
        bool ensureEarthBaseline = false,
        int partType = 0)
    {
        GameManager manager = GameManager.Instance;
        GameManager.ControlState currentState = manager != null
            ? GameManagerDemoAccess.GetCurrentState(manager)
            : GameManager.ControlState.EarthLevel;
        // 零部件 → 车辆倒播：忽略外部 partId/partType，仅按当前场景中的激活零件还原。
        string partIdForTransition = currentState == GameManager.ControlState.PartLevel
            ? null
            : partId;
        int partTypeForTransition = currentState == GameManager.ControlState.PartLevel
            ? 0
            : partType;

        // 跳转中或动画未结束：终止当前动画，强制瞬时改派到新目标
        if (_isBootstrapping || IsAnyTransitionBusy())
        {
            QueuePendingTransition(
                useInstantTransition: true,
                targetState,
                provinceCode,
                partIdForTransition,
                ensureEarthBaseline,
                partTypeForTransition);
            RequestRestartAfterCurrentTransition();
            LogManager.LogFeature(
                $"[ControlStateHierarchyTransitionController] 打断当前层级过渡，强制瞬时跳转 → {targetState}");
            return true;
        }

        _useInstantTransition = useInstantTransition;
        if (partIdForTransition != null)
        {
            _partId = partIdForTransition;
        }

        if (PartTypeMatchUtil.IsValidPartType(partTypeForTransition))
        {
            _partType = partTypeForTransition;
        }
        else if (partIdForTransition != null)
        {
            // 显式只传显示名时清空旧标号，避免误用上次 partType
            _partType = 0;
        }

        // 先记下当前省（倒回省级动画用），再解析目标省参数
        _vehicleToProvinceReverseName = CaptureCurrentProvinceDisplayNameForReverse();
        ResolveActiveProvinceParameters(provinceCode);

        if (targetState == GameManager.ControlState.ProvinceLevel)
        {
            // 省级目标：真正切到目标省；缓存由 FocusModule 写入
            LogManager.LogFeature(
                $"[ControlStateHierarchyTransitionController] 省级目标换省 | " +
                $"reverse={_vehicleToProvinceReverseName ?? "(空)"} → " +
                $"target={_activeProvinceName}({_activeProvinceCode}) module={_activeProvinceModuleName}");
        }
        else
        {
            // 非省级：只更新省缓存供过渡动画，级别按邻接图走（同级则仅改缓存）
            ApplyTargetProvinceCacheForNonProvinceTarget();
        }

        StartCoroutine(BootstrapAfterWarmup(ensureEarthBaseline, targetState));
        return true;
    }

    /// <summary>
    /// 由省/国家 code 解析本次跳转显示名与板块模块名。
    /// 空 code：优先进省缓存（国内省 / 国外国家），再读 GameManager / WorldMap 默认。
    /// </summary>
    private void ResolveActiveProvinceParameters(string provinceCode)
    {
        string unitCode;
        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            unitCode = WorldMapPlateResolver.ResolveUnitCode(provinceCode);
        }
        else if (PlateProvinceFocusResolver.TryGetCachedProvinceCode(out string cachedCode))
        {
            unitCode = cachedCode;
        }
        else
        {
            unitCode = WorldMapPlateResolver.ResolveUnitCode(null);
        }

        _activeProvinceCode = unitCode;
        _activeProvinceName = WorldMapPlateResolver.ResolveUnitDisplayName(unitCode);
        if (string.IsNullOrWhiteSpace(_activeProvinceName) &&
            PlateProvinceFocusResolver.HasCachedProvince)
        {
            _activeProvinceName = PlateProvinceFocusResolver.CachedProvinceName;
        }

        if (WorldMapPlateResolver.TryResolveUnitModuleName(unitCode, out string moduleName) &&
            !string.IsNullOrWhiteSpace(moduleName))
        {
            _activeProvinceModuleName = moduleName.Trim();
        }
        else
        {
            GameManager gm = GameManager.Instance;
            _activeProvinceModuleName = gm != null
                ? gm.ResolveProvinceModuleName(null)
                : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_activeProvinceName))
        {
            GameManager gm = GameManager.Instance;
            _activeProvinceName = gm != null ? gm.DefaultProvinceName : "浙江";
        }
    }

    /// <summary>捕获跳转前当前省显示名，供车辆→省级倒播使用。</summary>
    private static string CaptureCurrentProvinceDisplayNameForReverse()
    {
        if (PlateProvinceFocusResolver.HasCachedProvince &&
            !string.IsNullOrWhiteSpace(PlateProvinceFocusResolver.CachedProvinceName))
        {
            return PlateProvinceFocusResolver.CachedProvinceName.Trim();
        }

        if (PlateProvinceFocusResolver.TryGetFocusedPlateProvinceCode(out string focusedCode) &&
            !string.IsNullOrWhiteSpace(focusedCode))
        {
            string name = WorldMapPlateResolver.ResolveUnitDisplayName(focusedCode);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }

        GameManager gm = GameManager.Instance;
        return gm != null ? gm.DefaultProvinceName : "浙江";
    }

    /// <summary>非省级目标：把解析后的目标省写入进省缓存（同级换省仅改缓存、不换画面层级）。</summary>
    private void ApplyTargetProvinceCacheForNonProvinceTarget()
    {
        if (string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_activeProvinceModuleName) &&
            PlateProvinceFocusResolver.TryCacheFromModuleName(_activeProvinceModuleName))
        {
            LogManager.LogFeature(
                $"[ControlStateHierarchyTransitionController] 非省级目标已更新省缓存(模块) | " +
                $"code={PlateProvinceFocusResolver.CachedProvinceCode} | " +
                $"name={PlateProvinceFocusResolver.CachedProvinceName}");
            return;
        }

        if (PlateProvinceFocusResolver.TryCacheProvince(_activeProvinceCode, _activeProvinceName))
        {
            LogManager.LogFeature(
                $"[ControlStateHierarchyTransitionController] 非省级目标已更新省缓存 | " +
                $"code={_activeProvinceCode} | name={_activeProvinceName}");
        }
    }

    /// <summary>当前聚焦/缓存省是否与本次目标省不一致（需 Focus 换省）。</summary>
    private bool NeedsProvinceRefocus()
    {
        if (string.IsNullOrWhiteSpace(_activeProvinceModuleName) &&
            string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            return false;
        }

        PlateMapDisplayController display = PlateMapDisplayController.Instance;
        PlateMapDisplayModule focused = display != null ? display.FocusedModule : null;
        if (focused != null && !string.IsNullOrWhiteSpace(_activeProvinceModuleName))
        {
            return !string.Equals(
                focused.ModuleKey,
                _activeProvinceModuleName,
                StringComparison.OrdinalIgnoreCase);
        }

        if (PlateProvinceFocusResolver.TryGetCachedProvinceCode(out string cachedCode) &&
            !string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            return !string.Equals(
                cachedCode.Trim(),
                _activeProvinceCode.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(_activeProvinceModuleName);
    }

    /// <summary>
    /// 同级车辆/攻击链路：跳过层级过渡动画，刷新展示，并触发一次完成事件（宿主 from=-1 回调）。
    /// 数据请求（如 RequestCarVehicleData）仍由宿主另行调用，此处不替代。
    /// </summary>
    private IEnumerator ApplySameLevelNonHierarchyRefresh(GameManager.ControlState level)
    {
        yield return null;

        EventManager em = EventManager.Instance;
        if (level == GameManager.ControlState.VehicleLevel)
        {
            VehicleToPartTransitionController.Instance?.EnsureVehicleLevelVisualsHidden();
            CarVehicleDataController.Instance?.TryShowVehicleUiFromCache();

            string provinceName = ResolveActiveProvinceName();
            LogManager.LogFeature(
                $"[ControlStateHierarchyTransitionController] 同级车辆：跳过层级动画，刷新展示并完成回调 | province={provinceName}");
            em?.TriggerPlateToVehicleViewTransitionCompleted(provinceName);
            yield break;
        }

        if (level == GameManager.ControlState.AttackPathLevel)
        {
            CarVehicleDataController.Instance?.ApplyAttackPathsFromCacheForTransition();
            LogManager.LogFeature(
                "[ControlStateHierarchyTransitionController] 同级攻击链路：跳过层级动画，刷新链路并完成回调");
            em?.TriggerVehicleToAttackPathTransitionCompleted();
        }
    }

    private IEnumerator BootstrapAfterWarmup(bool ensureEarthBaseline, GameManager.ControlState targetState)
    {
        for (int i = 0; i < Mathf.Max(0, _warmupFrames); i++)
        {
            if (_restartRequested)
            {
                yield break;
            }

            yield return null;
        }

        yield return BootstrapToState(targetState, ensureEarthBaseline);
    }

    private IEnumerator BootstrapToState(GameManager.ControlState targetState, bool ensureEarthBaseline)
    {
        _isBootstrapping = true;
        try
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                LogManager.LogFeatureWarning(
                    $"[ToStateError] reason=未找到 GameManager | target={targetState}");
                yield break;
            }

            if (_useInstantTransition)
            {
                using (TransitionInstantDurationScope.ApplyToLoadedScene())
                {
                    yield return RunBootstrapCore(manager, targetState, ensureEarthBaseline);
                }
            }
            else
            {
                yield return RunBootstrapCore(manager, targetState, ensureEarthBaseline);
            }

            GameManager.ControlState actualState = LogCompleted(targetState);
            // 层级跳转结束后：按会话恢复/隐藏事件溯源黄点（仅国家/对应省级显示）
            SecurityEventDetailApi.RefreshTracePoiForControlState(actualState, _activeProvinceCode);
        }
        finally
        {
            _isBootstrapping = false;
        }
    }

    private IEnumerator RunBootstrapCore(
        GameManager manager,
        GameManager.ControlState targetState,
        bool ensureEarthBaseline)
    {
        if (_restartRequested)
        {
            yield break;
        }

        if (ensureEarthBaseline)
        {
            GameManagerDemoAccess.ForceState(manager, GameManager.ControlState.EarthLevel);
            yield return null;
        }

        yield return StepTowardTarget(manager, targetState);
    }

    /// <summary>从 GameManager 逻辑当前状态出发，每步只执行一条相邻过渡，直至到达目标。</summary>
    private IEnumerator StepTowardTarget(GameManager manager, GameManager.ControlState targetState)
    {
        for (int step = 0; step < MaxTransitionSteps; step++)
        {
            if (_restartRequested)
            {
                yield break;
            }

            GameManager.ControlState currentState = GameManagerDemoAccess.GetCurrentState(manager);
            if (currentState == targetState)
            {
                // 已在省级但目标省不同：补一次 Focus，避免「同级换省无效」
                if (targetState == GameManager.ControlState.ProvinceLevel && NeedsProvinceRefocus())
                {
                    yield return GoToProvinceLevel(_activeProvinceModuleName);
                    yield return WaitUntilControllersIdle();
                }
                else if (targetState == GameManager.ControlState.VehicleLevel
                         || targetState == GameManager.ControlState.AttackPathLevel)
                {
                    // 同级车辆/攻击链路：不重播层级过渡，只刷非层级展示并发一次完成回调
                    yield return ApplySameLevelNonHierarchyRefresh(targetState);
                }

                yield break;
            }

            GameManager.ControlState expectedNext = GetNextStateToward(currentState, targetState);
            if (expectedNext == currentState)
            {
                LogManager.LogFeatureWarning(
                    $"[ToStateError] reason=无法规划下一步 | from={currentState} | target={targetState}");
                yield break;
            }

            yield return PlayTransitionStep(currentState, expectedNext);
            yield return WaitUntilControllersIdle();

            if (_restartRequested)
            {
                yield break;
            }

            GameManager.ControlState nextState = GameManagerDemoAccess.GetCurrentState(manager);
            if (nextState == currentState)
            {
                LogManager.LogFeatureWarning(
                    $"[ToStateError] reason=过渡后状态未变化 | from={currentState} | target={targetState}");
                yield break;
            }

            if (nextState != expectedNext)
            {
                LogManager.LogFeatureWarning(
                    $"[ToStateError] reason=状态跳变异常 | from={currentState} | actual={nextState} | " +
                    $"expected={expectedNext} | target={targetState}");
                yield break;
            }
        }

        LogManager.LogFeatureWarning(
            $"[ToStateError] reason=超过最大步数 | max={MaxTransitionSteps} | " +
            $"from={GameManagerDemoAccess.GetCurrentState(manager)} | target={targetState}");
    }

    /// <summary>
    /// 根据逻辑当前与目标计算下一步状态（显式邻接图，不依赖枚举加减）。
    /// 零件与攻击路径可与车辆级衔接；零件 ↔ 攻击路径可直连。
    /// </summary>
    private static GameManager.ControlState GetNextStateToward(
        GameManager.ControlState current,
        GameManager.ControlState target)
    {
        if (current == target)
        {
            return current;
        }

        switch (current)
        {
            case GameManager.ControlState.PartLevel:
                if (target == GameManager.ControlState.PartLevel)
                {
                    return current;
                }

                // 零件 ↔ 攻击链路直连，不经车辆级中转。
                if (target == GameManager.ControlState.AttackPathLevel)
                {
                    return GameManager.ControlState.AttackPathLevel;
                }

                return GameManager.ControlState.VehicleLevel;

            case GameManager.ControlState.AttackPathLevel:
                if (target == GameManager.ControlState.PartLevel)
                {
                    return GameManager.ControlState.PartLevel;
                }

                return target == GameManager.ControlState.AttackPathLevel
                    ? current
                    : GameManager.ControlState.VehicleLevel;

            case GameManager.ControlState.VehicleLevel:
                if (target == GameManager.ControlState.PartLevel)
                {
                    return GameManager.ControlState.PartLevel;
                }

                if (target == GameManager.ControlState.AttackPathLevel)
                {
                    return GameManager.ControlState.AttackPathLevel;
                }

                if (target < GameManager.ControlState.VehicleLevel)
                {
                    return GameManager.ControlState.ProvinceLevel;
                }

                return current;

            default:
                // 地球 / 国家 / 省级主干
                if (target >= GameManager.ControlState.PartLevel)
                {
                    return current < GameManager.ControlState.VehicleLevel
                        ? current + 1
                        : current;
                }

                if (target > current)
                {
                    return current + 1;
                }

                return current - 1;
        }
    }

    /// <summary>播放单步相邻过渡（from → to 必须匹配邻接图）。</summary>
    private IEnumerator PlayTransitionStep(
        GameManager.ControlState from,
        GameManager.ControlState to)
    {
        if (from == GameManager.ControlState.EarthLevel
            && to == GameManager.ControlState.CountryLevel)
        {
            yield return WaitForVoidEvent(
                h => EventManager.Instance.OnTransitionToPlateMapCompleted += h,
                h => EventManager.Instance.OnTransitionToPlateMapCompleted -= h,
                () =>
                {
                    MapApi.Instance.TransitionToPlateMap();
                    return true;
                },
                "地球 → 板块");
            yield break;
        }

        if (from == GameManager.ControlState.CountryLevel
            && to == GameManager.ControlState.ProvinceLevel)
        {
            yield return GoToProvinceLevel(_activeProvinceModuleName);
            yield break;
        }

        if (from == GameManager.ControlState.ProvinceLevel
            && to == GameManager.ControlState.VehicleLevel)
        {
            yield return GoToVehicleLevel(_activeProvinceName);
            yield break;
        }

        if (from == GameManager.ControlState.VehicleLevel
            && to == GameManager.ControlState.PartLevel)
        {
            yield return GoToPartLevel();
            yield break;
        }

        if (from == GameManager.ControlState.VehicleLevel
            && to == GameManager.ControlState.AttackPathLevel)
        {
            yield return GoToAttackPathLevel();
            yield break;
        }

        if (from == GameManager.ControlState.VehicleLevel
            && to == GameManager.ControlState.ProvinceLevel)
        {
            // 倒播必须用「当前物理省」，不能用已解析的目标省（否则 A 车回不到 A 省）
            string reverseName = ResolveProvinceNameForVehicleToProvinceReverse();
            yield return WaitForBoolStringEvent(
                h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted += h,
                h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted -= h,
                () => MapApi.Instance.TransitionCityToPlateMap(reverseName),
                $"车辆 → 板块聚焦倒播 ({reverseName})");
            yield break;
        }

        if (from == GameManager.ControlState.PartLevel
            && to == GameManager.ControlState.VehicleLevel)
        {
            string activePartId = ResolveActivePartIdForVehicleReverse();
            yield return WaitForBoolStringEvent(
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted += h,
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted -= h,
                () => MapApi.Instance.TransitionPartToVehicle(activePartId),
                string.IsNullOrEmpty(activePartId)
                    ? "零件 → 车辆倒播（当前零件）"
                    : $"零件 → 车辆倒播（当前零件 {activePartId}）");
            yield break;
        }

        if (from == GameManager.ControlState.PartLevel
            && to == GameManager.ControlState.AttackPathLevel)
        {
            yield return WaitForVoidEvent(
                h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted += h,
                h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted -= h,
                () => MapApi.Instance.TransitionPartToAttackPath(),
                "零件 → 攻击路径");
            yield break;
        }

        if (from == GameManager.ControlState.AttackPathLevel
            && to == GameManager.ControlState.PartLevel)
        {
            yield return GoToPartLevelFromAttackPath();
            yield break;
        }

        if (from == GameManager.ControlState.AttackPathLevel
            && to == GameManager.ControlState.VehicleLevel)
        {
            yield return WaitForVoidEvent(
                h => EventManager.Instance.OnAttackPathToVehicleTransitionCompleted += h,
                h => EventManager.Instance.OnAttackPathToVehicleTransitionCompleted -= h,
                () => MapApi.Instance.TransitionAttackPathToVehicle(),
                "攻击路径 → 车辆倒播");
            yield break;
        }

        if (from == GameManager.ControlState.ProvinceLevel
            && to == GameManager.ControlState.CountryLevel)
        {
            yield return WaitForVoidEvent(
                h => EventManager.Instance.OnPlateMapRestoreCameraCompleted += h,
                h => EventManager.Instance.OnPlateMapRestoreCameraCompleted -= h,
                () => MapApi.Instance.RestorePlateMapCamera(),
                "省级相机还原");
            yield break;
        }

        if (from == GameManager.ControlState.CountryLevel
            && to == GameManager.ControlState.EarthLevel)
        {
            yield return WaitForVoidEvent(
                h => EventManager.Instance.OnTransitionToEarthCompleted += h,
                h => EventManager.Instance.OnTransitionToEarthCompleted -= h,
                () =>
                {
                    MapApi.Instance.TransitionToEarth();
                    return true;
                },
                "板块 → 地球");
            yield break;
        }

        LogManager.LogFeatureWarning($"[ToStateError] reason=未定义的过渡边 | from={from} | to={to}");
    }

    private IEnumerator GoToProvinceLevel(string moduleName)
    {
        string key = !string.IsNullOrWhiteSpace(moduleName)
            ? moduleName.Trim()
            : ResolveActiveProvinceModuleName();

        if (string.IsNullOrWhiteSpace(key))
        {
            LogManager.LogFeatureWarning(
                $"[ToStateError] reason=未解析到板块模块名，无法进入省级 | moduleName={moduleName} | " +
                $"activeModule={_activeProvinceModuleName} | activeProvince={_activeProvinceName}");
            yield break;
        }

        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted += h,
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted -= h,
            () => MapApi.Instance.FocusPlateMapModule(key),
            $"板块 → 板块聚焦 {key}");
    }

    private IEnumerator GoToVehicleLevel(string provinceName)
    {
        string name = !string.IsNullOrWhiteSpace(provinceName)
            ? provinceName.Trim()
            : ResolveActiveProvinceName();

        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnPlateToVehicleViewTransitionCompleted += h,
            h => EventManager.Instance.OnPlateToVehicleViewTransitionCompleted -= h,
            () => MapApi.Instance.TransitionPlateMapToCity(name),
            $"板块聚焦 → 城市 → 车辆 ({name})");
    }

    private string ResolveActiveProvinceName()
    {
        if (!string.IsNullOrWhiteSpace(_activeProvinceName))
        {
            return _activeProvinceName.Trim();
        }

        GameManager gm = GameManager.Instance;
        return gm != null ? gm.DefaultProvinceName : "浙江";
    }

    /// <summary>车辆→省级倒播省名：优先换省前捕获的当前省。</summary>
    private string ResolveProvinceNameForVehicleToProvinceReverse()
    {
        if (!string.IsNullOrWhiteSpace(_vehicleToProvinceReverseName))
        {
            return _vehicleToProvinceReverseName.Trim();
        }

        return ResolveActiveProvinceName();
    }

    private string ResolveActiveProvinceModuleName()
    {
        if (!string.IsNullOrWhiteSpace(_activeProvinceModuleName))
        {
            return _activeProvinceModuleName.Trim();
        }

        GameManager gm = GameManager.Instance;
        return gm != null ? gm.ResolveProvinceModuleName(null) : string.Empty;
    }

    private IEnumerator GoToPartLevel()
    {
        string key = string.IsNullOrWhiteSpace(_partId) ? null : _partId.Trim();
        int type = PartTypeMatchUtil.IsValidPartType(_partType) ? _partType : 0;
        string stepLabel = type > 0
            ? $"车辆 → 零件 type={type} name={key ?? "(空)"}"
            : (string.IsNullOrEmpty(key) ? "车辆 → 零件（默认）" : $"车辆 → 零件 {key}");
        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted += h,
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionVehicleToPart(key, type),
            stepLabel);
    }

    private IEnumerator GoToAttackPathLevel()
    {
        yield return WaitForVoidEvent(
            h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted += h,
            h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted -= h,
            () => MapApi.Instance.TransitionVehicleToAttackPath(),
            "车辆 → 攻击路径");
    }

    private IEnumerator GoToPartLevelFromAttackPath()
    {
        string key = string.IsNullOrWhiteSpace(_partId) ? null : _partId.Trim();
        int type = PartTypeMatchUtil.IsValidPartType(_partType) ? _partType : 0;
        string stepLabel = type > 0
            ? $"攻击路径 → 零件 type={type} name={key ?? "(空)"}"
            : (string.IsNullOrEmpty(key) ? "攻击路径 → 零件（默认）" : $"攻击路径 → 零件 {key}");
        yield return WaitForBoolStringStringEvent(
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted += h,
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionAttackPathToPart(key, type),
            stepLabel);
    }

    /// <summary>零部件 → 车辆倒播时使用的零件 ID：优先上次正播记录。</summary>
    private static string ResolveActivePartIdForVehicleReverse()
    {
        VehicleToPartTransitionController partController = VehicleToPartTransitionController.Instance;
        if (partController == null)
        {
            return null;
        }

        string lastPartId = partController.LastPartId;
        return string.IsNullOrEmpty(lastPartId) ? null : lastPartId;
    }

    private IEnumerator WaitForVoidEvent(
        Action<Action> subscribe,
        Action<Action> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=未找到 EventManager | step={stepName}");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDone);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=步骤启动失败 | step={stepName}");
            unsubscribe(OnStepDone);
            yield break;
        }

        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDone);
    }

    private IEnumerator WaitForBoolStringEvent(
        Action<Action<string>> subscribe,
        Action<Action<string>> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=未找到 EventManager | step={stepName}");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDoneWithName);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=步骤启动失败 | step={stepName}");
            unsubscribe(OnStepDoneWithName);
            yield break;
        }

        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDoneWithName);
    }

    private IEnumerator WaitForBoolStringStringEvent(
        Action<Action<string, string>> subscribe,
        Action<Action<string, string>> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=未找到 EventManager | step={stepName}");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDoneWithNameAndId);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ToStateError] reason=步骤启动失败 | step={stepName}");
            unsubscribe(OnStepDoneWithNameAndId);
            yield break;
        }

        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDoneWithNameAndId);
    }

    private void OnStepDone()
    {
        _stepDone = true;
    }

    private void OnStepDoneWithName(string _)
    {
        _stepDone = true;
    }

    private void OnStepDoneWithNameAndId(string _, string __)
    {
        _stepDone = true;
    }

    private IEnumerator WaitUntilControllersIdle()
    {
        yield return WaitUntil(() =>
        {
            if (_restartRequested)
            {
                CompleteBusyTransitionsImmediately();
            }

            return !IsAnyTransitionBusy();
        }, "等待其它过渡结束");
    }

    private IEnumerator WaitUntil(Func<bool> predicate, string stepName)
    {
        float elapsed = 0f;
        while (!predicate() && !_restartRequested && elapsed < _stepTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!predicate() && !_restartRequested && stepName != "等待其它过渡结束")
        {
            LogManager.LogFeatureWarning(
                $"[ToStateError] reason=步骤超时 | step={stepName} | timeout={_stepTimeoutSeconds}s");
        }
    }

    private static bool IsAnyTransitionBusy()
    {
        if (EarthTransition.Instance != null && EarthTransition.Instance.IsTransitioning)
        {
            return true;
        }

        if (PlateToCityMapTransitionOrchestrator.Instance != null
            && PlateToCityMapTransitionOrchestrator.Instance.IsOrchestrating)
        {
            return true;
        }

        if (PlateToGaodeMapTransitionController.Instance != null
            && PlateToGaodeMapTransitionController.Instance.IsTransitioning)
        {
            return true;
        }

        if (GaodeToCityTransitionController.Instance != null
            && GaodeToCityTransitionController.Instance.IsTransitioning)
        {
            return true;
        }

        if (CarModelDissolveController.Instance != null
            && CarModelDissolveController.Instance.IsTransitioning)
        {
            return true;
        }

        if (CityHideTransitionController.Instance != null
            && CityHideTransitionController.Instance.IsTransitioning)
        {
            return true;
        }

        if (VehicleToPartTransitionController.Instance != null
            && VehicleToPartTransitionController.Instance.IsTransitioning)
        {
            return true;
        }

        return false;
    }

    /// <summary>各场景过渡控制器是否仍在播放动画。</summary>
    public static bool IsAnyTransitionAnimationBusy() => IsAnyTransitionBusy();

    /// <summary>
    /// 外部调用：终止当前层级流程并优先执行新的目标跳转。
    /// 立即 Complete 进行中的过渡动画，并标记重启以改派最新请求。
    /// </summary>
    public void AbortRunningTransition()
    {
        if (!_isBootstrapping && !IsAnyTransitionBusy())
        {
            return;
        }

        RequestRestartAfterCurrentTransition();
    }

    private void QueuePendingTransition(
        bool useInstantTransition,
        GameManager.ControlState targetState,
        string provinceCode,
        string partId,
        bool ensureEarthBaseline,
        int partType = 0)
    {
        _pendingTransitionRequest = new PendingTransitionRequest
        {
            UseInstantTransition = useInstantTransition,
            TargetState = targetState,
            ProvinceCode = provinceCode,
            PartId = partId,
            PartType = partType,
            EnsureEarthBaseline = ensureEarthBaseline
        };
        _hasPendingTransitionRequest = true;
    }

    private void RequestRestartAfterCurrentTransition()
    {
        _restartRequested = true;
        // 让正在 WaitUntil(_stepDone) 的步骤立刻结束，避免空等超时
        _stepDone = true;
        CompleteBusyTransitionsImmediately();
    }

    private void StartPendingTransitionIfAny()
    {
        if (!_hasPendingTransitionRequest)
        {
            return;
        }

        PendingTransitionRequest request = _pendingTransitionRequest;
        _hasPendingTransitionRequest = false;

        TransitionToState(
            request.UseInstantTransition,
            request.TargetState,
            request.ProvinceCode,
            request.PartId,
            request.EnsureEarthBaseline,
            request.PartType);
    }

    private static void CompleteBusyTransitionsImmediately()
    {
        EarthTransition.Instance?.CompleteCurrentTransitionImmediate();
        PlateToCityMapTransitionOrchestrator.Instance?.CompleteCurrentTransitionImmediate();
        PlateToGaodeMapTransitionController.Instance?.CompleteCurrentTransitionImmediate();
        GaodeToCityTransitionController.Instance?.CompleteCurrentTransitionImmediate();
        CarModelDissolveController.Instance?.CompleteCurrentTransitionImmediate();
        CityHideTransitionController.Instance?.CompleteCurrentTransitionImmediate();
        VehicleToPartTransitionController.Instance?.CompleteCurrentTransitionImmediate();
    }

    private static GameManager.ControlState LogCompleted(GameManager.ControlState targetState)
    {
        GameManager manager = GameManager.Instance;
        GameManager.ControlState actual = manager != null
            ? GameManagerDemoAccess.GetCurrentState(manager)
            : targetState;
        LogManager.LogFeature($"[ControlStateHierarchyTransitionController] 跳转结束，目标 {targetState}，当前 {actual}。");
        return actual;
    }

    private void LateUpdate()
    {
        if (_isBootstrapping)
        {
            return;
        }

        if (_restartRequested)
        {
            _restartRequested = false;
        }

        StartPendingTransitionIfAny();
    }
}
