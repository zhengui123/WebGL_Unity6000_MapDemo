using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁告警流程协程宿主：
/// 瞬时回国家 → 国家停留 → 省级停留 → 按 Vin≥3 轮流下钻（车辆 → 攻击链路 → 零部件）→ 回国家再评估。
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

    [Header("调试")]
    [Tooltip("Console 输出 [计时] 日志，区分过渡耗时与停留耗时")]
    [SerializeField] private bool _logStageTiming = true;

    private Coroutine _flowRoutine;
    private Coroutine _interruptCooldownRoutine;
    private bool _skipCurrentHold;
    private bool _provinceFocusSignal;
    private bool _transitionStepDone;
    private ThreatVisualStage _visualStage = ThreatVisualStage.None;
    private string _activeProvinceCode;
    private string _activePlateModuleName;
    private string _activeEncryptVin;
    private bool _resumeFromVehicleDrillSubtree;
    private bool _forceStartFromCountry;
    private bool _lastTransitionSucceeded;
    private float _holdCountdownRemaining;
    private float _holdCountdownTotal;
    private float _interruptCooldownRemaining;

    /// <summary>是否正在跑威胁流程。</summary>
    public bool IsRunning => _flowRoutine != null;

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

    /// <summary>Vin≥3 时请求进入车辆大屏（参数为 Vin）。</summary>
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

    /// <summary>启动一轮威胁流程（进行中或冷却中则忽略）。</summary>
    /// <param name="resumeFromVehicleDrillSubtree">已在车辆/攻击链路/零件级时跳过国家与省级停留，直接 Vin 下钻。</param>
    public bool TryStartThreatFlow(bool resumeFromVehicleDrillSubtree = false)
    {
        if (_flowRoutine != null)
        {
            return false;
        }

        if (IsInInterruptCooldown)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 威胁冷却中，拒绝启动流程 | 剩余={_interruptCooldownRemaining:F0}s");
            return false;
        }

        bool interruptedCarousel = TryStopAutoCarouselForThreatDrill(out string carouselMode);
        // 轮播/延时等待被打断时：必须从全国开始，不允许续钻车辆子树。
        _forceStartFromCountry = interruptedCarousel;
        _resumeFromVehicleDrillSubtree = resumeFromVehicleDrillSubtree && !interruptedCarousel;

        if (interruptedCarousel)
        {
            Debug.Log(
                $"[ThreatAlertFlowRunner] 已停止自动轮播（{carouselMode}），威胁下钻从全国开始。");
        }

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
            detail += $" | vin={_activeEncryptVin}";
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

        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);

        // 退出打断不改 ControlState，保持当前级别。
        StartInterruptCooldown();
        Debug.Log(
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
            Debug.LogWarning("[ThreatAlertFlowRunner] 当前不在威胁冷却中，忽略刷新冷却。");
            return false;
        }

        StartInterruptCooldown();
        Debug.Log(
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
                    Debug.Log("[ThreatAlertFlowRunner] 数据刷新后无达标省，跳过国家停留剩余时间。");
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
                Debug.Log("[ThreatAlertFlowRunner] 零件级停留中收到新数据，保持当前零件展示。");
                break;
            }
            default:
                Debug.Log("[ThreatAlertFlowRunner] 过渡动画中收到新数据，待当前步骤完成后使用最新缓存。");
                break;
        }
    }

    /// <summary>停止流程并清理 POI/高亮（调试重置，同时取消冷却）。</summary>
    public void StopAndResetVisuals()
    {
        StopFlowInternal();
        CancelInterruptCooldown();
        ThreatProvinceAlertController.NotifyFlowStopped();
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
        _activePlateModuleName = null;
        _activeEncryptVin = null;
        _resumeFromVehicleDrillSubtree = false;
        _forceStartFromCountry = false;
        ClearHoldCountdown();
    }

    private void StartInterruptCooldown()
    {
        CancelInterruptCooldown();
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
        Debug.Log($"[ThreatAlertFlowRunner] 威胁冷却开始 | {total:F0}s（期间不检测）");

        while (_interruptCooldownRemaining > 0f)
        {
            _interruptCooldownRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        _interruptCooldownRemaining = 0f;
        _interruptCooldownRoutine = null;
        Debug.Log("[ThreatAlertFlowRunner] 威胁冷却结束，恢复威胁数据检测。");
        ThreatProvinceAlertController.EvaluateAfterDataUpdated();
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

                // 轮播打断后强制从全国进入；否则才允许车辆子树续钻。
                bool skipToVinDrill = !_forceStartFromCountry &&
                                     (_resumeFromVehicleDrillSubtree || IsInVehicleDrillControlState());
                _resumeFromVehicleDrillSubtree = false;
                _forceStartFromCountry = false;

                if (!skipToVinDrill)
                {
                    yield return EnsureCountryLevel();
                    yield return PlayCountryStage();
                }
                else
                {
                    Debug.Log(
                        "[ThreatAlertFlowRunner] 已在车辆/攻击链路/零件级，跳过国家阶段，直接进入省级 Vin 下钻。");
                }

                qualified = store.GetProvincesMeetingThreshold(ThreatAlertSettings.EventsPerProvinceThreshold);
                if (qualified == null || qualified.Count == 0)
                {
                    ApplyNoThreatIdleState();
                    yield break;
                }

                string provinceCode = qualified[0];
                yield return PlayProvinceStage(provinceCode, skipToVinDrill);

                yield return EnsureCountryLevelFromProvince();
            }
        }
        finally
        {
            _visualStage = ThreatVisualStage.None;
            _activeProvinceCode = null;
            _activePlateModuleName = null;
            _activeEncryptVin = null;
            _flowRoutine = null;
            ThreatProvinceAlertController.NotifyFlowStopped();
        }
    }

    private IEnumerator EnsureCountryLevel()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentState == GameManager.ControlState.CountryLevel)
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

        yield return WaitForHierarchyTransition(
            hierarchy,
            GameManager.ControlState.CountryLevel,
            useInstant: true,
            provinceCode: null,
            confirmTimeoutSeconds: 5f);
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
        Debug.Log(
            $"[ThreatAlertFlowRunner] 国家阶段：达标省={qualified?.Count ?? 0}，" +
            $"停留={countryHold:F1}s");

        yield return WaitHoldSeconds(countryHold, "国家级停留");
        _visualStage = ThreatVisualStage.None;
    }

    private IEnumerator PlayProvinceStage(string provinceCode, bool skipToVinDrill)
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

        if (!skipToVinDrill)
        {
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
            Debug.Log(
                $"[ThreatAlertFlowRunner] 省级阶段：province={provinceCode}，事件={events?.Count ?? 0}，" +
                $"停留={provinceHold:F1}s");

            yield return WaitHoldSeconds(provinceHold, $"省级停留 province={provinceCode}");
            _visualStage = ThreatVisualStage.None;
        }
        else
        {
            events = store.GetEventsByProvince(provinceCode);
            context.Events = events;
            ApplyProvinceStageVisuals(provinceCode, events);
            Debug.Log(
                $"[ThreatAlertFlowRunner] 跳过省级停留，自当前车辆级继续 Vin 下钻 | province={provinceCode}");
        }

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
            Debug.Log(
                $"[ThreatAlertFlowRunner] 省级达标 Vin 数={qualifyingVins.Count}，开始轮流下钻 | province={provinceCode}");

            string provinceDisplayName = ResolveProvinceDisplayName(provinceCode, plateModuleName);
            for (int i = 0; i < qualifyingVins.Count; i++)
            {
                string encryptVin = qualifyingVins[i];
                bool hasNextVin = i < qualifyingVins.Count - 1;
                Debug.Log(
                    $"[ThreatAlertFlowRunner] Vin 下钻 ({i + 1}/{qualifyingVins.Count}) | vin={encryptVin} | " +
                    $"hasNextVin={hasNextVin}");
                ThreatVehicleEntryRequested?.Invoke(encryptVin);
                yield return PlayVinDrillChain(
                    provinceCode,
                    provinceDisplayName,
                    plateModuleName,
                    encryptVin,
                    hasNextVin);
            }

            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log(
                $"[ThreatAlertFlowRunner] 该省全部 Vin 下钻完成，将回国家级处理下一达标省 | province={provinceCode}");
        }
        else
        {
            store.RemoveProvinceEventsAndExclude(provinceCode);
            Debug.Log($"[ThreatAlertFlowRunner] 无 Vin 达阈值，已删除并排除该省告警：{provinceCode}");
        }

        ThreatProvinceDrillReserved?.Invoke(context);
        ThreatProvinceAlertController.NotifyProvinceAlertCompleted(context);
        _activeProvinceCode = null;
        _activePlateModuleName = null;
        _activeEncryptVin = null;
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
    }

    /// <summary>单 Vin：车辆级 → 攻击链路级 → 轮流零件级；结束后回车辆级（有下一 Vin）或交由省阶段回国家。</summary>
    private IEnumerator PlayVinDrillChain(
        string provinceCode,
        string provinceDisplayName,
        string plateModuleName,
        string encryptVin,
        bool hasNextVin)
    {
        _activeEncryptVin = encryptVin;
        _activeProvinceCode = provinceCode;
        _activePlateModuleName = plateModuleName;

        yield return RunTimedStep("进入车辆级", EnsureAtVehicleLevel(provinceCode));
        yield return RunTimedStep("车辆数据请求(非阻塞)", RequestVehicleDataAndWait(encryptVin));

        _visualStage = ThreatVisualStage.VehicleHold;
        float vehicleHold = Mathf.Max(0.1f, _vehicleLevelHoldSeconds);
        Debug.Log($"[ThreatAlertFlowRunner] 车辆级停留 | vin={encryptVin} | {vehicleHold:F1}s");
        yield return WaitHoldSeconds(vehicleHold, $"车辆级停留 vin={encryptVin}");
        _visualStage = ThreatVisualStage.None;

        Debug.Log($"[ThreatAlertFlowRunner] 车辆级停留结束，进入攻击链路 | vin={encryptVin}");
        yield return RunTimedStep($"车辆→攻击链路 vin={encryptVin}", TransitionToAttackPathLevelAndWait());
        bool atAttackPathLevel = IsAtAttackPathLevel();
        if (!atAttackPathLevel)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 车辆→攻击链路过渡未完成，仍继续后续阶段（使用已有缓存） | vin={encryptVin} | " +
                $"control={GameManager.Instance?.CurrentState}");
        }

        ApplyAttackPathStageVisuals(atAttackPathLevel);

        _visualStage = ThreatVisualStage.AttackPathHold;
        float attackHold = Mathf.Max(0.1f, _attackPathLevelHoldSeconds);
        Debug.Log($"[ThreatAlertFlowRunner] 攻击链路级停留 | vin={encryptVin} | {attackHold:F1}s");
        yield return WaitHoldSeconds(attackHold, $"攻击链路级停留 vin={encryptVin}");
        _visualStage = ThreatVisualStage.None;

        List<string> partIds = ResolvePartIdsForDrill();
        if (partIds.Count == 0)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] 无可用零部件，跳过零件级 | vin={encryptVin}");
            yield return ReturnToVehicleLevelFromDrill();
            yield break;
        }

        for (int i = 0; i < partIds.Count; i++)
        {
            string partId = partIds[i];
            bool fromAttackPath = i == 0 &&
                                  GameManager.Instance != null &&
                                  GameManager.Instance.CurrentState == GameManager.ControlState.AttackPathLevel;

            if (fromAttackPath)
            {
                yield return RunTimedStep(
                    $"攻击路径→零件 {partId}",
                    TransitionAttackPathToPartAndWait(partId));
            }
            else
            {
                bool partToPart = GameManager.Instance != null &&
                                 GameManager.Instance.CurrentState == GameManager.ControlState.PartLevel;
                string stepLabel = partToPart ? $"零件→零件 {partId}" : $"车辆→零件 {partId}";
                yield return RunTimedStep(stepLabel, TransitionToPartLevelAndWait(partId));
            }

            // 零件加载/切换完成后立刻开始停留计时
            _visualStage = ThreatVisualStage.PartHold;
            float partHold = Mathf.Max(0.1f, _partLevelHoldSeconds);
            Debug.Log(
                $"[ThreatAlertFlowRunner] 零件级停留 ({i + 1}/{partIds.Count}) | part={partId} | {partHold:F1}s");
            yield return WaitHoldSeconds(
                partHold,
                $"零件级停留 ({i + 1}/{partIds.Count}) part={partId}");
            _visualStage = ThreatVisualStage.None;
        }

        yield return ReturnToVehicleLevelFromDrill();

        if (hasNextVin)
        {
            Debug.Log(
                $"[ThreatAlertFlowRunner] 本 Vin 全部零件展示完毕，已回车辆级，即将用下一 Vin 重新请求车辆数据 | vin={encryptVin}");
        }
        else
        {
            Debug.Log(
                $"[ThreatAlertFlowRunner] 省内最后一辆 Vin 展示完毕，已回车辆级，随后回国家级 | vin={encryptVin}");
        }
    }

    private IEnumerator EnsureAtVehicleLevel(string provinceCode)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            yield break;
        }

        if (gm.CurrentState == GameManager.ControlState.PartLevel ||
            gm.CurrentState == GameManager.ControlState.AttackPathLevel)
        {
            yield return ReturnToVehicleLevelFromDrill();
        }

        if (gm.CurrentState == GameManager.ControlState.VehicleLevel)
        {
            yield break;
        }

        ControlStateHierarchyTransitionController hierarchy =
            ControlStateHierarchyTransitionController.Instance;
        if (hierarchy == null)
        {
            Debug.LogWarning("[ThreatAlertFlowRunner] 无法进入车辆级：缺少层级过渡控制器。");
            yield break;
        }

        yield return WaitForHierarchyTransition(
            hierarchy,
            GameManager.ControlState.VehicleLevel,
            useInstant: false,
            provinceCode: provinceCode,
            confirmTimeoutSeconds: 15f);
    }

    private IEnumerator ReturnToVehicleLevelFromDrill()
    {
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
            Debug.LogWarning(
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
            Debug.LogWarning("[ThreatAlertFlowRunner] MapApi 车辆→攻击路径启动失败，尝试层级控制器。");
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
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 车辆→攻击链路未完成 | control={gm?.CurrentState}");
        }
    }

    private static bool IsAtAttackPathLevel()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentState == GameManager.ControlState.AttackPathLevel;
    }

    private IEnumerator TransitionAttackPathToPartAndWait(string partId)
    {
        yield return WaitForTransitionControllersIdle();
        yield return WaitForBoolStringStringTransition(
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted += h,
            h => EventManager.Instance.OnAttackPathToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionAttackPathToPart(partId),
            $"攻击路径 → 零件 {partId}");
    }

    /// <summary>
    /// 进入/切换零件：已在零件级监听零件→零件完成；否则监听车辆→零件完成。
    /// 启动后若未真正过渡（同零件已激活），立刻视为加载完成并开始停留。
    /// </summary>
    private IEnumerator TransitionToPartLevelAndWait(string partId)
    {
        yield return WaitForTransitionControllersIdle();

        EventManager em = EventManager.Instance;
        if (em == null || MapApi.Instance == null)
        {
            Debug.LogWarning("[ThreatAlertFlowRunner] 无 EventManager/MapApi，跳过零件过渡。");
            yield break;
        }

        bool partToPart = GameManager.Instance != null &&
                          GameManager.Instance.CurrentState == GameManager.ControlState.PartLevel;
        string stepName = partToPart ? $"零件 → 零件 {partId}" : $"车辆 → 零件 {partId}";

        _transitionStepDone = false;
        if (partToPart)
        {
            em.OnPartToPartTransitionCompleted += OnTransitionStepDoneWithPart;
        }
        else
        {
            em.OnVehicleToPartTransitionCompleted += OnTransitionStepDoneWithName;
        }

        bool started = MapApi.Instance.TransitionVehicleToPart(partId);
        if (!started)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
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
            Debug.LogWarning("[ThreatAlertFlowRunner] 未找到 CarVehicleDataController，跳过车辆信息请求。");
            yield break;
        }

        Debug.Log($"[ThreatAlertFlowRunner] 请求车辆态势 | encryptVin={encryptVin}");
        // 关键点：不要阻塞车辆停留计时。
        // 先用已有缓存立即刷新车辆 UI；接口成功/失败都让流程按停留秒数继续跳转。
        controller.TryShowVehicleUiFromCache();

        if (controller.IsRequesting)
        {
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] 检测到已有车辆请求进行中，跳过本次请求启动（仍继续用当前缓存） | vin={encryptVin}");
            yield break;
        }

        controller.Request(
            encryptVin,
            startTime: null,
            endTime: null,
            onCompleted: (success, error) =>
            {
                if (!success)
                {
                    Debug.LogWarning(
                        $"[ThreatAlertFlowRunner] 车辆信息加载失败，流程继续（继续使用已有缓存） | vin={encryptVin} | error={error}");
                }
            });

        // 给一帧，避免后续阶段与请求回调在同一帧内竞争。
        yield return null;
    }

    private static List<string> ResolvePartIdsForDrill()
    {
        List<string> partIds = CarVehicleDataStore.Instance.BuildAttackChainNodePartNames();
        if (partIds.Count > 0)
        {
            return partIds;
        }

        List<CarVehiclePartSlide> slides = CarVehicleDataStore.Instance.BuildPartSlides();
        List<string> fallback = new List<string>(slides.Count);
        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < slides.Count; i++)
        {
            string name = slides[i].PartTypeName;
            if (string.IsNullOrWhiteSpace(name) || !unique.Add(name.Trim()))
            {
                continue;
            }

            fallback.Add(name.Trim());
        }

        return fallback;
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
        Debug.Log(
            $"[ThreatAlertFlowRunner] 省级数据已刷新 | province={_activeProvinceCode} | " +
            $"events={events?.Count ?? 0} | qualifyingVins={vins.Count}");
    }

    private void RefreshVehicleStageVisuals()
    {
        if (string.IsNullOrWhiteSpace(_activeEncryptVin))
        {
            return;
        }

        Debug.Log($"[ThreatAlertFlowRunner] 车辆级数据刷新 | vin={_activeEncryptVin}");
        StartCoroutine(RequestVehicleDataAndWait(_activeEncryptVin));
    }

    private void RefreshAttackPathStageVisuals()
    {
        ApplyAttackPathStageVisuals(IsAtAttackPathLevel());
        Debug.Log("[ThreatAlertFlowRunner] 攻击链路画面已按最新缓存刷新。");
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
            Debug.LogWarning($"[ThreatAlertFlowRunner] 跳转 {from} → {targetState} 启动失败。");
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
            Debug.LogWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDone);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
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
            Debug.LogWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDoneWithName);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
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
            Debug.LogWarning($"[ThreatAlertFlowRunner] 无 EventManager，跳过 {stepName}。");
            yield break;
        }

        _transitionStepDone = false;
        subscribe(OnTransitionStepDoneWithPart);
        yield return WaitForTransitionControllersIdle();

        if (!tryStart())
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 启动失败。");
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

    /// <summary>收集省内 Vin 出现次数 ≥ 阈值的全部车辆（按次数降序、Vin 升序）。</summary>
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
            if (item == null || string.IsNullOrWhiteSpace(item.vin))
            {
                continue;
            }

            string key = item.vin.Trim();
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        List<KeyValuePair<string, int>> qualifying = new List<KeyValuePair<string, int>>();
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value >= ThreatAlertSettings.SameVinCountToEnterVehicle)
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
            Debug.Log($"[ThreatAlertFlowRunner][计时] 开始 | {stepName}");
        }

        yield return step;

        if (_logStageTiming)
        {
            Debug.Log(
                $"[ThreatAlertFlowRunner][计时] 结束 | {stepName} | 实际={Time.unscaledTime - start:F2}s");
        }
    }

    private void LogTransitionWaitResult(string stepName, float waitStart, bool completed)
    {
        if (!completed)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 等待完成超时。");
        }

        if (!_logStageTiming)
        {
            return;
        }

        float elapsed = Time.unscaledTime - waitStart;
        Debug.Log(
            $"[ThreatAlertFlowRunner][计时] 过渡等待 | {stepName} | 实际={elapsed:F2}s | " +
            $"完成={completed} | control={GameManager.Instance?.CurrentState}");
    }

    private IEnumerator WaitHoldSeconds(float seconds, string stepName = null)
    {
        float start = Time.unscaledTime;
        if (_logStageTiming && !string.IsNullOrEmpty(stepName))
        {
            Debug.Log($"[ThreatAlertFlowRunner][计时] 停留开始 | {stepName} | 配置={seconds:F2}s");
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
        Debug.Log(
            $"[ThreatAlertFlowRunner][计时] 停留结束 | {stepName} | 实际={elapsed:F2}s | " +
            $"配置={configuredSeconds:F2}s | 偏差={delta:+#.##;-#.##;0.00}s{skipTag}");
    }

    private void ApplyNoThreatIdleState()
    {
        POI_Manager.Instance?.RemoveAllPoi();
        PlateMapHighlightController.Instance?.ClearHighlight();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
        ThreatProvinceAlertController.NotifyAllAlertsCompleted();

        // 自然跑完：进入自动轮播循环（不进冷却）。
        if (MapApi.Instance != null)
        {
            MapApi.Instance.SetBigScreenAutoCarouselEnabled(true, bypassDelayedStart: true);
        }

        Debug.Log("[ThreatAlertFlowRunner] 无达标省，GameManager → Default，已开启自动轮播。");
    }

    private void HandleProvinceFocusCompleted(string _)
    {
        _provinceFocusSignal = true;
    }
}
