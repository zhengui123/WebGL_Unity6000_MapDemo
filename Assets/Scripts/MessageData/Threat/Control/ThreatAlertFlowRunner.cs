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

    [Header("下钻停留（秒，可分别配置）")]
    [Tooltip("车辆级停留")]
    [SerializeField] private float _vehicleLevelHoldSeconds = ThreatAlertSettings.VehicleLevelHoldSeconds;
    [Tooltip("攻击链路级停留")]
    [SerializeField] private float _attackPathLevelHoldSeconds = ThreatAlertSettings.AttackPathLevelHoldSeconds;
    [Tooltip("每个零部件级停留")]
    [SerializeField] private float _partLevelHoldSeconds = ThreatAlertSettings.PartLevelHoldSeconds;

    private Coroutine _flowRoutine;
    private bool _skipCurrentHold;
    private bool _provinceFocusSignal;
    private bool _transitionStepDone;
    private ThreatVisualStage _visualStage = ThreatVisualStage.None;
    private string _activeProvinceCode;
    private string _activePlateModuleName;
    private string _activeEncryptVin;
    private bool _resumeFromVehicleDrillSubtree;
    private bool _lastTransitionSucceeded;

    /// <summary>是否正在跑威胁流程。</summary>
    public bool IsRunning => _flowRoutine != null;

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
    }

    /// <summary>启动一轮威胁流程（进行中则忽略）。</summary>
    /// <param name="resumeFromVehicleDrillSubtree">已在车辆/攻击链路/零件级时跳过国家与省级停留，直接 Vin 下钻。</param>
    public bool TryStartThreatFlow(bool resumeFromVehicleDrillSubtree = false)
    {
        if (_flowRoutine != null)
        {
            return false;
        }

        _resumeFromVehicleDrillSubtree = resumeFromVehicleDrillSubtree;
        _flowRoutine = StartCoroutine(ThreatFlowRoutine());
        return true;
    }

    /// <summary>跳过当前停留计时（Demo「结束当前告警」可用）。</summary>
    public void SkipCurrentHold()
    {
        _skipCurrentHold = true;
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
        _activePlateModuleName = null;
        _activeEncryptVin = null;
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

                bool skipToVinDrill = _resumeFromVehicleDrillSubtree || IsInVehicleDrillControlState();
                _resumeFromVehicleDrillSubtree = false;

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
            provinceName: null,
            provinceModuleName: null,
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

        Debug.Log(
            $"[ThreatAlertFlowRunner] 国家阶段：达标省={qualified?.Count ?? 0}，" +
            $"停留={ThreatAlertSettings.CountryLevelHoldSeconds:F0}s");

        yield return WaitHoldSeconds(ThreatAlertSettings.CountryLevelHoldSeconds);
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
                focusTimeout -= Time.deltaTime;
                yield return null;
            }

            _visualStage = ThreatVisualStage.ProvinceHold;
            events = store.GetEventsByProvince(provinceCode);
            context.Events = events;
            ApplyProvinceStageVisuals(provinceCode, events);

            Debug.Log(
                $"[ThreatAlertFlowRunner] 省级阶段：province={provinceCode}，事件={events?.Count ?? 0}，" +
                $"停留={ThreatAlertSettings.ProvinceLevelHoldSeconds:F0}s");

            yield return WaitHoldSeconds(ThreatAlertSettings.ProvinceLevelHoldSeconds);
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

        yield return EnsureAtVehicleLevel(provinceDisplayName, plateModuleName);
        yield return RequestVehicleDataAndWait(encryptVin);

        _visualStage = ThreatVisualStage.VehicleHold;
        float vehicleHold = Mathf.Max(0.1f, _vehicleLevelHoldSeconds);
        Debug.Log($"[ThreatAlertFlowRunner] 车辆级停留 | vin={encryptVin} | {vehicleHold:F0}s");
        yield return WaitHoldSeconds(vehicleHold);
        _visualStage = ThreatVisualStage.None;

        Debug.Log($"[ThreatAlertFlowRunner] 车辆级停留结束，进入攻击链路 | vin={encryptVin}");
        yield return TransitionToAttackPathLevelAndWait();
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
        Debug.Log($"[ThreatAlertFlowRunner] 攻击链路级停留 | vin={encryptVin} | {attackHold:F0}s");
        yield return WaitHoldSeconds(attackHold);
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
                yield return TransitionAttackPathToPartAndWait(partId);
            }
            else
            {
                yield return TransitionToPartLevelAndWait(partId);
            }

            _visualStage = ThreatVisualStage.PartHold;
            float partHold = Mathf.Max(0.1f, _partLevelHoldSeconds);
            Debug.Log(
                $"[ThreatAlertFlowRunner] 零件级停留 ({i + 1}/{partIds.Count}) | part={partId} | {partHold:F0}s");
            yield return WaitHoldSeconds(partHold);
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

    private IEnumerator EnsureAtVehicleLevel(string provinceDisplayName, string plateModuleName)
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
            provinceName: provinceDisplayName,
            provinceModuleName: plateModuleName,
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
                    provinceName: null,
                    provinceModuleName: null,
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

    private IEnumerator TransitionToPartLevelAndWait(string partId)
    {
        yield return WaitForTransitionControllersIdle();
        yield return WaitForBoolStringTransition(
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted += h,
            h => EventManager.Instance.OnVehicleToPartTransitionCompleted -= h,
            () => MapApi.Instance.TransitionVehicleToPart(partId),
            $"车辆 → 零件 {partId}");
    }

    private IEnumerator RequestVehicleDataAndWait(string encryptVin)
    {
        CarVehicleDataController controller = CarVehicleDataController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[ThreatAlertFlowRunner] 未找到 CarVehicleDataController，跳过车辆信息请求。");
            yield break;
        }

        float waitRequestTimeout = 30f;
        while (controller.IsRequesting && waitRequestTimeout > 0f)
        {
            waitRequestTimeout -= Time.deltaTime;
            yield return null;
        }

        bool done = false;
        bool ok = false;
        Debug.Log($"[ThreatAlertFlowRunner] 请求车辆态势 | encryptVin={encryptVin}");
        controller.Request(
            encryptVin,
            startTime: null,
            endTime: null,
            onCompleted: (success, error) =>
            {
                done = true;
                ok = success;
                if (!success)
                {
                    Debug.LogWarning(
                        $"[ThreatAlertFlowRunner] 车辆信息加载失败，流程继续 | vin={encryptVin} | error={error}");
                }
            });

        float timeout = 45f;
        while (!done && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (controller.TryShowVehicleUiFromCache())
        {
            int slideCount = CarVehicleDataStore.Instance.BuildPartSlides().Count;
            if (ok)
            {
                Debug.Log(
                    $"[ThreatAlertFlowRunner] 车辆信息已刷新并展示 | vin={encryptVin} | slides={slideCount}");
            }
            else
            {
                string reason = !done ? "请求超时" : "接口失败";
                Debug.LogWarning(
                    $"[ThreatAlertFlowRunner] {reason}，使用已有缓存展示车辆信息 | vin={encryptVin} | slides={slideCount}");
            }
        }
        else if (!ok)
        {
            string reason = !done ? "请求超时" : "接口失败";
            Debug.LogWarning(
                $"[ThreatAlertFlowRunner] {reason}且无可用车辆缓存，流程仍继续 | vin={encryptVin}");
        }
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
        string provinceName,
        string provinceModuleName,
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
            provinceName: provinceName,
            provinceModuleName: provinceModuleName);
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
            confirmTimeoutSeconds -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForTransitionControllersIdle()
    {
        float timeout = 30f;
        while (ControlStateHierarchyTransitionController.IsAnyTransitionAnimationBusy() && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        VehicleToPartTransitionController partController = VehicleToPartTransitionController.Instance;
        timeout = 30f;
        while (partController != null && partController.IsTransitioning && timeout > 0f)
        {
            timeout -= Time.deltaTime;
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

        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        _lastTransitionSucceeded = _transitionStepDone;
        if (!_lastTransitionSucceeded)
        {
            Debug.LogWarning($"[ThreatAlertFlowRunner] {stepName} 等待完成超时。");
        }

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

        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

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

        float timeout = 60f;
        while (!_transitionStepDone && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

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
