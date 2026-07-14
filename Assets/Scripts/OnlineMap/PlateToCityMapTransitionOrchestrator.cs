using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 板块地图 → 二维 GaodeMap → 城市模型 两阶段过渡总控。
/// 正播：阶段一 → 阶段二 → SwitchToKjCar + PlayHideTransition 并行；倒播：SwitchToRealyCar + PlayHideTransitionReverse 并行 → 地图倒放。
/// </summary>
[DisallowMultipleComponent]
public class PlateToCityMapTransitionOrchestrator : MonoBehaviour
{
    [Header("过渡控制器（留空则自动查找）")]
    [SerializeField] private PlateToGaodeMapTransitionController _plateTransitionController;
    [SerializeField] private GaodeToCityTransitionController _cityTransitionController;
    [FormerlySerializedAs("_carModelChangeController")]
    [SerializeField] private CarModelDissolveController _carModelDissolveController;
    [SerializeField] private CityHideTransitionController _cityHideTransitionController;

    [Header("默认省份（无法从聚焦板块解析 code 时回退）")]
    [SerializeField] private string _defaultProvinceName = "山东";

    private bool _isOrchestrating;
    private bool _isForwardOrchestration;
    /// <summary>编排器触发的车辆溶解阶段（正播末尾 / 倒播开头）。</summary>
    private OrchestratorCarPhase _carPhase = OrchestratorCarPhase.None;
    private string _activeProvinceName;

    private enum OrchestratorCarPhase
    {
        None,
        ForwardEndToKj,
        ReverseStartToRealy
    }

    public bool IsOrchestrating => _isOrchestrating;

    private static PlateToCityMapTransitionOrchestrator _instance;

    public static PlateToCityMapTransitionOrchestrator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlateToCityMapTransitionOrchestrator>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToCityMapTransitionPlay += HandlePlateToCityPlayRequested;
        em.OnCityToPlateMapTransitionReverse += HandleCityToPlateReverseRequested;
        em.OnPlateToGaodeMapTransitionCompleted += HandlePlateStageForwardCompleted;
        em.OnGaodeMapToCityTransitionCompleted += HandleCityStageForwardCompleted;
        em.OnCityToGaodeMapTransitionReverseCompleted += HandleCityStageReverseCompleted;
        em.OnGaodeMapToPlateTransitionCompleted += HandlePlateStageReverseCompleted;
        em.OnCarSwitchToKjCarCompleted += HandleCarSwitchToKjCarCompleted;
        em.OnCarSwitchToRealyCarCompleted += HandleCarSwitchToRealyCarCompleted;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToCityMapTransitionPlay -= HandlePlateToCityPlayRequested;
        em.OnCityToPlateMapTransitionReverse -= HandleCityToPlateReverseRequested;
        em.OnPlateToGaodeMapTransitionCompleted -= HandlePlateStageForwardCompleted;
        em.OnGaodeMapToCityTransitionCompleted -= HandleCityStageForwardCompleted;
        em.OnCityToGaodeMapTransitionReverseCompleted -= HandleCityStageReverseCompleted;
        em.OnGaodeMapToPlateTransitionCompleted -= HandlePlateStageReverseCompleted;
        em.OnCarSwitchToKjCarCompleted -= HandleCarSwitchToKjCarCompleted;
        em.OnCarSwitchToRealyCarCompleted -= HandleCarSwitchToRealyCarCompleted;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>正播：板块 → GaodeMap → City-Maker。省名/code 为空时用聚焦板块 provinceCode。</summary>
    public bool PlayFullTransition(string provinceNameOrCode = null)
    {
        if (_isOrchestrating)
        {
            return false;
        }

        ResolveReferences();
        if (_plateTransitionController == null || _cityTransitionController == null)
        {
            Debug.LogError("[PlateToCityOrchestrator] 未找到阶段过渡控制器。");
            return false;
        }

        if (_plateTransitionController.IsTransitioning
            || _cityTransitionController.IsTransitioning
            || IsCarTransitionBlocking()
            || IsCityHideTransitionBlocking())
        {
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceNameOrCode);
        _isOrchestrating = true;
        _isForwardOrchestration = true;
        _carPhase = OrchestratorCarPhase.None;

        EventManager.Instance?.TriggerPlateToCityMapTransitionStarted(_activeProvinceName);
        EventManager.Instance?.TriggerPlateToVehicleViewTransitionStarted(_activeProvinceName);

        if (!_plateTransitionController.PlayTransition(_activeProvinceName))
        {
            ResetOrchestration();
            return false;
        }

        return true;
    }

