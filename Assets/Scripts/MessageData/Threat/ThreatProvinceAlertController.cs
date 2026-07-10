using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁态势省级告警：达标后依次处理，处理期间不重复触发。
/// 当前告警动作：切换 <see cref="GameManager.BigScreenPlaybackState.Threat"/>（其它动作预留）。
/// </summary>
public static class ThreatProvinceAlertController
{
    private static readonly Queue<string> _pendingProvinceCodes = new Queue<string>();
    private static readonly HashSet<string> _queuedOrProcessingProvinces = new HashSet<string>();

    private static bool _isProcessing;
    private static ThreatProvinceAlertContext _currentContext;

    /// <summary>是否正在执行告警处理流程（含排队等待）。</summary>
    public static bool IsProcessing => _isProcessing;

    /// <summary>当前正在处理的省级 code；无则 null。</summary>
    public static string CurrentProvinceCode => _currentContext?.ProvinceCode;

    /// <summary>当前正在处理的上下文；无则 null。</summary>
    public static ThreatProvinceAlertContext CurrentContext => _currentContext;

    /// <summary>开始处理某省告警时触发（已切换 Threat 状态）。</summary>
    public static event Action<ThreatProvinceAlertContext> ProvinceAlertStarted;

    /// <summary>某省告警处理完毕并已清理该省数据后触发。</summary>
    public static event Action<ThreatProvinceAlertContext> ProvinceAlertCompleted;

    /// <summary>全部排队省份处理完毕后触发。</summary>
    public static event Action AllProvinceAlertsCompleted;

    /// <summary>
    /// 数据入库后评估是否触发告警；处理进行中时忽略新评估。
    /// </summary>
    public static void EvaluateAfterDataUpdated()
    {
        if (_isProcessing)
        {
            return;
        }

        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        IReadOnlyList<string> qualifiedProvinces = store.GetProvincesMeetingThreshold(
            ThreatAlertSettings.EventsPerProvinceThreshold);
        if (qualifiedProvinces == null || qualifiedProvinces.Count == 0)
        {
            return;
        }

        EnqueueQualifiedProvinces(qualifiedProvinces);
        TryStartNextProvince();
    }

    /// <summary>
    /// 告警处理结束（由地图聚焦、下钻等脚本在处理完成后调用）。
    /// 会清理当前达标省数据并继续处理队列中的下一省。
    /// </summary>
    public static void CompleteCurrentProvinceAlert()
    {
        if (!_isProcessing || _currentContext == null)
        {
            Debug.LogWarning("[ThreatProvinceAlertController] 当前没有进行中的省级告警，忽略 Complete 调用。");
            return;
        }

        ThreatProvinceAlertContext completedContext = _currentContext;
        string provinceCode = completedContext.ProvinceCode;

        HighRiskSecurityEventDataStore.Instance.RemoveProvinceEvents(provinceCode);
        _queuedOrProcessingProvinces.Remove(provinceCode);

        _currentContext = null;
        ProvinceAlertCompleted?.Invoke(completedContext);

        Debug.Log($"[ThreatProvinceAlertController] 省级告警处理完毕，已清理数据：province={provinceCode}");

        if (_pendingProvinceCodes.Count > 0)
        {
            TryStartNextProvince();
            return;
        }

        _isProcessing = false;
        AllProvinceAlertsCompleted?.Invoke();
        Debug.Log("[ThreatProvinceAlertController] 全部达标省份已处理完毕。");
    }

    /// <summary>清空排队与处理状态（调试或场景重置用）。</summary>
    public static void ResetProcessingState()
    {
        _pendingProvinceCodes.Clear();
        _queuedOrProcessingProvinces.Clear();
        _currentContext = null;
        _isProcessing = false;
    }

    private static void EnqueueQualifiedProvinces(IReadOnlyList<string> provinceCodes)
    {
        for (int i = 0; i < provinceCodes.Count; i++)
        {
            string provinceCode = provinceCodes[i];
            if (string.IsNullOrWhiteSpace(provinceCode))
            {
                continue;
            }

            if (_queuedOrProcessingProvinces.Contains(provinceCode))
            {
                continue;
            }

            _queuedOrProcessingProvinces.Add(provinceCode);
            _pendingProvinceCodes.Enqueue(provinceCode);
        }
    }

    private static void TryStartNextProvince()
    {
        if (_pendingProvinceCodes.Count == 0)
        {
            return;
        }

        _isProcessing = true;
        string provinceCode = _pendingProvinceCodes.Dequeue();

        IReadOnlyList<HighRiskSecurityEventItem> events =
            HighRiskSecurityEventDataStore.Instance.GetEventsByProvince(provinceCode);
        _currentContext = new ThreatProvinceAlertContext
        {
            ProvinceCode = provinceCode,
            Events = events,
        };

        ExecuteAlertActions(_currentContext);
        ProvinceAlertStarted?.Invoke(_currentContext);

        Debug.Log(
            $"[ThreatProvinceAlertController] 开始处理省级告警：province={provinceCode}，事件数={events?.Count ?? 0}，" +
            $"队列剩余={_pendingProvinceCodes.Count}");
    }

    private static void ExecuteAlertActions(ThreatProvinceAlertContext context)
    {
        GameManager manager = GameManager.Instance;
        manager?.SetPlaybackState(GameManager.BigScreenPlaybackState.Threat);

        // 预留：地图聚焦、POI、Android 威胁下钻通知等。
    }
}
