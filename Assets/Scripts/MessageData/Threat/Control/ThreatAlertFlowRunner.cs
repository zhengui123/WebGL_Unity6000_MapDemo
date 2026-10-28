using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 威胁告警流程协程宿主：
/// 任意非国家级触发时先瞬时回国家，再从头：国家停留 → 省级停留 → Vin&gt;3 下钻（车辆 → 攻击链路 → 零部件）→ 回国家再评估。
/// 省级/车辆/攻击链路停留期间若数据再次入库：刷新当前阶段画面与缓存，不重入流程。
/// </summary>
public class ThreatAlertFlowRunner : UnitySingle<ThreatAlertFlowRunner>
{
    private enum ThreatVisualStage
    {
        None = 0,
        CountryHold = 1,
        ProvinceHold = 2,
        VehicleHold = 3,
        AttackPathHold = 4,
        PartHold = 5,
    }

    [Header("国家/省级停留（秒）")]
    [Tooltip("国家级停留")]
    [SerializeField] private float _countryLevelHoldSeconds = ThreatAlertSettings.CountryLevelHoldSeconds;
    [Tooltip("省级（板块）停留")]
    [SerializeField] private float _provinceLevelHoldSeconds = ThreatAlertSettings.ProvinceLevelHoldSeconds;

    [Header("下钻停留（秒，可分别配置）")]
    [Tooltip("车辆级停留")]
    [SerializeField] private float _vehicleLevelHoldSeconds = ThreatAlertSettings.VehicleLevelHoldSeconds;
    [Tooltip("攻击链路级停留")]
    [SerializeField] private float _attackPathLevelHoldSeconds = ThreatAlertSettings.AttackPathLevelHoldSeconds;
    [Tooltip("每个零部件级停留")]
    [SerializeField] private float _partLevelHoldSeconds = ThreatAlertSettings.PartLevelHoldSeconds;

    [Header("主动退出冷却（秒）")]
    [Tooltip("主动退出/打断威胁下钻后，暂停威胁检测的冷却时长")]
    [SerializeField] private float _interruptCooldownSeconds = ThreatAlertSettings.InterruptCooldownSeconds;

    [Header("消费标记")]
    [Tooltip("开启时：威胁下钻自然结束或主动退出时，对本轮已播省再补传一遍 HighRiskEventConsumedMark。每省播完即时上传不受影响。")]
    [SerializeField] private bool _flushConsumedMarkOnFlowEnd = true;

    [Header("威胁轮询")]
    [Tooltip("开启时：威胁下钻进行中暂停高危事件自动轮询；自然结束立刻恢复；主动退出则等冷却结束后再恢复（原逻辑）。")]
    [SerializeField] private bool _pauseThreatPollingDuringDrill = true;

    [Header("调试")]
    [Tooltip("Console 输出 [计时] 日志，区分过渡耗时与停留耗时")]
    [SerializeField] private bool _logStageTiming = true;

    private static readonly string[] EmptyPartEventIds = Array.Empty<string>();

    private Coroutine _flowRoutine;
    private Coroutine _interruptCooldownRoutine;
    private bool _skipCurrentHold;
    private bool _provinceFocusSignal;
    private bool _transitionStepDone;
    private ThreatVisualStage _visualStage = ThreatVisualStage.None;
    private string _activeProvinceCode;
    private string _activePlateModuleName;
    private string _activeEncryptVin;
    private string _activeVin;
    private string[] _activePartEventIds = EmptyPartEventIds;
    private bool _lastTransitionSucceeded;
    private float _holdCountdownRemaining;
    private float _holdCountdownTotal;
    private float _interruptCooldownRemaining;

    /// <summary>本轮已播完省及其 eventId（删缓存前记录，供结束/退出时补传消费标记）。</summary>
    private readonly List<SessionConsumedProvince> _sessionConsumedProvinces =
        new List<SessionConsumedProvince>(8);

    /// <summary>本轮是否因下钻暂停了威胁自动轮询（自然结束时恢复；主动退出交给冷却）。</summary>
    private bool _threatFlowPausedPolling;

    private struct SessionConsumedProvince
    {
        public string ProvinceCode;
        public string[] EventIds;
    }

    /// <summary>是否正在跑威胁流程。</summary>
    public bool IsRunning => _flowRoutine != null;

    /// <summary>当前威胁流程正在处理的省/国家 code；未进入省级阶段时为空。</summary>
    public string ActiveProvinceCode => _activeProvinceCode;

    /// <summary>当前威胁下钻车辆 encryptVin；未进入 Vin 阶段时为空。</summary>
    public string ActiveEncryptVin => _activeEncryptVin;

    /// <summary>当前威胁下钻车辆明文 vin；未进入 Vin 阶段时为空。</summary>
    public string ActiveVin => _activeVin;

    /// <summary>当前零部件停留对应的全部 pending eventId；非零件停留时为空数组。</summary>
    public string[] ActivePartEventIds =>
        _activePartEventIds != null ? _activePartEventIds : EmptyPartEventIds;

    /// <summary>是否处于主动打断后的冷却期。</summary>
    public bool IsInInterruptCooldown => _interruptCooldownRoutine != null;

    /// <summary>冷却剩余秒数。</summary>
    public float InterruptCooldownRemaining => Mathf.Max(0f, _interruptCooldownRemaining);

    /// <summary>Inspector 配置的打断冷却秒数。</summary>
    public float ConfiguredInterruptCooldownSeconds => _interruptCooldownSeconds;

    /// <summary>Inspector 配置的国家级停留秒数。</summary>
    public float ConfiguredCountryHoldSeconds => _countryLevelHoldSeconds;

    /// <summary>Inspector 配置的省级停留秒数。</summary>
    public float ConfiguredProvinceHoldSeconds => _provinceLevelHoldSeconds;

    /// <summary>Inspector 配置的车辆级停留秒数。</summary>
    public float ConfiguredVehicleHoldSeconds => _vehicleLevelHoldSeconds;

    /// <summary>Inspector 配置的攻击链路级停留秒数。</summary>
    public float ConfiguredAttackPathHoldSeconds => _attackPathLevelHoldSeconds;

    /// <summary>Inspector 配置的零件级停留秒数。</summary>
    public float ConfiguredPartHoldSeconds => _partLevelHoldSeconds;

    /// <summary>当前停留倒计时剩余秒数（向上取整展示）。</summary>
    public float HoldCountdownRemaining => _holdCountdownRemaining;

    /// <summary>当前停留配置总秒数。</summary>
    public float HoldCountdownTotal => _holdCountdownTotal;