    /// <summary>倒播：SwitchToRealyCar → City-Maker → GaodeMap → 板块。</summary>
    public bool PlayFullTransitionReverse(string provinceNameOrCode = null)
    {
        if (_isOrchestrating)
        {
            return false;
        }

        ResolveReferences();
        if (_plateTransitionController == null || _cityTransitionController == null)
        {
            Debug.LogError("[PlateToCityOrchestrator] 未找到阶段过渡控制器。");
            return false;
        }

        if (_plateTransitionController.IsTransitioning
            || _cityTransitionController.IsTransitioning
            || IsCarTransitionBlocking()
            || IsCityHideTransitionBlocking())
        {
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceNameOrCode);
        _isOrchestrating = true;
        _isForwardOrchestration = false;
        _carPhase = OrchestratorCarPhase.None;

        EventManager.Instance?.TriggerCityToPlateMapTransitionReverseStarted(_activeProvinceName);
        EventManager.Instance?.TriggerVehicleToPlateViewTransitionStarted(_activeProvinceName);

        PlayCarTransitionAtReverseStart();
        return true;
    }

    private void HandlePlateToCityPlayRequested(string provinceName)
    {
        PlayFullTransition(provinceName);
    }

    private void HandleCityToPlateReverseRequested(string provinceName)
    {
        PlayFullTransitionReverse(provinceName);
    }

    private void HandlePlateStageForwardCompleted(string provinceName)
    {
        if (!_isOrchestrating || !_isForwardOrchestration)
        {
            return;
        }

        if (_cityTransitionController == null)
        {
            Debug.LogError("[PlateToCityOrchestrator] 阶段一完成，但未找到 GaodeToCityTransitionController。");
            ResetOrchestration();
            return;
        }

        if (!_cityTransitionController.PlayTransition())
        {
            Debug.LogError("[PlateToCityOrchestrator] 阶段二正播启动失败。");
            ResetOrchestration();
        }
    }

    private void HandleCityStageForwardCompleted()
    {
        if (!_isOrchestrating || !_isForwardOrchestration)
        {
            return;
        }

        PlayCarTransitionAtForwardEnd();
    }

    private void HandleCityStageReverseCompleted()
    {
        if (!_isOrchestrating || _isForwardOrchestration)
        {
            return;
        }

        if (_plateTransitionController == null)
        {
            Debug.LogError("[PlateToCityOrchestrator] 阶段二倒播完成，但未找到 PlateToGaodeMapTransitionController。");
            ResetOrchestration();
            return;
        }

        if (!_plateTransitionController.PlayTransitionReverse(_activeProvinceName))
        {
            Debug.LogError("[PlateToCityOrchestrator] 阶段一倒播启动失败。");
            ResetOrchestration();
        }
    }

    private void HandlePlateStageReverseCompleted(string provinceName)
    {
        if (!_isOrchestrating || _isForwardOrchestration)
        {
            return;
        }

        CompleteReverseOrchestration();
    }

    private void HandleCarSwitchToKjCarCompleted()
    {
        if (!_isOrchestrating || !_isForwardOrchestration || _carPhase != OrchestratorCarPhase.ForwardEndToKj)
        {
            return;
        }

        CompleteForwardOrchestration();
    }

    private void HandleCarSwitchToRealyCarCompleted()
    {
        if (!_isOrchestrating || _isForwardOrchestration || _carPhase != OrchestratorCarPhase.ReverseStartToRealy)
        {
            return;
        }

        _carPhase = OrchestratorCarPhase.None;
        BeginReverseMapTransitions();
    }

    /// <summary>正播末尾：SwitchToKjCar 与 PlayHideTransition 同时启动（不等待城市隐藏完成）。</summary>
    private void PlayCarTransitionAtForwardEnd()
    {
        ResolveReferences();
        TryPlayCityHideTransitionParallel(forward: true);

        if (_carModelDissolveController == null)
        {
            Debug.LogWarning("[PlateToCityOrchestrator] 未找到 CarModelDissolveController，跳过车辆溶解并直接完成正播。");
            CompleteForwardOrchestration();
            return;
        }

        _carPhase = OrchestratorCarPhase.ForwardEndToKj;
        if (!_carModelDissolveController.SwitchToKjCar())
        {
            Debug.LogWarning("[PlateToCityOrchestrator] SwitchToKjCar 未启动，直接完成正播。");
            _carPhase = OrchestratorCarPhase.None;
            CompleteForwardOrchestration();
        }
    }

