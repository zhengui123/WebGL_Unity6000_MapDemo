using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 编辑器/运行时测试：开局按预设 <see cref="GameManager.ControlState"/> 快速进入对应界面。
/// 跳转期间通过 <see cref="TransitionInstantDurationScope"/> 将过渡时长临时置 0，
/// 结束后恢复各控制器 Inspector 配置，不影响正式过渡参数。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)] // 晚于多数 Awake，确保单例与场景引用已就绪
public class ControlStateStartDemo : MonoBehaviour
{
    [Header("开局跳转")]
    [Tooltip("进入 Play 后是否自动执行开局跳转")]
    [SerializeField] private bool _applyOnPlay = true;
    [Tooltip("开局目标操控级别")]
    [SerializeField] private GameManager.ControlState _startState = GameManager.ControlState.EarthLevel;

    [Header("过渡参数（与正式流程一致，仅用于触发 API）")]
    [Tooltip("车辆级及倒播编排使用的省份名")]
    [SerializeField] private string _provinceName = "山东";
    [Tooltip("省级聚焦使用的板块模块名（场景 GameObject 名）")]
    [SerializeField] private string _provinceModuleName = "polySurface3";

    [Header("启动时机")]
    [Tooltip("Play 后等待帧数，避免与其它 Awake/Start 抢初始化顺序")]
    [SerializeField] private int _warmupFrames = 2;
    [Tooltip("单步过渡最长等待秒数，超时打 Warning")]
    [SerializeField] private float _stepTimeoutSeconds = 30f;

    /// <summary>是否正在执行开局跳转协程。</summary>
    private bool _isBootstrapping;

    public bool IsBootstrapping => _isBootstrapping;

    private void Start()
    {

        if (!_applyOnPlay)
        {
            return;
        }
#if UNITY_EDITOR
        StartCoroutine(BootstrapAfterWarmup());
#endif
    }

    /// <summary>编辑器 Inspector 按钮或运行时手动触发开局跳转。</summary>
    public void ApplyStartStateNow()
    {
        if (_isBootstrapping)
        {
            Debug.LogWarning("[ControlStateStartDemo] 正在跳转中，请稍候。");
            return;
        }

        StartCoroutine(BootstrapAfterWarmup());
    }

    /// <summary>预热若干帧后再开始跳转，降低初始化竞态。</summary>
    private IEnumerator BootstrapAfterWarmup()
    {
        for (int i = 0; i < Mathf.Max(0, _warmupFrames); i++)
        {
            yield return null;
        }

        yield return BootstrapToState(_startState);
    }

    /// <summary>
    /// 核心流程：先归零到地球级，再按枚举顺序逐级正播至目标状态。
    /// using 块内时长为 0，块结束后自动恢复各控制器原时长。
    /// </summary>
    private IEnumerator BootstrapToState(GameManager.ControlState targetState)
    {
        _isBootstrapping = true;
        try
        {
            using (TransitionInstantDurationScope.ApplyToLoadedScene())
            {
                // 统一从地球级起步，若当前更高则先倒播还原
                yield return ResetToEarthLevel();

                if (targetState > GameManager.ControlState.EarthLevel)
                {
                    yield return GoToCountryLevel();
                }

                if (targetState > GameManager.ControlState.CountryLevel)
                {
                    yield return GoToProvinceLevel(_provinceModuleName);
                }

                if (targetState > GameManager.ControlState.ProvinceLevel)
                {
                    yield return GoToVehicleLevel(_provinceName);
                }

                // 零部件级尚无独立场景过渡，仅设置 GameManager 状态
                if (targetState > GameManager.ControlState.VehicleLevel)
                {
                    ApplyPartLevelState();
                }
            }

            LogCompleted(targetState);
        }
        finally
        {
            _isBootstrapping = false;
        }
    }

