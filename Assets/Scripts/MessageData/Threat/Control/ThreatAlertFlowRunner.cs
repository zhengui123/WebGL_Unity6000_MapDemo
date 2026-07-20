using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁告警流程协程宿主：先瞬时回到国家级 → 国家 10s → 取最新达标第一条省 → 省 60s → Vin/下钻预留 → 回国家再评估。
/// 场景中挂一个即可（可用 <see cref="UnitySingle{T}"/> 自动查找）。
/// </summary>
public class ThreatAlertFlowRunner : UnitySingle<ThreatAlertFlowRunner>
{
    private Coroutine _flowRoutine;
    private bool _skipCurrentHold;
    private bool _vehicleStageFinished;
    private bool _provinceFocusSignal;

    /// <summary>是否正在跑威胁流程。</summary>
    public bool IsRunning => _flowRoutine != null;

    /// <summary>Vin≥3 时请求进入车辆大屏（预留，参数为 Vin）。</summary>
    public static event Action<string> ThreatVehicleEntryRequested;

    /// <summary>省级 60s 之后的威胁下钻预留钩子（具体动作等后续指令）。</summary>
    public static event Action<ThreatProvinceAlertContext> ThreatProvinceDrillReserved;

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateMapFocusModuleCompleted += HandleProvinceFocusCompleted;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateMapFocusModuleCompleted -= HandleProvinceFocusCompleted;
        StopFlowInternal();
    }

    /// <summary>启动一轮威胁流程（进行中则忽略）。</summary>
    public bool TryStartThreatFlow()
    {
        if (_flowRoutine != null)
        {
            return false;
        }

        _flowRoutine = StartCoroutine(ThreatFlowRoutine());
        return true;
    }

    /// <summary>跳过当前国家/省级停留计时（Demo「结束当前告警」可用）。</summary>
    public void SkipCurrentHold()
    {
        _skipCurrentHold = true;
    }

    /// <summary>车辆大屏阶段结束（预留对接完成后由外部调用）。</summary>
    public void NotifyVehicleStageFinished()
    {
        _vehicleStageFinished = true;
    }

    /// <summary>停止流程并清理 POI/高亮（调试重置）。</summary>
    public void StopAndResetVisuals()
    {
        StopFlowInternal();
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
    }

    private void StopFlowInternal()
    {
        if (_flowRoutine != null)
        {
            StopCoroutine(_flowRoutine);
            _flowRoutine = null;
        }

        _skipCurrentHold = false;
        _vehicleStageFinished = false;
    }

    private IEnumerator ThreatFlowRoutine()
    {
        try
        {
            while (true)
            {
                HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
                IReadOnlyList<string> qualified = store.GetProvincesMeetingThreshold(
                    ThreatAlertSettings.EventsPerProvinceThreshold);

                if (qualified == null || qualified.Count == 0)
                {
                    ApplyNoThreatIdleState();
                    yield break;
                }

                GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Threat);

                yield return EnsureCountryLevel();
                yield return PlayCountryStage(qualified);

                // 每次用最新达标列表的第一条（无长期队列）
                qualified = store.GetProvincesMeetingThreshold(ThreatAlertSettings.EventsPerProvinceThreshold);
                if (qualified == null || qualified.Count == 0)
                {
                    ApplyNoThreatIdleState();
                    yield break;
                }

                string provinceCode = qualified[0];
                yield return PlayProvinceStage(provinceCode);

                // 省阶段结束后回到国家，用最新缓存再评估
                yield return EnsureCountryLevelFromProvince();
            }
        }
        finally
        {
            _flowRoutine = null;
            ThreatProvinceAlertController.NotifyFlowStopped();
        }
    }

    /// <summary>
    /// 若不在国家级：经层级控制器瞬时逐步跳回 CountryLevel，完成后再继续威胁逻辑。
    /// </summary>
    private IEnumerator EnsureCountryLevel()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        if (gm.CurrentState == GameManager.ControlState.CountryLevel)
        {
            yield break;
        }

        ControlStateHierarchyTransitionController hierarchy =
            ControlStateHierarchyTransitionController.Instance;
        if (hierarchy == null)
        {
            Debug.LogWarning(
                "[ThreatAlertFlowRunner] 未找到 ControlStateHierarchyTransitionController，无法跳回国家级。");
            yield break;
        }

        while (hierarchy.IsBootstrapping)
        {
            yield return null;
        }

        if (gm.CurrentState == GameManager.ControlState.CountryLevel)
        {
            yield break;
        }

        GameManager.ControlState from = gm.CurrentState;
        bool started = hierarchy.TransitionToState(
            useInstantTransition: true,
            targetState: GameManager.ControlState.CountryLevel);
        if (!started)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] 从 {from} 瞬时跳转国家级失败。");
            yield break;
        }

        Debug.Log($"[ThreatAlertFlowRunner] 瞬时跳转回国家级：{from} → CountryLevel");

        const float bootstrapStartTimeoutSeconds = 3f;
        float elapsed = 0f;
        while (!hierarchy.IsBootstrapping && elapsed < bootstrapStartTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        while (hierarchy.IsBootstrapping)
        {
            yield return null;
        }

        while (ControlStateHierarchyTransitionController.IsAnyTransitionAnimationBusy())
        {
            yield return null;
        }

        float confirmTimeout = 5f;
        while (gm.CurrentState != GameManager.ControlState.CountryLevel && confirmTimeout > 0f)
        {
            confirmTimeout -= Time.deltaTime;
            yield return null;
        }

        if (gm.CurrentState != GameManager.ControlState.CountryLevel)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 跳转后仍非国家级：{gm.CurrentState}，继续尝试后续步骤。");
        }
    }

    /// <summary>省阶段结束后回国家：与任意级别回国家同一套瞬时层级跳转。</summary>
    private IEnumerator EnsureCountryLevelFromProvince()
    {
        yield return EnsureCountryLevel();
    }

    private IEnumerator PlayCountryStage(IReadOnlyList<string> qualifiedProvinceCodes)
    {
        POI_Manager.Instance?.RemoveAllPoi();

        List<string> plateNames = new List<string>(qualifiedProvinceCodes.Count);
        for (int i = 0; i < qualifiedProvinceCodes.Count; i++)
        {
            string code = qualifiedProvinceCodes[i];
            if (!ThreatProvinceCenterLookup.TryGetCenter(code, out double lon, out double lat))
            {
                continue;
            }

            POI_Manager.Instance?.SpawnPoi(code, POIType.provinece_Rad, lon, lat);

            if (PlateMapAPI.Instance != null &&
                PlateMapAPI.Instance.TryResolvePlateMapName(code, out string plateName) &&
                !string.IsNullOrWhiteSpace(plateName) &&
                !plateNames.Contains(plateName))
            {
                plateNames.Add(plateName);
            }
        }

        PlateMapHighlightController.Instance?.HighlightModulesByName(plateNames);

        Debug.Log(
            $"[ThreatAlertFlowRunner] 国家阶段：达标省={qualifiedProvinceCodes.Count}，" +
            $"停留={ThreatAlertSettings.CountryLevelHoldSeconds:F0}s");

        yield return WaitHoldSeconds(ThreatAlertSettings.CountryLevelHoldSeconds);
    }

    private IEnumerator PlayProvinceStage(string provinceCode)
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        IReadOnlyList<HighRiskSecurityEventItem> events = store.GetEventsByProvince(provinceCode);
        ThreatProvinceAlertContext context = new ThreatProvinceAlertContext
        {
            ProvinceCode = provinceCode,
            Events = events,
        };

        ThreatProvinceAlertController.NotifyProvinceAlertStarted(context);

        string plateModuleName = provinceCode;
        if (PlateMapAPI.Instance != null &&
            PlateMapAPI.Instance.TryResolvePlateMapName(provinceCode, out string plateName) &&
            !string.IsNullOrWhiteSpace(plateName))
        {
            plateModuleName = plateName;
        }

        _provinceFocusSignal = false;
        GameManager.Instance?.SwitchToProvinceLevel(plateModuleName);

        float focusTimeout = 30f;
        while (!_provinceFocusSignal && focusTimeout > 0f)
        {
            focusTimeout -= Time.deltaTime;
            yield return null;
        }

        POI_Manager.Instance?.RemoveAllPoi();
        SpawnEventPois(provinceCode, events);

        Debug.Log(
            $"[ThreatAlertFlowRunner] 省级阶段：province={provinceCode}，事件={events?.Count ?? 0}，" +
            $"停留={ThreatAlertSettings.ProvinceLevelHoldSeconds:F0}s");

        yield return WaitHoldSeconds(ThreatAlertSettings.ProvinceLevelHoldSeconds);

        // —— Vin≥3：预留进车辆大屏 ——
        if (TryFindVinMeetingThreshold(events, out string targetVin))
        {
            _vehicleStageFinished = false;
            Debug.Log($"[ThreatAlertFlowRunner][预留] Vin≥{ThreatAlertSettings.SameVinCountToEnterVehicle}，请求车辆大屏 vin={targetVin}");
            ThreatVehicleEntryRequested?.Invoke(targetVin);

            // 预留：外部未接通时短等后继续；接通后请调 NotifyVehicleStageFinished
            float vehicleWait = 0.5f;
            while (!_vehicleStageFinished && vehicleWait > 0f)
            {
                vehicleWait -= Time.deltaTime;
                yield return null;
            }

            if (!_vehicleStageFinished)
            {
                Debug.Log("[ThreatAlertFlowRunner][预留] 车辆阶段未完成通知，自动继续。");
            }

            // 车辆真实回流删除策略待后续指令；先排除该省避免国家评估死循环
            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log($"[ThreatAlertFlowRunner][预留] 车辆阶段后已排除该省数据：{provinceCode}");
        }
        else
        {
            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log($"[ThreatAlertFlowRunner] Vin 未达阈值，已删除并排除该省告警：{provinceCode}");
        }

        // —— 威胁下钻预留（等后续指令；当前仅广播后继续）——
        ThreatProvinceDrillReserved?.Invoke(context);
        Debug.Log("[ThreatAlertFlowRunner][预留] 威胁下钻钩子已触发，等待后续指令；本次直接继续返回国家。");

        ThreatProvinceAlertController.NotifyProvinceAlertCompleted(context);
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
    }

    private static void SpawnEventPois(string provinceCode, IReadOnlyList<HighRiskSecurityEventItem> events)
    {
        if (events == null || POI_Manager.Instance == null)
        {
            return;
        }

        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || !item.TryGetLongitudeLatitude(out double lon, out double lat))
            {
                continue;
            }

            POI_Manager.Instance.SpawnPoi(provinceCode, POIType.provinece_Rad, lon, lat);
        }
    }

    private static bool TryFindVinMeetingThreshold(IReadOnlyList<HighRiskSecurityEventItem> events, out string vin)
    {
        vin = null;
        if (events == null || events.Count == 0)
        {
            return false;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || string.IsNullOrWhiteSpace(item.vin))
            {
                continue;
            }

            string key = item.vin.Trim();
            counts.TryGetValue(key, out int count);
            count++;
            counts[key] = count;
            if (count >= ThreatAlertSettings.SameVinCountToEnterVehicle)
            {
                vin = key;
                return true;
            }
        }

        return false;
    }

    private IEnumerator WaitHoldSeconds(float seconds)
    {
        _skipCurrentHold = false;
        float remain = Mathf.Max(0f, seconds);
        while (remain > 0f && !_skipCurrentHold)
        {
            remain -= Time.deltaTime;
            yield return null;
        }

        _skipCurrentHold = false;
    }

    private void ApplyNoThreatIdleState()
    {
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
        ThreatProvinceAlertController.NotifyAllAlertsCompleted();
        Debug.Log("[ThreatAlertFlowRunner] 无达标省，GameManager → Default。");
    }

    private void HandleProvinceFocusCompleted(string _)
    {
        _provinceFocusSignal = true;
    }
}
