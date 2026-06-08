using UnityEngine;

/// <summary>
/// 板块地图 → 二维 GaodeMap → 城市模型 两阶段过渡总控。
/// 正播：阶段一完成后立刻播放阶段二；倒播：阶段二倒放完成后立刻播放阶段一倒放。
/// </summary>
[DisallowMultipleComponent]
public class PlateToCityMapTransitionOrchestrator : MonoBehaviour
{
    [Header("过渡控制器（留空则自动查找）")]
    [SerializeField] private PlateToGaodeMapTransitionController _plateTransitionController;
    [SerializeField] private GaodeToCityTransitionController _cityTransitionController;

    [Header("默认省份（事件未传参时使用）")]
    [SerializeField] private string _defaultProvinceName = "山东";

    private bool _isOrchestrating;
    private bool _isForwardOrchestration;
    private string _activeProvinceName;

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
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>正播：板块 → GaodeMap → City-Maker。</summary>
    public bool PlayFullTransition(string provinceName = null)
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

        if (_plateTransitionController.IsTransitioning || _cityTransitionController.IsTransitioning)
        {
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceName);
        _isOrchestrating = true;
        _isForwardOrchestration = true;

        EventManager.Instance?.TriggerPlateToCityMapTransitionStarted(_activeProvinceName);

        if (!_plateTransitionController.PlayTransition(_activeProvinceName))
        {
            ResetOrchestration();
            return false;
        }

        return true;
    }

    /// <summary>倒播：City-Maker → GaodeMap → 板块。</summary>
    public bool PlayFullTransitionReverse(string provinceName = null)
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

        if (_plateTransitionController.IsTransitioning || _cityTransitionController.IsTransitioning)
        {
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceName);
        _isOrchestrating = true;
        _isForwardOrchestration = false;

        EventManager.Instance?.TriggerCityToPlateMapTransitionReverseStarted(_activeProvinceName);

        if (!_cityTransitionController.PlayTransitionReverse())
        {
            ResetOrchestration();
            return false;
        }

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

        string provinceName = _activeProvinceName;
        ResetOrchestration();
        EventManager.Instance?.TriggerPlateToCityMapTransitionCompleted(provinceName);
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

        ResetOrchestration();
        EventManager.Instance?.TriggerCityToPlateMapTransitionReverseCompleted(provinceName);
    }

    private void ResetOrchestration()
    {
        _isOrchestrating = false;
        _isForwardOrchestration = false;
    }

    private string ResolveProvinceName(string provinceNameOverride)
    {
        if (!string.IsNullOrWhiteSpace(provinceNameOverride))
        {
            return provinceNameOverride.Trim();
        }

        return _defaultProvinceName;
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