    /// <summary>倒播开头：SwitchToRealyCar 与 PlayHideTransitionReverse 同时启动。</summary>
    private void PlayCarTransitionAtReverseStart()
    {
        ResolveReferences();
        TryPlayCityHideTransitionParallel(forward: false);

        if (_carModelDissolveController == null)
        {
            Debug.LogWarning("[PlateToCityOrchestrator] 未找到 CarModelDissolveController，跳过车辆溶解并直接开始倒播。");
            BeginReverseMapTransitions();
            return;
        }

        _carPhase = OrchestratorCarPhase.ReverseStartToRealy;
        if (!_carModelDissolveController.SwitchToRealyCar())
        {
            Debug.LogWarning("[PlateToCityOrchestrator] SwitchToRealyCar 未启动，直接开始倒播。");
            _carPhase = OrchestratorCarPhase.None;
            BeginReverseMapTransitions();
        }
    }

    /// <summary>与车辆切换并行播放城市隐藏/显现，失败仅打日志，不影响编排器等待车辆完成事件。</summary>
    private void TryPlayCityHideTransitionParallel(bool forward)
    {
        if (_cityHideTransitionController == null)
        {
            return;
        }

        bool started = forward
            ? _cityHideTransitionController.PlayHideTransition()
            : _cityHideTransitionController.PlayHideTransitionReverse();

        if (!started)
        {
            Debug.LogWarning(forward
                ? "[PlateToCityOrchestrator] PlayHideTransition 未启动，继续等待车辆切换完成。"
                : "[PlateToCityOrchestrator] PlayHideTransitionReverse 未启动，继续等待车辆切换完成。");
        }
    }

    /// <summary>车辆已切回 RealyCar，启动 City-Maker → GaodeMap 倒播。</summary>
    private void BeginReverseMapTransitions()
    {
        ResolveReferences();
        if (_cityTransitionController == null)
        {
            Debug.LogError("[PlateToCityOrchestrator] 未找到 GaodeToCityTransitionController，倒播中止。");
            ResetOrchestration();
            return;
        }

        if (!_cityTransitionController.PlayTransitionReverse())
        {
            Debug.LogError("[PlateToCityOrchestrator] 阶段二倒播启动失败。");
            ResetOrchestration();
        }
    }

    private void CompleteForwardOrchestration()
    {
        string provinceName = _activeProvinceName;
        ResetOrchestration();
        EventManager.Instance?.TriggerPlateToCityMapTransitionCompleted(provinceName);
        EventManager.Instance?.TriggerPlateToVehicleViewTransitionCompleted(provinceName);
    }

    private void CompleteReverseOrchestration()
    {
        string provinceName = _activeProvinceName;
        ResetOrchestration();
        EventManager.Instance?.TriggerCityToPlateMapTransitionReverseCompleted(provinceName);
        EventManager.Instance?.TriggerVehicleToPlateViewTransitionCompleted(provinceName);
    }

    private bool IsCarTransitionBlocking()
    {
        ResolveReferences();
        return _carModelDissolveController != null && _carModelDissolveController.IsTransitioning;
    }

    private bool IsCityHideTransitionBlocking()
    {
        ResolveReferences();
        return _cityHideTransitionController != null && _cityHideTransitionController.IsTransitioning;
    }

    private void ResetOrchestration()
    {
        _isOrchestrating = false;
        _isForwardOrchestration = false;
        _carPhase = OrchestratorCarPhase.None;
    }

    private string ResolveProvinceName(string provinceNameOrCodeOverride)
    {
        return PlateProvinceFocusResolver.ResolveProvinceName(
            provinceNameOrCodeOverride,
            _defaultProvinceName);
    }

    private void ResolveReferences()
    {
        if (_plateTransitionController == null)
        {
            _plateTransitionController = PlateToGaodeMapTransitionController.Instance;
        }

        if (_cityTransitionController == null)
        {
            _cityTransitionController = GaodeToCityTransitionController.Instance;
        }

        if (_carModelDissolveController == null)
        {
            _carModelDissolveController = CarModelDissolveController.Instance;
        }

        if (_cityHideTransitionController == null)
        {
            _cityHideTransitionController = CityHideTransitionController.Instance;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("测试：正播 板块→城市")]
    private void EditorTestPlayFull()
    {
        PlayFullTransition(_defaultProvinceName);
    }

    [ContextMenu("测试：倒播 城市→板块")]
    private void EditorTestPlayFullReverse()
    {
        PlayFullTransitionReverse(_defaultProvinceName);
    }
#endif
}