    /// <summary>
    /// 将当前操控级别降到 EarthLevel：按 Part → Vehicle → Province → Country 顺序倒播或强制降级。
    /// </summary>
    private IEnumerator ResetToEarthLevel()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ControlStateStartDemo] 未找到 GameManager。");
            yield break;
        }

        while (GameManagerDemoAccess.GetCurrentState(manager) > GameManager.ControlState.EarthLevel)
        {
            GameManager.ControlState state = GameManagerDemoAccess.GetCurrentState(manager);
            switch (state)
            {
                case GameManager.ControlState.PartLevel:
                    // 无倒播 API，先降到 Vehicle 再走正式倒播链
                    GameManagerDemoAccess.ForceState(manager, GameManager.ControlState.VehicleLevel);
                    yield return null;
                    break;

                case GameManager.ControlState.VehicleLevel:
                    yield return RunOrchestratorStep(
                        () => MapApi.Instance.TransitionCityToPlateMap(_provinceName),
                        "车辆 → 板块倒播");
                    break;

                case GameManager.ControlState.ProvinceLevel:
                    yield return WaitForVoidEvent(
                        h => EventManager.Instance.OnPlateMapRestoreCameraCompleted += h,
                        h => EventManager.Instance.OnPlateMapRestoreCameraCompleted -= h,
                        () => MapApi.Instance.RestorePlateMapCamera(),
                        "省级相机还原");
                    break;

                case GameManager.ControlState.CountryLevel:
                    yield return WaitForVoidEvent(
                        h => EventManager.Instance.OnTransitionToEarthCompleted += h,
                        h => EventManager.Instance.OnTransitionToEarthCompleted -= h,
                        () => MapApi.Instance.TransitionToEarth(),
                        "板块 → 地球");
                    break;

                default:
                    GameManagerDemoAccess.ForceState(manager, GameManager.ControlState.EarthLevel);
                    yield return null;
                    break;
            }
        }
    }

    /// <summary>地球级 → 国家级别：地球过渡到板块地图。</summary>
    private IEnumerator GoToCountryLevel()
    {
        yield return WaitForVoidEvent(
            h => EventManager.Instance.OnTransitionToPlateMapCompleted += h,
            h => EventManager.Instance.OnTransitionToPlateMapCompleted -= h,
            () => MapApi.Instance.TransitionToPlateMap(),
            "地球 → 板块");
    }

    /// <summary>国家级别 → 省级：聚焦指定板块模块。</summary>
    private IEnumerator GoToProvinceLevel(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Debug.LogWarning("[ControlStateStartDemo] 未配置省级模块名 _provinceModuleName。");
            yield break;
        }

        string key = moduleName.Trim();
        yield return WaitForStringEvent(
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted += h,
            h => EventManager.Instance.OnPlateMapFocusModuleCompleted -= h,
            () => MapApi.Instance.FocusPlateMapModule(key),
            $"聚焦省级模块 {key}");
    }

    /// <summary>省级 → 车辆级：两阶段编排正播（板块 → Gaode → City）。</summary>
    private IEnumerator GoToVehicleLevel(string provinceName)
    {
        yield return RunOrchestratorStep(
            () => MapApi.Instance.TransitionPlateMapToCity(provinceName),
            "板块 → 车辆界面");
    }

    /// <summary>车辆级 → 零部件级：仅更新 GameManager 状态（占位）。</summary>
    private void ApplyPartLevelState()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        GameManagerDemoAccess.ForceState(manager, GameManager.ControlState.PartLevel);
        Debug.Log("[ControlStateStartDemo] 零部件级暂无独立过渡，已直接设置 GameManager 状态。");
    }

    /// <summary>启动编排器并轮询直至 <see cref="PlateToCityMapTransitionOrchestrator.IsOrchestrating"/> 为 false。</summary>
    private IEnumerator RunOrchestratorStep(Func<bool> startAction, string stepName)
    {
        yield return WaitUntilControllersIdle();
        if (!startAction())
        {
            yield return WaitUntilControllersIdle();
            startAction();
        }

        yield return WaitUntil(
            () => PlateToCityMapTransitionOrchestrator.Instance == null
                || !PlateToCityMapTransitionOrchestrator.Instance.IsOrchestrating,
            stepName);
    }

    /// <summary>订阅无参完成事件 → 触发 API → 等待事件回调。</summary>
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

    /// <summary>订阅带模块名参数的完成事件 → 触发 API → 等待事件回调。</summary>
    private IEnumerator WaitForStringEvent(
        Action<Action<string>> subscribe,
        Action<Action<string>> unsubscribe,
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
        subscribe(OnStepDoneWithName);
        yield return WaitUntilControllersIdle();
        trigger();
        yield return WaitUntil(() => _stepDone, stepName);
        unsubscribe(OnStepDoneWithName);
    }

    /// <summary>当前步骤是否收到完成事件。</summary>
    private bool _stepDone;

    private void OnStepDone()
    {
        _stepDone = true;
    }

    private void OnStepDoneWithName(string _)
    {
        _stepDone = true;
    }

    /// <summary>等待场景中所有已知过渡控制器均空闲。</summary>
    private IEnumerator WaitUntilControllersIdle()
    {
        yield return WaitUntil(() => !IsAnyTransitionBusy(), "等待其它过渡结束");
    }

    /// <summary>通用轮询等待，使用 unscaledDeltaTime 避免 Time.timeScale 影响。</summary>
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

    /// <summary>检查各过渡单例是否仍在播放中。</summary>
    private static bool IsAnyTransitionBusy()
    {
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

        return false;
    }

    private static void LogCompleted(GameManager.ControlState state)
    {
        Debug.Log($"[ControlStateStartDemo] 开局已进入 {state}（演示用瞬时过渡已结束，各控制器时长已恢复）。");
    }
}