    /// <summary>当前是否处于车辆/攻击链路/零件级（威胁下钻子树）。</summary>
    public static bool IsInVehicleDrillControlState()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return false;
        }

        GameManager.ControlState state = gm.CurrentState;
        return state == GameManager.ControlState.VehicleLevel
               || state == GameManager.ControlState.PartLevel
               || state == GameManager.ControlState.AttackPathLevel;
    }

    /// <summary>Vin&gt;3 时请求进入车辆大屏（参数为 Vin）。</summary>
    public static event Action<string> ThreatVehicleEntryRequested;

    /// <summary>省级全部 Vin 下钻完成后的钩子。</summary>
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
        CancelInterruptCooldown();
        ThreatProvinceAlertController.NotifyFlowStopped();
    }

    /// <summary>启动一轮威胁流程（进行中或冷却中则忽略）。始终从国家级开始下钻。</summary>
    public bool TryStartThreatFlow()
    {
        if (!ThreatAlertSettings.IsThreatFeatureEnabledOnCurrentPlatform)
        {
            LogManager.LogFeature(
                "[ThreatAlertFlowRunner] 当前平台关闭威胁功能，拒绝启动 ThreatAlertFlowRunner。");
            return false;
        }

        if (_flowRoutine != null)
        {
            return false;
        }

        if (IsInInterruptCooldown)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 威胁冷却中，拒绝启动流程 | 剩余={_interruptCooldownRemaining:F0}s");
            return false;
        }

        bool interruptedCarousel = TryStopAutoCarouselForThreatDrill(out string carouselMode);
        if (interruptedCarousel)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 已停止自动轮播（{carouselMode}），威胁下钻从全国开始。");
        }

        GameManager.ControlState startState = GameManager.Instance != null
            ? GameManager.Instance.CurrentState
            : GameManager.ControlState.CountryLevel;
        if (startState != GameManager.ControlState.CountryLevel)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 当前为 {startState}，将先瞬时回国家级再从头下钻。");
        }

        // 原：下钻启动时上报一次达标省消费标记。现改为每省播放结束后用该省最新缓存上传；
        // 整轮结束/主动退出再按本轮已播省补传一遍。
        // HighRiskEventConsumedMarkApi.RequestFromCacheMeetingThreshold();
        ClearSessionConsumedProvinces();
        PauseThreatPollingForDrillIfEnabled();

        _flowRoutine = StartCoroutine(ThreatFlowRoutine());
        return true;
    }

    /// <summary>
    /// 若正在自动轮播或延时等待开轮播，则停止并取消延时；返回是否打断了轮播相关状态。
    /// </summary>
    private static bool TryStopAutoCarouselForThreatDrill(out string modeLabel)
    {
        modeLabel = null;
        BigScreenCarouselController carousel = BigScreenCarouselController.Instance;
        MapApi mapApi = MapApi.Instance;

        bool wasCarouselEnabled = carousel != null && carousel.IsAutoCarouselEnabled;
        bool wasDelayedWaiting = carousel != null && carousel.IsWaitingDelayedStart;
        if (!wasCarouselEnabled && !wasDelayedWaiting)
        {
            // 无轮播控制器时仍尝试关一次，兼容仅 MapApi 路径。
            if (mapApi != null && mapApi.IsBigScreenAutoCarouselEnabled())
            {
                mapApi.SetBigScreenAutoCarouselEnabled(false);
                mapApi.CancelBigScreenAutoCarouselDelayedStart();
                modeLabel = "轮播开启";
                return true;
            }

            return false;
        }

        if (wasCarouselEnabled && wasDelayedWaiting)
        {
            modeLabel = "轮播中+延时等待";
        }
        else if (wasCarouselEnabled)
        {
            modeLabel = "轮播中";
        }
        else
        {
            modeLabel = "延时等待开轮播";
        }

        if (mapApi != null)
        {
            mapApi.SetBigScreenAutoCarouselEnabled(false);
            mapApi.CancelBigScreenAutoCarouselDelayedStart();
        }
        else if (carousel != null)
        {
            carousel.SetAutoCarouselEnabled(false);
            carousel.CancelDelayedStart();
        }

        return true;
    }

    /// <summary>跳过当前停留计时（国家/省/车辆/攻击链路/零件；Demo「跳过停留」可用）。</summary>
    public void SkipCurrentHold()
    {
        _skipCurrentHold = true;
    }

    /// <summary>当前是否处于任一停留阶段。</summary>
    public bool IsInHoldStage =>
        _flowRoutine != null && _visualStage != ThreatVisualStage.None;

    /// <summary>当前停留阶段描述（Demo 展示用）。</summary>
    public string CurrentHoldStageLabel => _visualStage switch
    {
        ThreatVisualStage.CountryHold => "国家级停留",
        ThreatVisualStage.ProvinceHold => "省级停留",
        ThreatVisualStage.VehicleHold => "车辆级停留",
        ThreatVisualStage.AttackPathHold => "攻击链路停留",
        ThreatVisualStage.PartHold => "零件级停留",
        _ => "过渡/请求中（将跳过下一停留）",
    };

    /// <summary>Demo 面板用：当前流程状态与倒计时文案。</summary>
    public string GetFlowStatusText()
    {
        if (IsInInterruptCooldown)
        {
            return
                $"流程：威胁冷却中 | 倒计时 {Mathf.Ceil(_interruptCooldownRemaining):F0}s / " +
                $"{Mathf.Max(0.1f, _interruptCooldownSeconds):F0}s";
        }

        if (!IsRunning)
        {
            return "流程：空闲";
        }

        string detail = CurrentHoldStageLabel;
        if (!string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            detail += $" | 省={_activeProvinceCode}";
        }

        if (!string.IsNullOrWhiteSpace(_activeEncryptVin))
        {
            detail += $" | encryptVin={_activeEncryptVin}";
        }

        if (!string.IsNullOrWhiteSpace(_activeVin))
        {
            detail += $" | vin={_activeVin}";
        }

        if (IsInHoldStage && _holdCountdownTotal > 0f)
        {
            return
                $"流程：{detail} | 倒计时 {Mathf.Ceil(_holdCountdownRemaining):F0}s / {_holdCountdownTotal:F0}s";
        }

        return $"流程：{detail}";
    }

    /// <summary>
    /// 主动退出威胁下钻：停在当前级别，清理威胁 POI/高亮，进入冷却（不启动轮播）。
    /// </summary>
    public bool ExitThreatDrill()
    {
        bool wasRunning = IsRunning;
        StopFlowInternal();
        ThreatProvinceAlertController.NotifyFlowStopped();

        // 主动退出：对本轮已播完的省补传消费标记（与每省即时上传并存，作兜底）。
        FlushSessionConsumedMarks("ExitThreatDrill");

        // 主动退出不立刻恢复轮询：交给冷却结束后再开启（与下钻中已暂停兼容）。
        _threatFlowPausedPolling = false;

        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.ResolveForWorldMap()?.ClearHighlight();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);

        // 退出打断不改 ControlState，保持当前级别。
        StartInterruptCooldown();
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 已退出威胁下钻 | wasRunning={wasRunning} | " +
            $"control={GameManager.Instance?.CurrentState} | 冷却={_interruptCooldownSeconds:F0}s");
        return true;
    }

    /// <summary>
    /// 刷新威胁冷却：仅冷却中有效，重新计满配置秒数。
    /// </summary>
    public bool RefreshThreatCooldown()
    {
        if (!IsInInterruptCooldown)
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] 当前不在威胁冷却中，忽略刷新冷却。");
            return false;
        }

        StartInterruptCooldown();
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 已刷新威胁冷却 | {_interruptCooldownSeconds:F0}s");
        return true;
    }

    /// <summary>车辆/攻击链路/零件阶段可提前结束停留。</summary>
    public void NotifyVehicleStageFinished()
    {
        SkipCurrentHold();
    }

    /// <summary>
    /// 数据已刷新时：按当前阶段重绘画面，不重启流程。
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
                    LogManager.LogFeature("[ThreatAlertFlowRunner] 数据刷新后无达标省，跳过国家停留剩余时间。");
                    _skipCurrentHold = true;
                }

                break;
            }
            case ThreatVisualStage.ProvinceHold:
            {
                RefreshProvinceStageVisuals(store);
                break;
            }
            case ThreatVisualStage.VehicleHold:
            {
                RefreshVehicleStageVisuals();
                break;
            }
            case ThreatVisualStage.AttackPathHold:
            {
                RefreshAttackPathStageVisuals();
                break;
            }
            case ThreatVisualStage.PartHold:
            {
                LogManager.LogFeature("[ThreatAlertFlowRunner] 零件级停留中收到新数据，保持当前零件展示。");
                break;
            }
            default:
                LogManager.LogFeature("[ThreatAlertFlowRunner] 过渡动画中收到新数据，待当前步骤完成后使用最新缓存。");
                break;
        }
    }

    /// <summary>停止流程并清理 POI/高亮（调试重置，同时取消冷却）。</summary>
    public void StopAndResetVisuals()
    {
        StopFlowInternal();
        CancelInterruptCooldown();
        _threatFlowPausedPolling = false;
        // 调试取消冷却：若宿主仍开启轮询，恢复请求。
        HighRiskSecurityEventApiController.Instance?.OnThreatInterruptCooldownEnded();
        ThreatProvinceAlertController.NotifyFlowStopped();
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.ResolveForWorldMap()?.ClearHighlight();
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
        _activePlateModuleName = null;
        _activeEncryptVin = null;
        _activeVin = null;
        _activePartEventIds = EmptyPartEventIds;
        ClearHoldCountdown();
    }

    private void StartInterruptCooldown()
    {
        CancelInterruptCooldown();
        HighRiskSecurityEventApiController.Instance?.OnThreatInterruptCooldownStarted();
        _interruptCooldownRoutine = StartCoroutine(InterruptCooldownRoutine());
    }

    private void CancelInterruptCooldown()
    {
        if (_interruptCooldownRoutine != null)
        {
            StopCoroutine(_interruptCooldownRoutine);
            _interruptCooldownRoutine = null;
        }

        _interruptCooldownRemaining = 0f;
    }

    private IEnumerator InterruptCooldownRoutine()
    {
        float total = Mathf.Max(0.1f, _interruptCooldownSeconds);
        _interruptCooldownRemaining = total;
        LogManager.LogFeature($"[ThreatAlertFlowRunner] 威胁冷却开始 | {total:F0}s（期间不检测）");

        while (_interruptCooldownRemaining > 0f)
        {
            _interruptCooldownRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        _interruptCooldownRemaining = 0f;
        _interruptCooldownRoutine = null;
        LogManager.LogFeature("[ThreatAlertFlowRunner] 威胁冷却结束，交由轮询控制器先请求接口再评估。");
        // 不再直接用本地缓存 Evaluate：先 Request，入库后再 EvaluateAfterDataUpdated。
        HighRiskSecurityEventApiController.Instance?.OnThreatInterruptCooldownEnded();
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

                // 非国家级：先瞬时回国家，再国家停留 → 省停留 → Vin 下钻（不从车辆子树续钻）。
                yield return EnsureCountryLevel();
                yield return PlayCountryStage();

                qualified = store.GetProvincesMeetingThreshold(ThreatAlertSettings.EventsPerProvinceThreshold);
                if (qualified == null || qualified.Count == 0)
                {
                    ApplyNoThreatIdleState();
                    yield break;
                }

                string provinceCode = qualified[0];
                yield return PlayProvinceStage(provinceCode);

                yield return EnsureCountryLevelFromProvince();
            }
        }
        finally
        {
            _visualStage = ThreatVisualStage.None;
            _activeProvinceCode = null;
            _activePlateModuleName = null;
            _activeEncryptVin = null;
            _activeVin = null;
            _activePartEventIds = EmptyPartEventIds;
            _flowRoutine = null;
            ThreatProvinceAlertController.NotifyFlowStopped();
        }
    }

    private IEnumerator EnsureCountryLevel()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        // 先等当前过渡空闲，避免与进行中的层级动画抢状态。
        yield return WaitForTransitionControllersIdle();

        if (gm.CurrentState != GameManager.ControlState.CountryLevel)
        {
            ControlStateHierarchyTransitionController hierarchy =
                ControlStateHierarchyTransitionController.Instance;
            if (hierarchy == null)
            {
                LogManager.LogFeatureWarning(
                    "[ThreatAlertFlowRunner] 未找到 ControlStateHierarchyTransitionController，无法跳回国家级。");
                yield break;
            }

            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 瞬时回国家级 | from={gm.CurrentState}");
            yield return WaitForHierarchyTransition(
                hierarchy,
                GameManager.ControlState.CountryLevel,
                useInstant: true,
                provinceCode: null,
                confirmTimeoutSeconds: 8f);
        }

        // 画面归位全国 Home，避免仍停在省/车视角。
        PlateMapDisplayController.Instance?.SnapCameraToCountryHomeImmediate();
    }

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

        float countryHold = Mathf.Max(0.1f, _countryLevelHoldSeconds);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 国家阶段：达标省={qualified?.Count ?? 0}，" +
            $"停留={countryHold:F1}s");

        yield return WaitHoldSeconds(countryHold, "国家级停留");
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

        _activePlateModuleName = plateModuleName;
        _activeProvinceCode = provinceCode;

        _provinceFocusSignal = false;
        GameManager.Instance?.SwitchToProvinceLevel(plateModuleName);

        float focusTimeout = 30f;
        while (!_provinceFocusSignal && focusTimeout > 0f)
        {
            focusTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        _visualStage = ThreatVisualStage.ProvinceHold;
        events = store.GetEventsByProvince(provinceCode);
        context.Events = events;
        ApplyProvinceStageVisuals(provinceCode, events);

        float provinceHold = Mathf.Max(0.1f, _provinceLevelHoldSeconds);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 省级阶段：province={provinceCode}，事件={events?.Count ?? 0}，" +
            $"停留={provinceHold:F1}s");

        yield return WaitHoldSeconds(provinceHold, $"省级停留 province={provinceCode}");
        _visualStage = ThreatVisualStage.None;

        events = store.GetEventsByProvince(provinceCode);
        context.Events = events;
        yield return PlayProvinceVinDrillLoop(provinceCode, plateModuleName, context);
    }

    private IEnumerator PlayProvinceVinDrillLoop(
        string provinceCode,
        string plateModuleName,
        ThreatProvinceAlertContext context)
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        IReadOnlyList<HighRiskSecurityEventItem> events = store.GetEventsByProvince(provinceCode);
        List<string> qualifyingVins = CollectVinsMeetingThreshold(events);

        if (qualifyingVins.Count > 0)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 省级达标 Vin 数={qualifyingVins.Count}，开始轮流下钻 | province={provinceCode} | " +
                $"encryptVin列表=[{string.Join(" | ", qualifyingVins)}]");

            string provinceDisplayName = ResolveProvinceDisplayName(provinceCode, plateModuleName);
            for (int i = 0; i < qualifyingVins.Count; i++)
            {
                string encryptVin = qualifyingVins[i];
                string nextEncryptVin = i + 1 < qualifyingVins.Count ? qualifyingVins[i + 1] : null;
                bool hasNextVin = !string.IsNullOrWhiteSpace(nextEncryptVin);
                LogManager.LogFeature(
                    $"[ThreatAlertFlowRunner] Vin 下钻 ({i + 1}/{qualifyingVins.Count}) | " +
                    $"encryptVin={encryptVin} | nextEncryptVin={nextEncryptVin ?? "(无)"} | " +
                    $"hasNextVin={hasNextVin} | control={GameManager.Instance?.CurrentState}");

                // 先切换威胁缓存中的 vin/encryptVin；宿主回调改到进入车辆级之后（见 PlayVinDrillChain）。
                CacheActiveVehicleIdentity(encryptVin, $"省内Vin循环入口({i + 1}/{qualifyingVins.Count})");
                ThreatVehicleEntryRequested?.Invoke(encryptVin);
                yield return PlayVinDrillChain(
                    provinceCode,
                    provinceDisplayName,
                    plateModuleName,
                    encryptVin,
                    nextEncryptVin);
            }

            RecordProvinceConsumedForSession(provinceCode);
            HighRiskEventConsumedMarkApi.RequestFromProvinceCache(provinceCode);
            store.RemoveProvinceEventsAndExclude(provinceCode);
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 该省全部 Vin 下钻完成，将回国家级处理下一达标省 | province={provinceCode}");
        }
        else
        {
            RecordProvinceConsumedForSession(provinceCode);
            HighRiskEventConsumedMarkApi.RequestFromProvinceCache(provinceCode);
            store.RemoveProvinceEventsAndExclude(provinceCode);
            LogManager.LogFeature($"[ThreatAlertFlowRunner] 无 Vin 达阈值，已删除并排除该省告警：{provinceCode}");
        }

        ThreatProvinceDrillReserved?.Invoke(context);
        ThreatProvinceAlertController.NotifyProvinceAlertCompleted(context);
        _activeProvinceCode = null;
        _activePlateModuleName = null;
        _activeEncryptVin = null;
        _activeVin = null;
        _activePartEventIds = EmptyPartEventIds;
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.ResolveForWorldMap()?.ClearHighlight();
    }

    /// <summary>
    /// 已在车辆级时：身份已切到新 Vin 后，补发车辆级 onUnityControlStateTransition（开始+完成），
    /// 再进入后续后端查询。首辆从省进车时由正常过渡发回调，此处跳过。
    /// </summary>
    private IEnumerator NotifyHostVehicleLevelAfterVinSwitchedIfNeeded(string encryptVin)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameManager.ControlState.VehicleLevel)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 换车宿主回调跳过（当前非车辆级） | control={gm?.CurrentState} | " +
                $"encryptVin={encryptVin} | activeEncryptVin={_activeEncryptVin} | activeVin={_activeVin}");
            yield break;
        }

        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 同省换车：Vin 已切换，准备通知宿主车辆级 | encryptVin={encryptVin} | " +
            $"activeEncryptVin={_activeEncryptVin} | activeVin={_activeVin} | " +
            $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin}");
        NotifyHostVehicleLevelVinSwitched();
        yield return null;
    }

    /// <summary>宿主回调：车辆级同级换车（from=3→to=3 开始，再 from=-1→to=3 完成）。</summary>
    private static void NotifyHostVehicleLevelVinSwitched()
    {
        int vehicle = (int)GameManager.ControlState.VehicleLevel;
        ControlStateTransitionNotify started = ControlStateTransitionNotifyBuilder.BuildStarted(
            vehicle,
            vehicle,
            null,
            0);
        ControlStateTransitionNotify completed = ControlStateTransitionNotifyBuilder.BuildCompleted(
            vehicle,
            null,
            0);
        ThreatAlertFlowRunner runner = Instance;
        string expectEncrypt = runner != null ? runner.ActiveEncryptVin : null;
        bool startMismatch = !string.IsNullOrWhiteSpace(expectEncrypt) &&
                             !string.Equals(started.encryptVin, expectEncrypt, StringComparison.Ordinal);
        bool doneMismatch = !string.IsNullOrWhiteSpace(expectEncrypt) &&
                            !string.Equals(completed.encryptVin, expectEncrypt, StringComparison.Ordinal);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 换车宿主回调载荷 | expectEncryptVin={expectEncrypt} | " +
            $"start vin={started.vin} encryptVin={started.encryptVin} mismatch={startMismatch} " +
            $"eventIds={ControlStateTransitionNotifyBuilder.FormatEventIdsForLog(started.eventIds)} | " +
            $"done vin={completed.vin} encryptVin={completed.encryptVin} mismatch={doneMismatch} " +
            $"eventIds={ControlStateTransitionNotifyBuilder.FormatEventIdsForLog(completed.eventIds)}");
        if (startMismatch || doneMismatch)
        {
            LogManager.LogFeatureWarning(
                "[ThreatAlertFlowRunner] 换车宿主回调 encryptVin 与 ActiveEncryptVin 不一致，前端可能仍显示旧车");
        }

        AndroidMessage android = AndroidMessage.Instance;
        if (android != null)
        {
            LogManager.LogFeature("[ThreatAlertFlowRunner] 换车宿主通道=AndroidMessage");
            android.CallAndroidControlStateTransition(vehicle, vehicle);
            android.CallAndroidControlStateTransitionCompleted(vehicle);
            return;
        }

        WebGLAPI webgl = WebGLAPI.Instance;
        if (webgl != null)
        {
            LogManager.LogFeature("[ThreatAlertFlowRunner] 换车宿主通道=WebGLAPI");
            webgl.CallAndroidControlStateTransition(vehicle, vehicle);
            webgl.CallAndroidControlStateTransitionCompleted(vehicle);
            return;
        }

        LogManager.LogFeatureWarning(
            "[ThreatAlertFlowRunner] 换车宿主回调失败：AndroidMessage/WebGLAPI 均未找到，前端仍可能显示旧 Vin");
    }

    /// <summary>
    /// 回车辆级过渡前先切到下一辆 Vin，保证 Part/攻击链路→车辆 的宿主回调已是新 Vin。
    /// </summary>
    private IEnumerator SwitchToNextVinThenReturnToVehicle(string currentEncryptVin, string nextEncryptVin)
    {
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 准备切下一辆：回车辆前回填新 Vin | " +
            $"fromEncryptVin={currentEncryptVin} | toEncryptVin={nextEncryptVin} | " +
            $"beforeActiveEncryptVin={_activeEncryptVin} | control={GameManager.Instance?.CurrentState}");
        CacheActiveVehicleIdentity(nextEncryptVin, "回车辆前切换下一Vin");

        if (!string.Equals(_activeEncryptVin, nextEncryptVin?.Trim(), StringComparison.Ordinal))
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 回车辆前身份切换异常 | 期望={nextEncryptVin} | 实际Active={_activeEncryptVin}");
        }

        yield return ReturnToVehicleLevelFromDrill();
        yield return null;

        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 已回车辆级（宿主 Part/攻击→车辆 回调应已带新 Vin） | " +
            $"activeEncryptVin={_activeEncryptVin} | activeVin={_activeVin} | " +
            $"control={GameManager.Instance?.CurrentState} | " +
            $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin ?? "(空)"}");

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.ControlState.VehicleLevel)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 回车辆后控制状态未到 VehicleLevel | control={GameManager.Instance.CurrentState} | " +
                $"ActiveEncryptVin={_activeEncryptVin}");
        }
    }

    /// <summary>单 Vin：车辆级 → 攻击链路级 → 轮流零件级；有下一 Vin 则回车辆级，否则留给省阶段瞬时回国家。</summary>
    private IEnumerator PlayVinDrillChain(
        string provinceCode,
        string provinceDisplayName,
        string plateModuleName,
        string encryptVin,
        string nextEncryptVin)
    {
        bool hasNextVin = !string.IsNullOrWhiteSpace(nextEncryptVin);
        _activeProvinceCode = provinceCode;
        _activePlateModuleName = plateModuleName;
        // 循环入口已切身份；此处再写一次，保证单独调用链时仍正确。
        CacheActiveVehicleIdentity(encryptVin, "PlayVinDrillChain入口");

        GameManager gmBeforeEnsure = GameManager.Instance;
        bool alreadyAtVehicleLevel = gmBeforeEnsure != null &&
                                     gmBeforeEnsure.CurrentState == GameManager.ControlState.VehicleLevel;
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] PlayVinDrillChain 进入车辆前 | encryptVin={encryptVin} | " +
            $"ActiveEncryptVin={_activeEncryptVin} | ActiveVin={_activeVin} | " +
            $"alreadyAtVehicleLevel={alreadyAtVehicleLevel} | control={gmBeforeEnsure?.CurrentState} | " +
            $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin ?? "(空)"}");

        yield return RunTimedStep("进入车辆级", EnsureAtVehicleLevel(provinceCode, encryptVin));

        // 同省第二辆起已在车辆级：省→车过渡不会再发，须在请求前补发车辆级宿主回调（载荷应为新 Vin）。
        if (alreadyAtVehicleLevel)
        {
            yield return NotifyHostVehicleLevelAfterVinSwitchedIfNeeded(encryptVin);
        }

        yield return RunTimedStep("车辆数据请求(非阻塞)", RequestVehicleDataAndWait(encryptVin));

        _visualStage = ThreatVisualStage.VehicleHold;
        float vehicleHold = Mathf.Max(0.1f, _vehicleLevelHoldSeconds);
        LogManager.LogFeature($"[ThreatAlertFlowRunner] 车辆级停留 | vin={encryptVin} | {vehicleHold:F1}s");
        yield return WaitHoldSeconds(vehicleHold, $"车辆级停留 vin={encryptVin}");
        _visualStage = ThreatVisualStage.None;

        LogManager.LogFeature($"[ThreatAlertFlowRunner] 车辆级停留结束，进入攻击链路 | vin={encryptVin}");
        yield return RunTimedStep($"车辆→攻击链路 vin={encryptVin}", TransitionToAttackPathLevelAndWait());
        bool atAttackPathLevel = IsAtAttackPathLevel();
        if (!atAttackPathLevel)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 车辆→攻击链路过渡未完成，仍继续后续阶段（使用已有缓存） | vin={encryptVin} | " +
                $"control={GameManager.Instance?.CurrentState}");
        }

        ApplyAttackPathStageVisuals(atAttackPathLevel);

        _visualStage = ThreatVisualStage.AttackPathHold;
        float attackHold = Mathf.Max(0.1f, _attackPathLevelHoldSeconds);
        LogManager.LogFeature($"[ThreatAlertFlowRunner] 攻击链路级停留 | vin={encryptVin} | {attackHold:F1}s");
        yield return WaitHoldSeconds(attackHold, $"攻击链路级停留 vin={encryptVin}");
        _visualStage = ThreatVisualStage.None;

        List<ThreatPartDrillStep> partSteps = ResolvePartDrillSteps();
        if (partSteps.Count == 0)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] 无可用零部件，跳过零件级 | vin={encryptVin}");
            if (hasNextVin)
            {
                yield return SwitchToNextVinThenReturnToVehicle(encryptVin, nextEncryptVin);
            }
            else
            {
                LogManager.LogFeature(
                    $"[ThreatAlertFlowRunner] 省内最后一 Vin 无零件，跳过回车辆，随后瞬时回国家级 | vin={encryptVin}");
            }

            yield break;
        }

        for (int i = 0; i < partSteps.Count; i++)
        {
            ThreatPartDrillStep step = partSteps[i];
            string partId = step.PartId;
            int partType = step.PartType;
            _activePartEventIds = step.EventIds != null && step.EventIds.Length > 0
                ? step.EventIds
                : EmptyPartEventIds;

            bool fromAttackPath = i == 0 &&
                                  GameManager.Instance != null &&
                                  GameManager.Instance.CurrentState == GameManager.ControlState.AttackPathLevel;

            if (fromAttackPath)
            {
                yield return RunTimedStep(
                    $"攻击路径→零件 type={partType} {partId}",
                    TransitionAttackPathToPartAndWait(partId, partType));
            }
            else
            {
                bool partToPart = GameManager.Instance != null &&
                                 GameManager.Instance.CurrentState == GameManager.ControlState.PartLevel;
                string stepLabel = partToPart
                    ? $"零件→零件 type={partType} {partId}"
                    : $"车辆→零件 type={partType} {partId}";
                yield return RunTimedStep(stepLabel, TransitionToPartLevelAndWait(partId, partType));
            }

            // 零件加载/切换完成后立刻开始停留计时
            _visualStage = ThreatVisualStage.PartHold;
            float partHold = Mathf.Max(0.1f, _partLevelHoldSeconds);
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 零件级停留 ({i + 1}/{partSteps.Count}) | type={partType} | part={partId} | " +
                $"eventIds={FormatPartEventIdsForLog(_activePartEventIds)} | {partHold:F1}s");
            yield return WaitHoldSeconds(
                partHold,
                $"零件级停留 ({i + 1}/{partSteps.Count}) type={partType} part={partId}");
            _visualStage = ThreatVisualStage.None;
        }

        _activePartEventIds = EmptyPartEventIds;

        if (hasNextVin)
        {
            // 还有下一 Vin：回车辆过渡前先切身份，宿主 Part/攻击→车辆 回调即为新 Vin
            yield return SwitchToNextVinThenReturnToVehicle(encryptVin, nextEncryptVin);
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 本 Vin 全部零件展示完毕，已切下一 Vin 并回车辆级 | " +
                $"finishedEncryptVin={encryptVin} | nextActiveEncryptVin={_activeEncryptVin}");
        }
        else
        {
            // 本省最后一 Vin：跳过零件→车辆动画，由后续 EnsureCountryLevel 瞬时跳回国家
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 省内最后一辆 Vin 展示完毕，跳过零件→车辆，随后瞬时回国家级 | " +
                $"vin={encryptVin} | control={GameManager.Instance?.CurrentState}");
        }
    }

    private IEnumerator EnsureAtVehicleLevel(string provinceCode, string encryptVin)
    {
        CacheActiveVehicleIdentity(encryptVin, "EnsureAtVehicleLevel");

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        if (gm.CurrentState == GameManager.ControlState.PartLevel ||
            gm.CurrentState == GameManager.ControlState.AttackPathLevel)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] EnsureAtVehicleLevel：从 {gm.CurrentState} 回车辆 | " +
                $"ActiveEncryptVin={_activeEncryptVin} | ActiveVin={_activeVin}");
            yield return ReturnToVehicleLevelFromDrill();
        }

        if (gm.CurrentState == GameManager.ControlState.VehicleLevel)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] EnsureAtVehicleLevel：已在车辆级，跳过层级过渡 | " +
                $"请求encryptVin={encryptVin} | ActiveEncryptVin={_activeEncryptVin} | ActiveVin={_activeVin} | " +
                $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin ?? "(空)"}");
            yield break;
        }

        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] EnsureAtVehicleLevel：层级跳转到车辆级 | from={gm.CurrentState} | " +
            $"ActiveEncryptVin={_activeEncryptVin} | ActiveVin={_activeVin}");

        ControlStateHierarchyTransitionController hierarchy =
            ControlStateHierarchyTransitionController.Instance;
        if (hierarchy == null)
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] 无法进入车辆级：缺少层级过渡控制器。");
            yield break;
        }

        yield return WaitForHierarchyTransition(
            hierarchy,
            GameManager.ControlState.VehicleLevel,
            useInstant: false,
            provinceCode: provinceCode,
            confirmTimeoutSeconds: 15f);

        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] EnsureAtVehicleLevel：层级跳转结束 | control={gm.CurrentState} | " +
            $"ActiveEncryptVin={_activeEncryptVin} | ActiveVin={_activeVin}");
    }

    private IEnumerator ReturnToVehicleLevelFromDrill()
    {
        _activePartEventIds = EmptyPartEventIds;

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        if (gm.CurrentState == GameManager.ControlState.VehicleLevel)
        {
            yield break;
        }

        yield return WaitForTransitionControllersIdle();

        if (gm.CurrentState == GameManager.ControlState.PartLevel)
        {
            yield return WaitForBoolStringTransition(
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted += h,
                h => EventManager.Instance.OnVehicleToPartTransitionReverseCompleted -= h,
                () => MapApi.Instance.TransitionPartToVehicle(),
                "零件 → 车辆");
            yield break;
        }

        if (gm.CurrentState == GameManager.ControlState.AttackPathLevel)
        {
            yield return WaitForVoidTransition(
                h => EventManager.Instance.OnAttackPathToVehicleTransitionCompleted += h,
                h => EventManager.Instance.OnAttackPathToVehicleTransitionCompleted -= h,
                () => MapApi.Instance.TransitionAttackPathToVehicle(),
                "攻击路径 → 车辆");
        }
    }

    private IEnumerator TransitionToAttackPathLevelAndWait()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CurrentState == GameManager.ControlState.AttackPathLevel)
        {
            ApplyAttackPathStageVisuals();
            _lastTransitionSucceeded = true;
            yield break;
        }

        if (gm != null && gm.CurrentState == GameManager.ControlState.PartLevel)
        {
            yield return ReturnToVehicleLevelFromDrill();
        }

        if (gm != null && gm.CurrentState != GameManager.ControlState.VehicleLevel)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 车辆→攻击链路取消：当前非车辆级 ({gm.CurrentState})。");
            _lastTransitionSucceeded = false;
            yield break;
        }

        yield return WaitForTransitionControllersIdle();

        bool mapStarted = MapApi.Instance != null && MapApi.Instance.TransitionVehicleToAttackPath();
        if (mapStarted)
        {
            yield return WaitForVoidTransition(
                h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted += h,
                h => EventManager.Instance.OnVehicleToAttackPathTransitionCompleted -= h,
                () => true,
                "车辆 → 攻击路径（等待完成）");
        }
        else
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] MapApi 车辆→攻击路径启动失败，尝试层级控制器。");
            ControlStateHierarchyTransitionController hierarchy =
                ControlStateHierarchyTransitionController.Instance;
            if (hierarchy != null)
            {
                yield return WaitForHierarchyTransition(
                    hierarchy,
                    GameManager.ControlState.AttackPathLevel,
                    useInstant: false,
                    provinceCode: null,
                    confirmTimeoutSeconds: 20f);
            }
        }

        _lastTransitionSucceeded = IsAtAttackPathLevel();
        if (!_lastTransitionSucceeded)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 车辆→攻击链路未完成 | control={gm?.CurrentState}");
        }
    }

    private static bool IsAtAttackPathLevel()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentState == GameManager.ControlState.AttackPathLevel;
    }

    private IEnumerator TransitionAttackPathToPartAndWait(string partId, int partType = 0)
    {
        yield return WaitForTransitionControllersIdle();
        yield return WaitForBoolStringStringTransition(
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted += h,
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionAttackPathToPart(partId, partType),
            $"攻击路径 → 零件 type={partType} {partId}");
    }

    /// <summary>
    /// 进入/切换零件：已在零件级监听零件→零件完成；否则监听车辆→零件完成。
    /// 启动后若未真正过渡（同零件已激活），立刻视为加载完成并开始停留。
    /// </summary>
    private IEnumerator TransitionToPartLevelAndWait(string partId, int partType = 0)
    {
        yield return WaitForTransitionControllersIdle();

        EventManager em = EventManager.Instance;
        if (em == null || MapApi.Instance == null)
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] 无 EventManager/MapApi，跳过零件过渡。");
            yield break;
        }

        bool partToPart = GameManager.Instance != null &&
                          GameManager.Instance.CurrentState == GameManager.ControlState.PartLevel;
        string stepName = partToPart
            ? $"零件 → 零件 type={partType} {partId}"
            : $"车辆 → 零件 type={partType} {partId}";

        _transitionStepDone = false;
        if (partToPart)
        {
            em.OnPartToPartTransitionCompleted += OnTransitionStepDoneWithPart;
        }
        else
        {
            em.OnVehicleToPartTransitionCompleted += OnTransitionStepDoneWithName;
        }

        bool started = MapApi.Instance.TransitionVehicleToPart(partId, partType);
        if (!started)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
            _lastTransitionSucceeded = false;
            if (partToPart)
            {
                em.OnPartToPartTransitionCompleted -= OnTransitionStepDoneWithPart;
            }
            else
            {
                em.OnVehicleToPartTransitionCompleted -= OnTransitionStepDoneWithName;
            }

            yield break;
        }

        VehicleToPartTransitionController partController = VehicleToPartTransitionController.Instance;
        // 同零件已激活：PlayTransition 直接返回 true 且不播动画，立刻完成。
        if (partController != null && !partController.IsTransitioning)
        {
            _transitionStepDone = true;
        }

        float waitStart = Time.unscaledTime;
        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            // 动画已结束但事件漏发时，以 IsTransitioning=false 兜底，避免空等 60s。
            if (partController != null && !partController.IsTransitioning)
            {
                yield return null;
                if (!_transitionStepDone && partController != null && !partController.IsTransitioning)
                {
                    _transitionStepDone = true;
                    break;
                }
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        _lastTransitionSucceeded = _transitionStepDone;
        LogTransitionWaitResult(stepName, waitStart, _transitionStepDone);

        if (partToPart)
        {
            em.OnPartToPartTransitionCompleted -= OnTransitionStepDoneWithPart;
        }
        else
        {
            em.OnVehicleToPartTransitionCompleted -= OnTransitionStepDoneWithName;
        }
    }

    private IEnumerator RequestVehicleDataAndWait(string encryptVin)
    {
        CarVehicleDataController controller = CarVehicleDataController.Instance;
        if (controller == null)
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] 未找到 CarVehicleDataController，跳过车辆信息请求。");
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(encryptVin))
        {
            CacheActiveVehicleIdentity(encryptVin, "RequestVehicleDataAndWait");
        }

        ResolveVehicleQueryWindowFromThreatEvents(
            encryptVin,
            out string startTime,
            out string endTime);

        string targetVin = string.IsNullOrWhiteSpace(encryptVin) ? string.Empty : encryptVin.Trim();
        string cachedVin = controller.Store != null ? controller.Store.LastEncryptVin : null;
        bool vinMismatch = !string.IsNullOrWhiteSpace(cachedVin) &&
                           !string.IsNullOrWhiteSpace(targetVin) &&
                           !string.Equals(cachedVin.Trim(), targetVin, StringComparison.Ordinal);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 请求车辆态势 | targetEncryptVin={targetVin} | " +
            $"activeEncryptVin={_activeEncryptVin} | activeVin={_activeVin} | " +
            $"storeLastEncryptVin={cachedVin} | controllerActiveEncryptVin={controller.ActiveEncryptVin} | " +
            $"store与目标不一致={vinMismatch} | isRequesting={controller.IsRequesting} | " +
            $"startTime={startTime} | endTime={endTime}");
        if (vinMismatch)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 换车请求：Store 仍是旧 Vin，将按新 Vin 发起后端查询 | " +
                $"oldStore={cachedVin} | newTarget={targetVin}");
        }

        // 关键点：不要阻塞车辆停留计时。
        // 仅当缓存已是当前 Vin 时才立刻刷 UI，避免同省下一辆仍展示上一辆。
        bool cacheMatchesTarget = !string.IsNullOrWhiteSpace(cachedVin) &&
                                  !string.IsNullOrWhiteSpace(targetVin) &&
                                  string.Equals(cachedVin.Trim(), targetVin, StringComparison.Ordinal);
        if (cacheMatchesTarget)
        {
            controller.TryShowVehicleUiFromCache();
        }

        bool sameVinInFlight = controller.IsRequesting &&
                               !string.IsNullOrWhiteSpace(controller.ActiveEncryptVin) &&
                               string.Equals(controller.ActiveEncryptVin.Trim(), targetVin, StringComparison.Ordinal);
        if (sameVinInFlight)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 同 Vin 请求进行中，不重复发起 | vin={targetVin}");
            yield return null;
            yield break;
        }

        if (controller.IsRequesting)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner] 作废进行中的旧车请求并按新 Vin 重发 | " +
                $"oldVin={controller.ActiveEncryptVin} | newVin={targetVin}");
        }

        controller.RequestReplacingPending(
            encryptVin,
            startTime,
            endTime,
            onCompleted: (success, error) =>
            {
                if (!success)
                {
                    // 作废旧请求时也会回调失败，属预期，不按加载失败告警。
                    if (!string.IsNullOrEmpty(error) &&
                        error.IndexOf("作废", StringComparison.Ordinal) >= 0)
                    {
                        LogManager.LogFeature(
                            $"[ThreatAlertFlowRunner] 旧车请求已作废（换 Vin） | targetEncryptVin={targetVin} | error={error}");
                        return;
                    }

                    LogManager.LogFeatureWarning(
                        $"[ThreatAlertFlowRunner] 车辆信息加载失败，流程继续（继续使用已有缓存） | vin={targetVin} | error={error}");
                    return;
                }

                LogManager.LogFeature(
                    $"[ThreatAlertFlowRunner] 车辆态势请求成功 | targetEncryptVin={targetVin} | " +
                    $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin ?? "(空)"} | " +
                    $"ActiveEncryptVin={_activeEncryptVin}");
            });

        // 给一帧，避免后续阶段与请求回调在同一帧内竞争。
        yield return null;
    }

    /// <summary>
    /// 按当前 VIN 威胁条目 processTime 组装车辆态势起止时间：
    /// 单条 → start=当天 0 点、end=processTime；多条 → start=min 当天 0 点、end=max。
    /// </summary>
    private void ResolveVehicleQueryWindowFromThreatEvents(
        string encryptVin,
        out string startTime,
        out string endTime)
    {
        startTime = ThreatQueryDefaults.StartTime;
        endTime = ThreatQueryDefaults.EndTime;

        if (string.IsNullOrWhiteSpace(encryptVin))
        {
            LogManager.LogFeatureWarning("[ThreatAlertFlowRunner] 车辆态势时间窗：vin 为空，使用威胁默认起止时间。");
            return;
        }

        IReadOnlyList<HighRiskSecurityEventItem> events = CollectThreatEventsForVin(encryptVin);
        if (events == null || events.Count == 0)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 车辆态势时间窗：未找到 vin={encryptVin} 的威胁事件，使用默认起止时间。");
            return;
        }

        DateTime? minTime = null;
        DateTime? maxTime = null;
        int parsedCount = 0;
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || !TryParseThreatProcessTime(item.processTime, out DateTime processTime))
            {
                continue;
            }

            parsedCount++;
            if (!minTime.HasValue || processTime < minTime.Value)
            {
                minTime = processTime;
            }

            if (!maxTime.HasValue || processTime > maxTime.Value)
            {
                maxTime = processTime;
            }
        }

        if (!minTime.HasValue || !maxTime.HasValue)
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] 车辆态势时间窗：vin={encryptVin} 的 processTime 均无法解析，使用默认起止时间。");
            return;
        }

        // 单条与多条统一：start=min 当天 0 点，end=max(processTime)
        startTime = minTime.Value.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        endTime = maxTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 车辆态势时间窗已按 processTime 生成 | vin={encryptVin} | " +
            $"events={events.Count} | parsed={parsedCount} | {startTime} → {endTime}");
    }

    private IReadOnlyList<HighRiskSecurityEventItem> CollectThreatEventsForVin(string encryptVin)
    {
        List<HighRiskSecurityEventItem> matched = new List<HighRiskSecurityEventItem>();
        string vinKey = encryptVin.Trim();
        string provinceCode = _activeProvinceCode;

        IReadOnlyList<HighRiskSecurityEventItem> source = null;
        bool fromAllEvents = false;
        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            source = HighRiskSecurityEventDataStore.Instance?.GetEventsByProvince(provinceCode);
        }

        if (source == null || source.Count == 0)
        {
            source = HighRiskSecurityEventDataStore.Instance?.GetAllEvents();
            fromAllEvents = true;
        }

        if (source == null || source.Count == 0)
        {
            return matched;
        }

        for (int i = 0; i < source.Count; i++)
        {
            HighRiskSecurityEventItem item = source[i];
            if (item == null)
            {
                continue;
            }

            string itemKey = item.PreferEncryptVin();
            if (string.IsNullOrWhiteSpace(itemKey)
                || !string.Equals(itemKey, vinKey, StringComparison.Ordinal))
            {
                continue;
            }

            // 全量回退时仍尽量限定当前威胁单元（国内 province / 国外 country）
            if (fromAllEvents
                && !string.IsNullOrWhiteSpace(provinceCode)
                && !MatchesThreatMapUnit(item, provinceCode))
            {
                continue;
            }

            matched.Add(item);
        }

        return matched;
    }

    /// <summary>条目地理单元是否与目标 code 一致（province 优先，空则比 country）。</summary>
    private static bool MatchesThreatMapUnit(HighRiskSecurityEventItem item, string unitCode)
    {
        if (item == null || string.IsNullOrWhiteSpace(unitCode))
        {
            return false;
        }

        if (!item.TryResolveMapUnitCode(out string itemUnit) || string.IsNullOrWhiteSpace(itemUnit))
        {
            return false;
        }

        string itemKey = itemUnit.Trim();
        string unitKey = unitCode.Trim();
        if (string.Equals(itemKey, unitKey, StringComparison.Ordinal))
        {
            return true;
        }

        bool hasQuery = PlateMapBoundaryDatabase.TryNormalizeProvinceCode(unitKey, out string normalizedQuery);
        bool hasItem = PlateMapBoundaryDatabase.TryNormalizeProvinceCode(itemKey, out string normalizedItem);
        if (hasQuery && hasItem)
        {
            return string.Equals(normalizedItem, normalizedQuery, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryParseThreatProcessTime(string processTime, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(processTime))
        {
            return false;
        }

        string trimmed = processTime.Trim();
        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
        };

        if (DateTime.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
        {
            return true;
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private readonly struct ThreatPartDrillStep
    {
        public readonly int PartType;
        public readonly string PartId;
        public readonly string[] EventIds;

        public ThreatPartDrillStep(int partType, string partId, string[] eventIds)
        {
            PartType = partType;
            PartId = partId ?? string.Empty;
            EventIds = eventIds != null && eventIds.Length > 0 ? eventIds : EmptyPartEventIds;
        }
    }

    /// <summary>
    /// 按防护状态列表收集零件下钻步骤：每个零件只跳转 1 次，步骤内携带该零件全部 pending eventId。
    /// 无 pending 的零件仍跳转 1 次（eventIds 为空）；无防护数据时回退攻击链路节点。
    /// </summary>
    private List<ThreatPartDrillStep> ResolvePartDrillSteps()
    {
        return BuildPartDrillStepsFromProtectionList();
    }

    private static List<ThreatPartDrillStep> BuildPartDrillStepsFromProtectionList()
    {
        List<ThreatPartDrillStep> result = new List<ThreatPartDrillStep>();
        PartProtectionStatusData data = CarVehicleDataStore.Instance?.PartProtectionStatus?.data;
        AppendPartDrillStepsFromParts(result, data?.unprotectedParts);
        AppendPartDrillStepsFromParts(result, data?.protectedParts);

        if (result.Count > 0)
        {
            return result;
        }

        List<int> fallbackTypes = CarVehicleDataStore.Instance.BuildAttackChainNodePartTypes();
        AttackChainNode[] nodes = CarVehicleDataStore.Instance.AttackChain?.data?.nodes;
        HashSet<int> uniqueTypes = new HashSet<int>();
        for (int i = 0; i < fallbackTypes.Count; i++)
        {
            int partType = fallbackTypes[i];
            if (!uniqueTypes.Add(partType))
            {
                continue;
            }

            string displayName = ResolveDisplayNameForPartType(partType, nodes);
            result.Add(new ThreatPartDrillStep(partType, displayName, EmptyPartEventIds));
        }

        return result;
    }

    private static string ResolveDisplayNameForPartType(int partType, AttackChainNode[] nodes)
    {
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                AttackChainNode node = nodes[i];
                if (node == null
                    || !PartTypeMatchUtil.TryParsePartType(node.partType, out int nodeType)
                    || nodeType != partType
                    || string.IsNullOrWhiteSpace(node.partTypeName))
                {
                    continue;
                }

                return node.partTypeName.Trim();
            }
        }

        VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
        if (transition != null
            && transition.TryResolvePartByType(partType, out Transform root)
            && root != null)
        {
            return root.name;
        }

        return partType.ToString();
    }

    private static void AppendPartDrillStepsFromParts(
        List<ThreatPartDrillStep> steps,
        PartProtectionStatusPart[] parts)
    {
        if (parts == null || parts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartProtectionStatusPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            int partType = 0;
            PartTypeMatchUtil.TryNormalizePartType(part.partType, out partType);
            string partId = !string.IsNullOrWhiteSpace(part.partTypeName)
                ? part.partTypeName.Trim()
                : (PartTypeMatchUtil.IsValidPartType(partType) ? partType.ToString() : string.Empty);
            if (partId.Length == 0 && !PartTypeMatchUtil.IsValidPartType(partType))
            {
                continue;
            }

            steps.Add(new ThreatPartDrillStep(partType, partId, CollectPendingEventIds(part.pendingEvents)));
        }
    }

    private static string[] CollectPendingEventIds(PartProtectionPendingEvent[] pending)
    {
        if (pending == null || pending.Length == 0)
        {
            return EmptyPartEventIds;
        }

        int count = Mathf.Min(pending.Length, MessageListPanel.MaxMessageCount);
        List<string> ids = new List<string>(count);
        for (int j = 0; j < count; j++)
        {
            PartProtectionPendingEvent evt = pending[j];
            if (evt == null || string.IsNullOrWhiteSpace(evt.eventId))
            {
                continue;
            }

            ids.Add(evt.eventId.Trim());
        }

        return ids.Count == 0 ? EmptyPartEventIds : ids.ToArray();
    }

    private static string FormatPartEventIdsForLog(string[] eventIds)
    {
        if (eventIds == null || eventIds.Length == 0)
        {
            return "(空)";
        }

        return $"[{eventIds.Length}] {string.Join(",", eventIds)}";
    }

    private void RefreshProvinceStageVisuals(HighRiskSecurityEventDataStore store)
    {
        if (string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            return;
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

        List<string> vins = CollectVinsMeetingThreshold(events);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 省级数据已刷新 | province={_activeProvinceCode} | " +
            $"events={events?.Count ?? 0} | qualifyingVins={vins.Count}");
    }

    private void RefreshVehicleStageVisuals()
    {
        if (string.IsNullOrWhiteSpace(_activeEncryptVin))
        {
            return;
        }

        LogManager.LogFeature($"[ThreatAlertFlowRunner] 车辆级数据刷新 | vin={_activeEncryptVin}");
        StartCoroutine(RequestVehicleDataAndWait(_activeEncryptVin));
    }

    private void RefreshAttackPathStageVisuals()
    {
        ApplyAttackPathStageVisuals(IsAtAttackPathLevel());
        LogManager.LogFeature("[ThreatAlertFlowRunner] 攻击链路画面已按最新缓存刷新。");
    }

    private static void ApplyAttackPathStageVisuals(bool preferAttackPathLevel = true)
    {
        CarVehicleDataController controller = CarVehicleDataController.Instance;
        if (controller == null)
        {
            return;
        }

        bool shown = preferAttackPathLevel
            ? controller.TryShowAttackPathsFromCache()
            : controller.ApplyAttackPathsFromCacheForTransition();
        if (!shown && preferAttackPathLevel)
        {
            // 接口失败时仍尝试用缓存绘制攻击链路
            controller.ApplyAttackPathsFromCacheForTransition();
        }

        VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
        if (transition != null)
        {
            transition.SetPartNameLabelsVisible(true);
        }
    }

    private IEnumerator WaitForHierarchyTransition(
        ControlStateHierarchyTransitionController hierarchy,
        GameManager.ControlState targetState,
        bool useInstant,
        string provinceCode,
        float confirmTimeoutSeconds)
    {
        while (hierarchy.IsBootstrapping)
        {
            yield return null;
        }

        GameManager gm = GameManager.Instance;
        GameManager.ControlState from = gm != null
            ? gm.CurrentState
            : GameManager.ControlState.CountryLevel;

        bool started = hierarchy.TransitionToState(
            useInstantTransition: useInstant,
            targetState: targetState,
            provinceCode: provinceCode);
        if (!started)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] 跳转 {from} → {targetState} 启动失败。");
            yield break;
        }

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

        while (gm != null &&
               gm.CurrentState != targetState &&
               confirmTimeoutSeconds > 0f)
        {
            confirmTimeoutSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForTransitionControllersIdle()
    {
        float timeout = 30f;
        while (ControlStateHierarchyTransitionController.IsAnyTransitionAnimationBusy() && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        VehicleToPartTransitionController partController = VehicleToPartTransitionController.Instance;
        timeout = 30f;
        while (partController != null && partController.IsTransitioning && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForVoidTransition(
        Action<Action> subscribe,
        Action<Action> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDone);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
            _lastTransitionSucceeded = false;
            unsubscribe(OnTransitionStepDone);
            yield break;
        }

        float waitStart = Time.unscaledTime;
        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        _lastTransitionSucceeded = _transitionStepDone;
        LogTransitionWaitResult(stepName, waitStart, _transitionStepDone);

        unsubscribe(OnTransitionStepDone);
    }

    private IEnumerator WaitForBoolStringTransition(
        Action<Action<string>> subscribe,
        Action<Action<string>> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDoneWithName);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
            unsubscribe(OnTransitionStepDoneWithName);
            yield break;
        }

        float waitStart = Time.unscaledTime;
        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        LogTransitionWaitResult(stepName, waitStart, _transitionStepDone);
        unsubscribe(OnTransitionStepDoneWithName);
    }

    private IEnumerator WaitForBoolStringStringTransition(
        Action<Action<string, string>> subscribe,
        Action<Action<string, string>> unsubscribe,
        Func<bool> tryStart,
        string stepName)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDoneWithPart);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
            unsubscribe(OnTransitionStepDoneWithPart);
            yield break;
        }

        float waitStart = Time.unscaledTime;
        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        LogTransitionWaitResult(stepName, waitStart, _transitionStepDone);
        unsubscribe(OnTransitionStepDoneWithPart);
    }

    private void OnTransitionStepDone()
    {
        _transitionStepDone = true;
    }

    private void OnTransitionStepDoneWithName(string _)
    {
        _transitionStepDone = true;
    }

    private void OnTransitionStepDoneWithPart(string _, string __)
    {
        _transitionStepDone = true;
    }

    private static string ResolveProvinceDisplayName(string provinceCode, string plateModuleName)
    {
        string name = WorldMapPlateResolver.ResolveUnitDisplayName(provinceCode);
        if (!string.IsNullOrWhiteSpace(name) &&
            !string.Equals(name, provinceCode, System.StringComparison.Ordinal))
        {
            return name;
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
        PlateMapHighlightController highlight = PlateMapHighlightController.ResolveForWorldMap();
        highlight?.ClearHighlight();

        if (qualifiedProvinceCodes == null || qualifiedProvinceCodes.Count == 0)
        {
            return;
        }

        List<string> plateNames = new List<string>(qualifiedProvinceCodes.Count);
        for (int i = 0; i < qualifiedProvinceCodes.Count; i++)
        {
            string code = qualifiedProvinceCodes[i];

            // 中心点仅用于 POI；失败不阻断高亮（国外 SOC 可能无国内高德表）
            if (ThreatProvinceCenterLookup.TryGetCenter(code, out double lon, out double lat))
            {
                POI_Manager.Instance?.SpawnPoi(code, POIType.provinece_Rad, lon, lat);
            }

            if (PlateMapAPI.Instance != null &&
                PlateMapAPI.Instance.TryResolvePlateMapName(code, out string plateName) &&
                !string.IsNullOrWhiteSpace(plateName) &&
                !plateNames.Contains(plateName))
            {
                plateNames.Add(plateName);
            }
        }

        highlight?.HighlightModulesByName(plateNames);
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

    /// <summary>收集省内 Vin 出现次数 &gt; 阈值的全部车辆（按次数降序、键升序；键优先 encryptVin）。</summary>
    private static List<string> CollectVinsMeetingThreshold(IReadOnlyList<HighRiskSecurityEventItem> events)
    {
        List<string> result = new List<string>();
        if (events == null || events.Count == 0)
        {
            return result;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null)
            {
                continue;
            }

            string key = item.PreferEncryptVin();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        List<KeyValuePair<string, int>> qualifying = new List<KeyValuePair<string, int>>();
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > ThreatAlertSettings.SameVinCountToEnterVehicle)
            {
                qualifying.Add(pair);
            }
        }

        qualifying.Sort(CompareVinCountDescending);
        for (int i = 0; i < qualifying.Count; i++)
        {
            result.Add(qualifying[i].Key);
        }

        return result;
    }

    /// <summary>缓存当前下钻车辆：encryptVin 为请求键；vin 从事件如实取明文。</summary>
    private void CacheActiveVehicleIdentity(string encryptVinKey, string reason = null)
    {
        if (string.IsNullOrWhiteSpace(encryptVinKey))
        {
            LogManager.LogFeatureWarning(
                $"[ThreatAlertFlowRunner] CacheActiveVehicleIdentity 忽略空 encryptVin | reason={reason ?? "(无)"}");
            return;
        }

        string key = encryptVinKey.Trim();
        string oldEncrypt = _activeEncryptVin;
        string oldPlain = _activeVin;
        bool changed = !string.Equals(oldEncrypt, key, StringComparison.Ordinal);
        _activeEncryptVin = key;
        _activeVin = ResolvePlainVinForEncryptKey(key);
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner] 切换车辆身份 | reason={reason ?? "(无)"} | changed={changed} | " +
            $"encryptVin: {oldEncrypt ?? "(空)"} → {_activeEncryptVin} | " +
            $"vin: {oldPlain ?? "(空)"} → {_activeVin ?? "(空)"} | " +
            $"storeLastEncryptVin={CarVehicleDataStore.Instance?.LastEncryptVin ?? "(空)"}");
    }

    private string ResolvePlainVinForEncryptKey(string encryptVinKey)
    {
        if (string.IsNullOrWhiteSpace(encryptVinKey))
        {
            return string.Empty;
        }

        string key = encryptVinKey.Trim();
        IReadOnlyList<HighRiskSecurityEventItem> source = null;
        if (!string.IsNullOrWhiteSpace(_activeProvinceCode))
        {
            source = HighRiskSecurityEventDataStore.Instance?.GetEventsByProvince(_activeProvinceCode);
        }

        if (source == null || source.Count == 0)
        {
            source = HighRiskSecurityEventDataStore.Instance?.GetAllEvents();
        }

        if (source == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < source.Count; i++)
        {
            HighRiskSecurityEventItem item = source[i];
            if (item == null)
            {
                continue;
            }

            if (!string.Equals(item.PreferEncryptVin(), key, StringComparison.Ordinal))
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(item.vin) ? string.Empty : item.vin.Trim();
        }

        return string.Empty;
    }

    private static int CompareVinCountDescending(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
    {
        int byCount = b.Value.CompareTo(a.Value);
        if (byCount != 0)
        {
            return byCount;
        }

        return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
    }

    private IEnumerator RunTimedStep(string stepName, IEnumerator step)
    {
        float start = Time.unscaledTime;
        if (_logStageTiming)
        {
            LogManager.LogFeature($"[ThreatAlertFlowRunner][计时] 开始 | {stepName}");
        }

        yield return step;

        if (_logStageTiming)
        {
            LogManager.LogFeature(
                $"[ThreatAlertFlowRunner][计时] 结束 | {stepName} | 实际={Time.unscaledTime - start:F2}s");
        }
    }

    private void LogTransitionWaitResult(string stepName, float waitStart, bool completed)
    {
        if (!completed)
        {
            LogManager.LogFeatureWarning($"[ThreatAlertFlowRunner] {stepName} 等待完成超时。");
        }

        if (!_logStageTiming)
        {
            return;
        }

        float elapsed = Time.unscaledTime - waitStart;
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner][计时] 过渡等待 | {stepName} | 实际={elapsed:F2}s | " +
            $"完成={completed} | control={GameManager.Instance?.CurrentState}");
    }

    private IEnumerator WaitHoldSeconds(float seconds, string stepName = null)
    {
        float start = Time.unscaledTime;
        if (_logStageTiming && !string.IsNullOrEmpty(stepName))
        {
            LogManager.LogFeature($"[ThreatAlertFlowRunner][计时] 停留开始 | {stepName} | 配置={seconds:F2}s");
        }

        _holdCountdownTotal = seconds;
        _holdCountdownRemaining = seconds;

        try
        {
            // 过渡/请求期间已点的跳过，进入停留后立即生效（不在此处清零）。
            if (_skipCurrentHold)
            {
                _skipCurrentHold = false;
                LogHoldResult(stepName, start, seconds, skipped: true);
                yield break;
            }

            float remain = Mathf.Max(0f, seconds);
            while (remain > 0f && !_skipCurrentHold)
            {
                _holdCountdownRemaining = Mathf.Max(0f, remain);
                // 使用非缩放时间，避免 GameManager 暂停/慢动作导致停留计时失真。
                remain -= Time.unscaledDeltaTime;
                yield return null;
            }

            bool skipped = _skipCurrentHold;
            _skipCurrentHold = false;
            LogHoldResult(stepName, start, seconds, skipped);
        }
        finally
        {
            ClearHoldCountdown();
        }
    }

    private void ClearHoldCountdown()
    {
        _holdCountdownTotal = 0f;
        _holdCountdownRemaining = 0f;
    }

    private void LogHoldResult(string stepName, float start, float configuredSeconds, bool skipped)
    {
        if (!_logStageTiming || string.IsNullOrEmpty(stepName))
        {
            return;
        }

        float elapsed = Time.unscaledTime - start;
        float delta = elapsed - configuredSeconds;
        string skipTag = skipped ? " | 已跳过" : string.Empty;
        LogManager.LogFeature(
            $"[ThreatAlertFlowRunner][计时] 停留结束 | {stepName} | 实际={elapsed:F2}s | " +
            $"配置={configuredSeconds:F2}s | 偏差={delta:+#.##;-#.##;0.00}s{skipTag}");
    }

    private void ApplyNoThreatIdleState()
    {
        // 自然结束：对本轮已播完的省补传消费标记（与每省即时上传并存，作兜底）。
        FlushSessionConsumedMarks("ApplyNoThreatIdleState");

        // 自然结束：立刻恢复威胁自动轮询（主动退出走冷却，不在此恢复）。
        ResumeThreatPollingAfterNaturalEndIfNeeded();

        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.ResolveForWorldMap()?.ClearHighlight();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
        ThreatProvinceAlertController.NotifyAllAlertsCompleted();

        // 原逻辑：威胁下钻自然跑完后进入大屏自动轮播（不进冷却）。
        // 现需求：结束后不再开启轮播（避免 轮播_LB_Controller inactive 时 StartCoroutine 报错）。
        // if (MapApi.Instance != null)
        // {
        //     MapApi.Instance.SetBigScreenAutoCarouselEnabled(true, bypassDelayedStart: true);
        // }

        LogManager.LogFeature("[ThreatAlertFlowRunner] 无达标省，GameManager → Default（不开自动轮播）。");
    }

    /// <summary>下钻开始：按开关暂停高危事件自动轮询（保留 wantPolling 意图）。</summary>
    private void PauseThreatPollingForDrillIfEnabled()
    {
        _threatFlowPausedPolling = false;
        if (!_pauseThreatPollingDuringDrill)
        {
            LogManager.LogBackend(
                "[ThreatAlertFlowRunner] 已关闭「下钻中暂停威胁轮询」，本轮不暂停自动获取。");
            return;
        }

        HighRiskSecurityEventApiController.Instance?.OnThreatInterruptCooldownStarted();
        _threatFlowPausedPolling = true;
        LogManager.LogBackend("[ThreatAlertFlowRunner] 威胁下钻开始，已暂停高危事件自动轮询。");
    }

    /// <summary>自然结束：若本轮暂停过轮询且当前不在打断冷却，则立即恢复。</summary>
    private void ResumeThreatPollingAfterNaturalEndIfNeeded()
    {
        if (!_pauseThreatPollingDuringDrill || !_threatFlowPausedPolling)
        {
            _threatFlowPausedPolling = false;
            return;
        }

        _threatFlowPausedPolling = false;
        if (IsInInterruptCooldown)
        {
            LogManager.LogBackend(
                "[ThreatAlertFlowRunner] 自然结束但处于打断冷却，轮询恢复交给冷却结束逻辑。");
            return;
        }

        HighRiskSecurityEventApiController.Instance?.OnThreatInterruptCooldownEnded();
        LogManager.LogBackend("[ThreatAlertFlowRunner] 威胁下钻自然结束，已恢复高危事件自动轮询。");
    }

    private void ClearSessionConsumedProvinces()
    {
        _sessionConsumedProvinces.Clear();
    }

    /// <summary>删省缓存前记录本轮已播省的 eventId，供结束/退出补传。</summary>
    private void RecordProvinceConsumedForSession(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return;
        }

        string code = provinceCode.Trim();
        string[] eventIds = HighRiskEventConsumedMarkApi.CollectEventIdsByProvince(code);
        if (eventIds == null || eventIds.Length == 0)
        {
            LogManager.LogBackend(
                $"[ThreatAlertFlowRunner] 省 {code} 无有效 eventId，不记入本轮补传列表。");
            return;
        }

        for (int i = 0; i < _sessionConsumedProvinces.Count; i++)
        {
            if (string.Equals(_sessionConsumedProvinces[i].ProvinceCode, code, StringComparison.Ordinal))
            {
                _sessionConsumedProvinces[i] = new SessionConsumedProvince
                {
                    ProvinceCode = code,
                    EventIds = eventIds,
                };
                LogManager.LogBackend(
                    $"[ThreatAlertFlowRunner] 已更新本轮补传省 | province={code} | eventIds={eventIds.Length}");
                return;
            }
        }

        _sessionConsumedProvinces.Add(new SessionConsumedProvince
        {
            ProvinceCode = code,
            EventIds = eventIds,
        });
        LogManager.LogBackend(
            $"[ThreatAlertFlowRunner] 已记录本轮补传省 | province={code} | eventIds={eventIds.Length} | " +
            $"sessionCount={_sessionConsumedProvinces.Count}");
    }

    /// <summary>威胁下钻结束/退出时，按本轮已播省逐省补传消费标记。</summary>
    private void FlushSessionConsumedMarks(string reason)
    {
        if (!_flushConsumedMarkOnFlowEnd)
        {
            LogManager.LogBackend(
                $"[ThreatAlertFlowRunner] 已关闭结束补传消费标记，跳过 | reason={reason} | " +
                $"recordedProvinces={_sessionConsumedProvinces.Count}");
            ClearSessionConsumedProvinces();
            return;
        }

        if (_sessionConsumedProvinces.Count == 0)
        {
            LogManager.LogBackend(
                $"[ThreatAlertFlowRunner] 本轮无已播省，跳过结束补传消费标记 | reason={reason}");
            return;
        }

        int provinceCount = _sessionConsumedProvinces.Count;
        LogManager.LogBackend(
            $"[ThreatAlertFlowRunner] 威胁下钻结束，开始补传消费标记 | provinces={provinceCount} | reason={reason}");

        for (int i = 0; i < provinceCount; i++)
        {
            SessionConsumedProvince entry = _sessionConsumedProvinces[i];
            if (entry.EventIds == null || entry.EventIds.Length == 0)
            {
                continue;
            }

            LogManager.LogBackend(
                $"[ThreatAlertFlowRunner] 补传消费标记 ({i + 1}/{provinceCount}) | " +
                $"province={entry.ProvinceCode} | eventIds={entry.EventIds.Length}");
            HighRiskEventConsumedMarkApi.Request(entry.EventIds, endTime: null);
        }

        ClearSessionConsumedProvinces();
    }

    private void HandleProvinceFocusCompleted(string _)
    {
        _provinceFocusSignal = true;
    }
}
