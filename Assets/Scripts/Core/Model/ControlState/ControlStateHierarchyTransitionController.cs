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
    [SerializeField] private GameManager.ControlState _startState = GameManager.ControlState.EarthLevel;
    [Tooltip("Play 进入时先将 GameManager 逻辑状态对齐为地球级（场景默认地球视角）")]
    [SerializeField] private bool _ensureEarthBaselineOnPlay = true;
    [Tooltip("勾选后将过渡动画时长临时置 0，跳转结束后自动恢复")]
    [SerializeField] private bool _useInstantTransition = true;

    [Header("过渡参数（与正式流程一致）")]
    [SerializeField] private string _provinceName = "山东";
    [SerializeField] private string _provinceModuleName = "polySurface3";
    [SerializeField] private string _partName;

    [Header("启动时机")]
    [SerializeField] private int _warmupFrames = 2;
    [SerializeField] private float _stepTimeoutSeconds = 30f;

    private const int MaxTransitionSteps = 16;

    private bool _isBootstrapping;
    public bool IsBootstrapping => _isBootstrapping;

    private bool _stepDone;

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

        StartCoroutine(BootstrapAfterWarmup(_ensureEarthBaselineOnPlay, _startState));
    }

    public void ApplyStartStateNow()
    {
        if (_isBootstrapping)
        {
            Debug.LogWarning("[ControlStateHierarchyTransitionController] 正在跳转中，请稍候。");
            return;
        }

        StartCoroutine(BootstrapAfterWarmup(false, _startState));
    }

    /// <summary>
    /// 从当前 <see cref="GameManager"/> 逻辑状态逐步过渡到目标级别。
    /// </summary>
    /// <param name="useInstantTransition">是否启用瞬时过渡（临时将动画时长置 0）。</param>
    /// <param name="targetState">目标操控级别。</param>
    /// <param name="provinceName">省份名；为 null 时沿用 Inspector 配置。</param>
    /// <param name="provinceModuleName">省级模块名；为 null 时沿用 Inspector 配置。</param>
    /// <param name="partName">零件名；为 null 时沿用 Inspector 配置。</param>
    /// <param name="ensureEarthBaseline">是否先将逻辑状态对齐为地球级。</param>
    /// <returns>已启动跳转返回 true；正在跳转中返回 false。</returns>
    public bool TransitionToState(
        bool useInstantTransition,
        GameManager.ControlState targetState,
        string provinceName = null,
        string provinceModuleName = null,
        string partName = null,
        bool ensureEarthBaseline = false)
    {
        if (_isBootstrapping)
        {
            Debug.LogWarning("[ControlStateHierarchyTransitionController] 正在跳转中，请稍候。");
            return false;
        }

        _useInstantTransition = useInstantTransition;
        ApplyOptionalTransitionParameters(provinceName, provinceModuleName, partName);
        StartCoroutine(BootstrapAfterWarmup(ensureEarthBaseline, targetState));
        return true;
    }

    private void ApplyOptionalTransitionParameters(
        string provinceName,
        string provinceModuleName,
        string partName)
    {
        if (provinceName != null)
        {
            _provinceName = provinceName;
        }

        if (provinceModuleName != null)
        {
            _provinceModuleName = provinceModuleName;
        }

        if (partName != null)
        {
            _partName = partName;
        }
    }

    private IEnumerator BootstrapAfterWarmup(bool ensureEarthBaseline, GameManager.ControlState targetState)
    {
        for (int i = 0; i < Mathf.Max(0, _warmupFrames); i++)
        {
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
                Debug.LogWarning("[ControlStateHierarchyTransitionController] 未找到 GameManager。");
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

            LogCompleted(targetState);
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
            GameManager.ControlState currentState = GameManagerDemoAccess.GetCurrentState(manager);
            if (currentState == targetState)
            {
                yield break;
            }

            GameManager.ControlState expectedNext = GetNextStateToward(currentState, targetState);
            if (expectedNext == currentState)
            {
                Debug.LogWarning(
                    $"[ControlStateHierarchyTransitionController] 无法从 {currentState} 向目标 {targetState} 规划下一步，中止。");
                yield break;
            }

            yield return PlayTransitionStep(currentState, expectedNext);
            yield return WaitUntilControllersIdle();

            GameManager.ControlState nextState = GameManagerDemoAccess.GetCurrentState(manager);
            if (nextState == currentState)
            {
                Debug.LogWarning(
                    $"[ControlStateHierarchyTransitionController] 过渡后状态未变化：{currentState}，目标 {targetState}，中止。");
                yield break;
            }

            if (nextState != expectedNext)
            {
                Debug.LogWarning(
                    $"[ControlStateHierarchyTransitionController] 状态跳变异常：{currentState} → {nextState}，期望 {expectedNext}，中止。");
                yield break;
            }
        }

        Debug.LogWarning(
            $"[ControlStateHierarchyTransitionController] 超过最大步数 {MaxTransitionSteps}，当前 {GameManagerDemoAccess.GetCurrentState(manager)}，目标 {targetState}。");
    }

    /// <summary>
    /// 根据逻辑当前与目标计算下一步状态（显式邻接图，不依赖枚举加减）。
    /// 零件与攻击路径仅经车辆级衔接。
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
                return target == GameManager.ControlState.PartLevel
                    ? current
                    : GameManager.ControlState.VehicleLevel;

            case GameManager.ControlState.AttackPathLevel:
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
            yield return GoToProvinceLevel(_provinceModuleName);
            yield break;
        }

        if (from == GameManager.ControlState.ProvinceLevel
            && to == GameManager.ControlState.VehicleLevel)
        {
            yield return GoToVehicleLevel(_provinceName);
            yield break;
        }

        if (from == GameManager.ControlState.VehicleLevel
            && to == GameManager.ControlState.PartLevel)
        {
            yield return GoToPartLevel(_partName);
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
            yield return WaitForBoolStringEvent(
                h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted += h,
                h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted -= h,
                () => MapApi.Instance.TransitionCityToPlateMap(_provinceName),
                "车辆 → 板块聚焦倒播");
            yield break;
        }

        if (from == GameManager.ControlState.PartLevel
            && to == GameManager.ControlState.VehicleLevel)
        {
            yield return WaitForBoolStringEvent(
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted += h,
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted -= h,
                () => MapApi.Instance.TransitionPartToVehicle(_partName),
                "零件 → 车辆倒播");
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

        Debug.LogWarning($"[ControlStateHierarchyTransitionController] 未定义的过渡边：{from} → {to}");
    }

    private IEnumerator GoToProvinceLevel(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Debug.LogWarning("[ControlStateHierarchyTransitionController] 未配置省级模块名 _provinceModuleName。");
            yield break;
        }

        string key = moduleName.Trim();
        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted += h,
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted -= h,
            () => MapApi.Instance.FocusPlateMapModule(key),
            $"板块 → 板块聚焦 {key}");
    }

    private IEnumerator GoToVehicleLevel(string provinceName)
    {
        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnPlateToVehicleViewTransitionCompleted += h,
            h => EventManager.Instance.OnPlateToVehicleViewTransitionCompleted -= h,
            () => MapApi.Instance.TransitionPlateMapToCity(provinceName),
            "板块聚焦 → 城市 → 车辆");
    }

    private IEnumerator GoToPartLevel(string partName)
    {
        string key = string.IsNullOrWhiteSpace(partName) ? null : partName.Trim();
        yield return WaitForBoolStringEvent(
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted += h,
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionVehicleToPart(key),
            string.IsNullOrEmpty(key) ? "车辆 → 零件（默认）" : $"车辆 → 零件 {key}");
    }

    private IEnumerator GoToAttackPathLevel()
    {
        yield return WaitForVoidEvent(
            h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted += h,
            h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted -= h,
            () => MapApi.Instance.TransitionVehicleToAttackPath(),
            "车辆 → 攻击路径");
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
            Debug.LogWarning($"[ControlStateHierarchyTransitionController] 未找到 EventManager，跳过 {stepName}。");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDone);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ControlStateHierarchyTransitionController] {stepName} 启动失败。");
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
            Debug.LogWarning($"[ControlStateHierarchyTransitionController] 未找到 EventManager，跳过 {stepName}。");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDoneWithName);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ControlStateHierarchyTransitionController] {stepName} 启动失败。");
            unsubscribe(OnStepDoneWithName);
            yield break;
        }

        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDoneWithName);
    }

    private void OnStepDone()
    {
        _stepDone = true;
    }

    private void OnStepDoneWithName(string _)
    {
        _stepDone = true;
    }

    private IEnumerator WaitUntilControllersIdle()
    {
        yield return WaitUntil(() => !IsAnyTransitionBusy(), "等待其它过渡结束");
    }

    private IEnumerator WaitUntil(Func<bool> predicate, string stepName)
    {
        float elapsed = 0f;
        while (!predicate() && elapsed < _stepTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!predicate() && stepName != "等待其它过渡结束")
        {
            Debug.LogWarning($"[ControlStateHierarchyTransitionController] {stepName} 超时（{_stepTimeoutSeconds}s）。");
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

        if (CarModelChangeController.Instance != null
            && CarModelChangeController.Instance.IsTransitioning)
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

    private static void LogCompleted(GameManager.ControlState targetState)
    {
        GameManager manager = GameManager.Instance;
        GameManager.ControlState actual = manager != null
            ? GameManagerDemoAccess.GetCurrentState(manager)
            : targetState;
        Debug.Log($"[ControlStateHierarchyTransitionController] 跳转结束，目标 {targetState}，当前 {actual}。");
    }
}
