using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁告警流程协程宿主：
/// 瞬时回国家级 → 国家停留 → 省级停留 →（同 Vin≥3 则完整动画进车辆 + 拉车辆数据 + 可配置停留）→ 回国家再评估。
/// 处理中若数据再次入库：只刷新当前阶段画面，不重入流程。
/// </summary>
public class ThreatAlertFlowRunner : UnitySingle<ThreatAlertFlowRunner>
{
    private enum ThreatVisualStage
    {
        None = 0,
        CountryHold = 1,
        ProvinceHold = 2,
        VehicleHold = 3,
    }

    [Header("车辆阶段")]
    [Tooltip("Vin≥3 进入车辆级后的停留秒数；默认取 ThreatAlertSettings.VehicleLevelHoldSeconds")]
    [SerializeField] private float _vehicleLevelHoldSeconds = ThreatAlertSettings.VehicleLevelHoldSeconds;

    private Coroutine _flowRoutine;
    private bool _skipCurrentHold;
    private bool _provinceFocusSignal;
    private ThreatVisualStage _visualStage = ThreatVisualStage.None;
    private string _activeProvinceCode;

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

    /// <summary>车辆阶段可提前结束停留（外部对接完成后调用）。</summary>
    public void NotifyVehicleStageFinished()
    {
        SkipCurrentHold();
    }

    /// <summary>
    /// 数据已刷新时：按当前阶段重绘 POI/高亮，不重启流程。
    /// 国家停留若已无达标省，会跳过剩余停留以便尽快结束。
    /// </summary>
    public void RefreshVisualsFromCache()
    {
        if (_flowRoutine == null)
        {
            return;
        }

        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        switch (_visualStage)
        {
            case ThreatVisualStage.CountryHold:
            {
                IReadOnlyList<string> qualified = store.GetProvincesMeetingThreshold(
                    ThreatAlertSettings.EventsPerProvinceThreshold);
                ApplyCountryStageVisuals(qualified);
                if (qualified == null || qualified.Count == 0)
                {
                    Debug.Log("[ThreatAlertFlowRunner] 数据刷新后无达标省，跳过国家停留剩余时间。");
                    _skipCurrentHold = true;
                }

                break;
            }
            case ThreatVisualStage.ProvinceHold:
            {
                if (string.IsNullOrWhiteSpace(_activeProvinceCode))
                {
                    break;
                }

                IReadOnlyList<HighRiskSecurityEventItem> events =
                    store.GetEventsByProvince(_activeProvinceCode);
                ApplyProvinceStageVisuals(_activeProvinceCode, events);

                ThreatProvinceAlertContext context = ThreatProvinceAlertController.CurrentContext;
                if (context != null &&
                    string.Equals(context.ProvinceCode, _activeProvinceCode, StringComparison.Ordinal))
                {
                    context.Events = events;
                }

                break;
            }
            default:
                Debug.Log("[ThreatAlertFlowRunner] 数据已刷新，当前非停留阶段，画面待后续步骤使用最新缓存。");
                break;
        }
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
        _visualStage = ThreatVisualStage.None;
        _activeProvinceCode = null;
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
                yield return PlayCountryStage();

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
            _visualStage = ThreatVisualStage.None;
            _activeProvinceCode = null;
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

    private IEnumerator PlayCountryStage()
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        IReadOnlyList<string> qualified = store.GetProvincesMeetingThreshold(
            ThreatAlertSettings.EventsPerProvinceThreshold);

        _visualStage = ThreatVisualStage.CountryHold;
        _activeProvinceCode = null;
        ApplyCountryStageVisuals(qualified);

        Debug.Log(
            $"[ThreatAlertFlowRunner] 国家阶段：达标省={qualified?.Count ?? 0}，" +
            $"停留={ThreatAlertSettings.CountryLevelHoldSeconds:F0}s");

        yield return WaitHoldSeconds(ThreatAlertSettings.CountryLevelHoldSeconds);
        _visualStage = ThreatVisualStage.None;
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

        _visualStage = ThreatVisualStage.ProvinceHold;
        _activeProvinceCode = provinceCode;
        // 聚焦完成后用最新缓存绘制（可能在跳转期间已刷新）
        events = store.GetEventsByProvince(provinceCode);
        context.Events = events;
        ApplyProvinceStageVisuals(provinceCode, events);

        Debug.Log(
            $"[ThreatAlertFlowRunner] 省级阶段：province={provinceCode}，事件={events?.Count ?? 0}，" +
            $"停留={ThreatAlertSettings.ProvinceLevelHoldSeconds:F0}s");

        yield return WaitHoldSeconds(ThreatAlertSettings.ProvinceLevelHoldSeconds);
        _visualStage = ThreatVisualStage.None;

        // 停留结束后再读最新缓存做 Vin 判定
        events = store.GetEventsByProvince(provinceCode);
        context.Events = events;

        if (TryFindVinMeetingThreshold(events, out string targetVin))
        {
            Debug.Log(
                $"[ThreatAlertFlowRunner] 同 Vin≥{ThreatAlertSettings.SameVinCountToEnterVehicle}，" +
                $"进入车辆级 | province={provinceCode} | vin={targetVin}");
            ThreatVehicleEntryRequested?.Invoke(targetVin);

            yield return PlayVehicleStage(provinceCode, plateModuleName, targetVin);

            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log($"[ThreatAlertFlowRunner] 车辆阶段结束，已排除该省数据：{provinceCode}");
        }
        else
        {
            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log($"[ThreatAlertFlowRunner] Vin 未达阈值，已删除并排除该省告警：{provinceCode}");
        }

        // —— 威胁下钻预留（零件/攻击路径等后续判定；当前直接回国家）——
        ThreatProvinceDrillReserved?.Invoke(context);
        Debug.Log("[ThreatAlertFlowRunner][预留] 威胁后续下钻钩子已触发；本次返回国家继续。");

        ThreatProvinceAlertController.NotifyProvinceAlertCompleted(context);
        _activeProvinceCode = null;
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
    }

