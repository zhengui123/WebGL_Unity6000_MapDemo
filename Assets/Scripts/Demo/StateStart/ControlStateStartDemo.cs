using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 编辑器/运行时测试：开局按预设 <see cref="GameManager.ControlState"/> 快速进入对应界面。
/// 从当前操控级别出发，每次只播放<strong>相邻一级</strong>的正播或倒播过渡，直至到达目标。
/// 地球 → 车辆 示例：地球→板块 → 板块聚焦 → 板块聚焦→城市→车辆。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class ControlStateStartDemo : MonoBehaviour
{
    [Header("开局跳转")]
    [SerializeField] private bool _applyOnPlay = true;
    [SerializeField] private GameManager.ControlState _startState = GameManager.ControlState.EarthLevel;
    [Tooltip("Play 进入时先将 GameManager 逻辑状态对齐为地球级（场景默认地球视角）")]
    [SerializeField] private bool _ensureEarthBaselineOnPlay = true;

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

    private void Start()
    {
        if (!_applyOnPlay)
        {
            return;
        }
#if UNITY_EDITOR
        StartCoroutine(BootstrapAfterWarmup(ensureEarthBaseline: _ensureEarthBaselineOnPlay));
#endif
    }

    public void ApplyStartStateNow()
    {
        if (_isBootstrapping)
        {
            Debug.LogWarning("[ControlStateStartDemo] 正在跳转中，请稍候。");
            return;
        }

        StartCoroutine(BootstrapAfterWarmup(ensureEarthBaseline: false));
    }

    private IEnumerator BootstrapAfterWarmup(bool ensureEarthBaseline)
    {
        for (int i = 0; i < Mathf.Max(0, _warmupFrames); i++)
        {
            yield return null;
        }

        yield return BootstrapToState(_startState, ensureEarthBaseline);
    }

    private IEnumerator BootstrapToState(GameManager.ControlState targetState, bool ensureEarthBaseline)
    {
        _isBootstrapping = true;
        try
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[ControlStateStartDemo] 未找到 GameManager。");
                yield break;
            }

            using (TransitionInstantDurationScope.ApplyToLoadedScene())
            {
                if (ensureEarthBaseline)
                {
                    GameManagerDemoAccess.ForceState(manager, GameManager.ControlState.EarthLevel);
                    yield return null;
                }

                yield return StepTowardTarget(manager, targetState);
            }

            LogCompleted(targetState);
        }
        finally
        {
            _isBootstrapping = false;
        }
    }

    /// <summary>比较当前与目标，每次只执行一级正播或倒播。</summary>
    private IEnumerator StepTowardTarget(GameManager manager, GameManager.ControlState targetState)
    {
        GameManager.ControlState previousState = GameManagerDemoAccess.GetCurrentState(manager);

        for (int step = 0; step < MaxTransitionSteps; step++)
        {
            GameManager.ControlState currentState = GameManagerDemoAccess.GetCurrentState(manager);
            if (currentState == targetState)
            {
                yield break;
            }

            bool stepUp = currentState < targetState;
            GameManager.ControlState expectedNext = stepUp
                ? currentState + 1
                : currentState - 1;

            if (stepUp)
            {
                yield return PlayStepUp(currentState);
            }
            else
            {
                yield return PlayStepDown(currentState);
            }

            yield return WaitUntilControllersIdle();

            GameManager.ControlState nextState = GameManagerDemoAccess.GetCurrentState(manager);
            if (nextState == previousState)
            {
                Debug.LogWarning(
                    $"[ControlStateStartDemo] 过渡后状态未变化：{previousState}，目标 {targetState}，中止。");
                yield break;
            }

            if (nextState != expectedNext)
            {
                Debug.LogWarning(
                    $"[ControlStateStartDemo] 状态跳变异常：{previousState} → {nextState}，期望 {expectedNext}。");
            }

            previousState = nextState;
        }

        Debug.LogWarning(
            $"[ControlStateStartDemo] 超过最大步数 {MaxTransitionSteps}，当前 {GameManagerDemoAccess.GetCurrentState(manager)}，目标 {targetState}。");
    }

    #region 正播（当前 → 下一级）

    private IEnumerator PlayStepUp(GameManager.ControlState currentState)
    {
        switch (currentState)
        {
            case GameManager.ControlState.EarthLevel:
                // 地球 → 板块（国家级别）
                yield return WaitForVoidEvent(
                    h => EventManager.Instance.OnTransitionToPlateMapCompleted += h,
                    h => EventManager.Instance.OnTransitionToPlateMapCompleted -= h,
                    () => MapApi.Instance.TransitionToPlateMap(),
                    "地球 → 板块");
                break;

            case GameManager.ControlState.CountryLevel:
                // 板块 → 板块聚焦（省级）
                yield return GoToProvinceLevel(_provinceModuleName);
                break;

            case GameManager.ControlState.ProvinceLevel:
                // 板块聚焦 → GaodeMap → 城市 → 车辆
                yield return GoToVehicleLevel(_provinceName);
                break;

            case GameManager.ControlState.VehicleLevel:
                // 车辆 → 零件
                yield return GoToPartLevel(_partName);
                break;

            default:
                Debug.LogWarning($"[ControlStateStartDemo] 无法从 {currentState} 继续正播。");
                yield break;
        }
    }

    #endregion

    #region 倒播（当前 → 上一级）

    private IEnumerator PlayStepDown(GameManager.ControlState currentState)
    {
        switch (currentState)
        {
            case GameManager.ControlState.PartLevel:
                yield return WaitForStringEvent(
                    h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted += h,
                    h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted -= h,
                    () => MapApi.Instance.TransitionPartToVehicle(_partName),
                    "零件 → 车辆倒播");
                break;

            case GameManager.ControlState.VehicleLevel:
                // 车辆 → 城市 → GaodeMap → 板块聚焦
                yield return WaitForStringEvent(
                    h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted += h,
                    h => EventManager.Instance.OnVehicleToPlateViewTransitionCompleted -= h,
                    () => MapApi.Instance.TransitionCityToPlateMap(_provinceName),
                    "车辆 → 板块聚焦倒播");
                break;

            case GameManager.ControlState.ProvinceLevel:
                // 板块聚焦 → 板块（国家级别）
                yield return WaitForVoidEvent(
                    h => EventManager.Instance.OnPlateMapRestoreCameraCompleted += h,
                    h => EventManager.Instance.OnPlateMapRestoreCameraCompleted -= h,
                    () => MapApi.Instance.RestorePlateMapCamera(),
                    "省级相机还原");
                break;

            case GameManager.ControlState.CountryLevel:
                // 板块 → 地球
                yield return WaitForVoidEvent(
                    h => EventManager.Instance.OnTransitionToEarthCompleted += h,
                    h => EventManager.Instance.OnTransitionToEarthCompleted -= h,
                    () => MapApi.Instance.TransitionToEarth(),
                    "板块 → 地球");
                break;

            default:
                GameManagerDemoAccess.ForceState(GameManager.Instance, GameManager.ControlState.EarthLevel);
                yield return null;
                break;
        }
    }

    #endregion

    private IEnumerator GoToProvinceLevel(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Debug.LogWarning("[ControlStateStartDemo] 未配置省级模块名 _provinceModuleName。");
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

    private IEnumerator WaitForVoidEvent(
        Action<Action> subscribe,
        Action<Action> unsubscribe,
        Action trigger,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            Debug.LogWarning($"[ControlStateStartDemo] 未找到 EventManager，跳过 {stepName}。");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDone);
        yield return WaitUntilControllersIdle();
        trigger();
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
            Debug.LogWarning($"[ControlStateStartDemo] 未找到 EventManager，跳过 {stepName}。");
            yield break;
        }

        _stepDone = false;
        subscribe(OnStepDoneWithName);
        yield return WaitUntilControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ControlStateStartDemo] {stepName} 启动失败。");
            unsubscribe(OnStepDoneWithName);
            yield break;
        }

        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDoneWithName);
    }

    private IEnumerator WaitForStringEvent(
        Action<Action<string>> subscribe,
        Action<Action<string>> unsubscribe,
        Action trigger,
        string stepName)
    {
        yield return WaitForBoolStringEvent(
            subscribe,
            unsubscribe,
            () =>
            {
                trigger();
                return true;
            },
            stepName);
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
            Debug.LogWarning($"[ControlStateStartDemo] {stepName} 超时（{_stepTimeoutSeconds}s）。");
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
        Debug.Log($"[ControlStateStartDemo] 跳转结束，目标 {targetState}，当前 {actual}。");
    }
}
