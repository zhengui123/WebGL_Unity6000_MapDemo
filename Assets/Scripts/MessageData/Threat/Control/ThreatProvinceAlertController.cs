using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁态势告警控制入口：数据达标后启动 <see cref="ThreatAlertFlowRunner"/>。
/// 不维护长期省份队列；每轮国家停留后取「当前最新达标列表第一条」。
/// 主动退出后进入冷却：冷却期内不检测、暂停高危事件轮询；冷却结束后若仍开启轮询则先请求再评估。
/// </summary>
public static class ThreatProvinceAlertController
{
    private static bool _isProcessing;
    private static ThreatProvinceAlertContext _currentContext;

    /// <summary>是否正在执行告警处理流程。</summary>
    public static bool IsProcessing =>
        _isProcessing || (ThreatAlertFlowRunner.Instance != null && ThreatAlertFlowRunner.Instance.IsRunning);

    /// <summary>是否处于主动打断后的冷却期（期间不启动威胁检测）。</summary>
    public static bool IsInInterruptCooldown =>
        ThreatAlertFlowRunner.Instance != null && ThreatAlertFlowRunner.Instance.IsInInterruptCooldown;

    /// <summary>冷却剩余秒数；非冷却中为 0。</summary>
    public static float InterruptCooldownRemaining =>
        ThreatAlertFlowRunner.Instance != null
            ? ThreatAlertFlowRunner.Instance.InterruptCooldownRemaining
            : 0f;

    /// <summary>当前正在处理的省级 code；无则 null。</summary>
    public static string CurrentProvinceCode => _currentContext?.ProvinceCode;

    /// <summary>当前正在处理的上下文；无则 null。</summary>
    public static ThreatProvinceAlertContext CurrentContext => _currentContext;

    /// <summary>开始处理某省告警时触发。</summary>
    public static event Action<ThreatProvinceAlertContext> ProvinceAlertStarted;

    /// <summary>某省告警阶段结束时触发。</summary>
    public static event Action<ThreatProvinceAlertContext> ProvinceAlertCompleted;

    /// <summary>全部告警流程空闲（无达标省）时触发。</summary>
    public static event Action AllProvinceAlertsCompleted;

    /// <summary>
    /// 数据入库后评估是否触发告警。
    /// 冷却中：不检测；处理中：仅刷新画面；空闲且达标：启动流程。
    /// </summary>
    public static void EvaluateAfterDataUpdated()
    {
        if (IsInInterruptCooldown)
        {
            Debug.Log(
                $"[ThreatProvinceAlertController] 威胁冷却中，跳过检测 | 剩余={InterruptCooldownRemaining:F0}s");
            return;
        }

        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        IReadOnlyList<string> qualifiedProvinces = store.GetProvincesMeetingThreshold(
            ThreatAlertSettings.EventsPerProvinceThreshold);

        if (IsProcessing)
        {
            ThreatAlertFlowRunner running = ThreatAlertFlowRunner.Instance;
            if (running != null)
            {
                running.RefreshVisualsFromCache();
            }

            Debug.Log(
                "[ThreatProvinceAlertController] 威胁处理中，仅刷新数据与当前阶段画面，不重新进入流程。" +
                $" 达标省数={qualifiedProvinces?.Count ?? 0}");
            return;
        }

        if (qualifiedProvinces == null || qualifiedProvinces.Count == 0)
        {
            GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
            return;
        }

        ThreatAlertFlowRunner runner = ThreatAlertFlowRunner.Instance;
        if (runner == null)
        {
            Debug.LogError(
                "[ThreatProvinceAlertController] 场景中未找到 ThreatAlertFlowRunner，请挂到任意常驻物体上。");
            return;
        }

        _isProcessing = true;

        bool carouselActive = IsAutoCarouselOrDelayedStartActive();
        bool resumeFromVehicle = !carouselActive && ThreatAlertFlowRunner.IsInVehicleDrillControlState();
        if (!runner.TryStartThreatFlow(resumeFromVehicleDrillSubtree: resumeFromVehicle))
        {
            _isProcessing = false;
            Debug.LogWarning("[ThreatProvinceAlertController] 威胁流程启动失败（可能已在运行或冷却中）。");
            return;
        }

        if (carouselActive)
        {
            Debug.Log(
                "[ThreatProvinceAlertController] 检测到自动轮播/延时等待，已停轮播并从全国进入威胁下钻。");
        }
        else if (resumeFromVehicle)
        {
            Debug.Log(
                "[ThreatProvinceAlertController] 已在车辆/攻击链路/零件级，从当前级别继续 Vin 下钻（跳过国家/省级停留）。");
        }
    }

    private static bool IsAutoCarouselOrDelayedStartActive()
    {
        BigScreenCarouselController carousel = BigScreenCarouselController.Instance;
        if (carousel != null)
        {
            return carousel.IsAutoCarouselEnabled || carousel.IsWaitingDelayedStart;
        }

        MapApi mapApi = MapApi.Instance;
        return mapApi != null && mapApi.IsBigScreenAutoCarouselEnabled();
    }

    /// <summary>
    /// 主动退出威胁下钻：停在当前级别，进入冷却（默认 180s）。
    /// </summary>
    public static bool ExitThreatDrill()
    {
        ThreatAlertFlowRunner runner = ThreatAlertFlowRunner.Instance;
        if (runner == null)
        {
            Debug.LogWarning("[ThreatProvinceAlertController] 未找到 ThreatAlertFlowRunner，无法退出威胁下钻。");
            return false;
        }

        return runner.ExitThreatDrill();
    }

    /// <summary>
    /// 刷新威胁冷却倒计时（仅冷却中有效，重新计满配置秒数）。
    /// </summary>
    public static bool RefreshThreatCooldown()
    {
        ThreatAlertFlowRunner runner = ThreatAlertFlowRunner.Instance;
        if (runner == null)
        {
            Debug.LogWarning("[ThreatProvinceAlertController] 未找到 ThreatAlertFlowRunner，无法刷新冷却。");
            return false;
        }

        return runner.RefreshThreatCooldown();
    }

    /// <summary>
    /// 跳过当前停留并尽快进入下一阶段（国家/省/车辆/攻击链路/零件；Demo/外部调用）。
    /// </summary>
    public static void CompleteCurrentProvinceAlert()
    {
        ThreatAlertFlowRunner runner = ThreatAlertFlowRunner.Instance;
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogWarning("[ThreatProvinceAlertController] 当前没有进行中的威胁流程，忽略 Complete 调用。");
            return;
        }

        runner.SkipCurrentHold();
    }

    /// <summary>清空处理状态与冷却（调试）。</summary>
    public static void ResetProcessingState()
    {
        _currentContext = null;
        _isProcessing = false;
        ThreatAlertFlowRunner.Instance?.StopAndResetVisuals();
    }

    internal static void NotifyProvinceAlertStarted(ThreatProvinceAlertContext context)
    {
        _currentContext = context;
        ProvinceAlertStarted?.Invoke(context);
    }

    internal static void NotifyProvinceAlertCompleted(ThreatProvinceAlertContext context)
    {
        ProvinceAlertCompleted?.Invoke(context);
        _currentContext = null;
    }

    internal static void NotifyAllAlertsCompleted()
    {
        _currentContext = null;
        _isProcessing = false;
        AllProvinceAlertsCompleted?.Invoke();
    }

    internal static void NotifyFlowStopped()
    {
        _isProcessing = false;
        _currentContext = null;
    }
}