    /// <summary>
    /// 完整动画进入车辆级 → 请求车辆态势双接口（vin 作 encryptVin）→ 可配置停留 → 结束。
    /// </summary>
    private IEnumerator PlayVehicleStage(string provinceCode, string plateModuleName, string encryptVin)
    {
        string provinceDisplayName = ResolveProvinceDisplayName(provinceCode, plateModuleName);
        yield return TransitionToVehicleLevel(provinceDisplayName, plateModuleName);

        RequestVehicleDataWithLogs(encryptVin);

        _visualStage = ThreatVisualStage.VehicleHold;
        float holdSeconds = Mathf.Max(0.1f, _vehicleLevelHoldSeconds);
        Debug.Log(
            $"[ThreatAlertFlowRunner] 车辆级停留开始 | vin={encryptVin} | " +
            $"seconds={holdSeconds:F0}（Inspector 可调 _vehicleLevelHoldSeconds）");

        yield return WaitHoldSeconds(holdSeconds);
        _visualStage = ThreatVisualStage.None;
        Debug.Log($"[ThreatAlertFlowRunner] 车辆级停留结束 | vin={encryptVin}");
    }

    private IEnumerator TransitionToVehicleLevel(string provinceName, string provinceModuleName)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CurrentState == GameManager.ControlState.VehicleLevel)
        {
            yield break;
        }

        ControlStateHierarchyTransitionController hierarchy =
            ControlStateHierarchyTransitionController.Instance;
        if (hierarchy == null)
        {
            Debug.LogWarning(
                "[ThreatAlertFlowRunner] 未找到 ControlStateHierarchyTransitionController，无法进入车辆级。");
            yield break;
        }

        while (hierarchy.IsBootstrapping)
        {
            yield return null;
        }

        GameManager.ControlState from = gm != null
            ? gm.CurrentState
            : GameManager.ControlState.ProvinceLevel;

        bool started = hierarchy.TransitionToState(
            useInstantTransition: false,
            targetState: GameManager.ControlState.VehicleLevel,
            provinceName: provinceName,
            provinceModuleName: provinceModuleName);
        if (!started)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] 从 {from} 跳转车辆级失败。");
            yield break;
        }

        Debug.Log($"[ThreatAlertFlowRunner] 完整动画跳转车辆级：{from} → VehicleLevel | province={provinceName}");

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

        float confirmTimeout = 15f;
        while (gm != null &&
               gm.CurrentState != GameManager.ControlState.VehicleLevel &&
               confirmTimeout > 0f)
        {
            confirmTimeout -= Time.deltaTime;
            yield return null;
        }

        if (gm != null && gm.CurrentState != GameManager.ControlState.VehicleLevel)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 跳转后仍非车辆级：{gm.CurrentState}，继续车辆数据请求与停留。");
        }
    }

    private static void RequestVehicleDataWithLogs(string encryptVin)
    {
        Debug.Log(
            $"[ThreatAlertFlowRunner] 调用车辆信息加载接口（防护状态+攻击链路）| encryptVin={encryptVin}");

        CarVehicleDataController controller = CarVehicleDataController.Instance;
        if (controller == null)
        {
            Debug.LogWarning(
                "[ThreatAlertFlowRunner] 未找到 CarVehicleDataController，跳过车辆信息请求（请确认 Manager 下已挂载）。");
            return;
        }

        if (controller.IsRequesting)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 车辆接口已有请求进行中，跳过本次 | vin={encryptVin}");
            return;
        }

        controller.Request(
            encryptVin,
            startTime: null,
            endTime: null,
            onCompleted: (ok, error) =>
            {
                if (ok)
                {
                    Debug.Log(
                        $"[ThreatAlertFlowRunner] 车辆信息加载成功 | encryptVin={encryptVin} | " +
                        $"slides={CarVehicleDataStore.Instance.BuildPartSlides().Count}");
                    return;
                }

                Debug.LogWarning(
                    $"[ThreatAlertFlowRunner] 车辆信息加载失败（网络可能不通，流程继续）| " +
                    $"encryptVin={encryptVin} | error={error}");
            });
    }

    private static string ResolveProvinceDisplayName(string provinceCode, string plateModuleName)
    {
        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string adcodeName) &&
            !string.IsNullOrWhiteSpace(adcodeName))
        {
            return adcodeName.Trim();
        }

        if (PlateMapAPI.Instance != null &&
            PlateMapAPI.Instance.TryGetProvinceName(provinceCode, out string boundaryName) &&
            !string.IsNullOrWhiteSpace(boundaryName))
        {
            return boundaryName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(plateModuleName))
        {
            return plateModuleName.Trim();
        }

        return provinceCode ?? string.Empty;
    }

    private static void ApplyCountryStageVisuals(IReadOnlyList<string> qualifiedProvinceCodes)
    {
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();

        if (qualifiedProvinceCodes == null || qualifiedProvinceCodes.Count == 0)
        {
            return;
        }

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
    }

    private static void ApplyProvinceStageVisuals(
        string provinceCode,
        IReadOnlyList<HighRiskSecurityEventItem> events)
    {
        POI_Manager.Instance?.RemoveAllPoi();
        SpawnEventPois(provinceCode, events);
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
