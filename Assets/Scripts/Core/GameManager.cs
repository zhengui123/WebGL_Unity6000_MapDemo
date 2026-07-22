using UnityEngine;
using System;

/// <summary>
/// 操控状态总控（地球/国家/省级/车辆/零部件/攻击路径）。
/// 负责驱动 <see cref="MapApi"/> 切换与订阅 <see cref="EventManager"/> 事件推进状态。
/// </summary>
public class GameManager : UnitySingle<GameManager>
{
    public enum ControlState
    {
        EarthLevel = 0,   // 初始地球级别
        CountryLevel = 1, // 国家级别
        ProvinceLevel = 2,// 省级
        VehicleLevel = 3,    // 车辆级
        PartLevel = 4,       // 零部件级
        AttackPathLevel = 5  // 攻击路径级
    }

    /// <summary>大屏业务播放状态（与操控级别独立）。</summary>
    public enum BigScreenPlaybackState
    {
        /// <summary>默认状态（自动轮播等常规展示）。</summary>
        Default = 0,

        /// <summary>告警定位状态（事件驱动地图聚焦）。</summary>
        AlertPositioning = 1,

        /// <summary>威胁状态（威胁钻取联动）。</summary>
        Threat = 2,
    }

    [Header("当前操控状态（只读运行时）")]
    [SerializeField] private ControlState _currentState = ControlState.EarthLevel;

    [Header("当前大屏播放状态（只读运行时）")]
    [SerializeField] private BigScreenPlaybackState _currentPlaybackState = BigScreenPlaybackState.Default;

    [Header("引用（可留空，运行时查找）")]
    [SerializeField] private PlateMapDisplayController _plateMapDisplayController;

    [Header("默认省级（省名为空时使用）")]
    [Tooltip("默认省 adcode，如浙江 330000")]
    [SerializeField] private string _defaultProvinceCode = "330000";
    [Tooltip("默认省名（由 code 自动解析；也可 Inspector 预填）")]
    [SerializeField] private string _defaultProvinceName = "浙江";

    [Header("国家级别点击板块效果")]
    [Tooltip("进入国家级别后是否允许点击板块模块（默认不允许）")]
    [SerializeField] private bool _enablePlateClickAtCountryLevel = false;

    [Header("省级聚焦期间禁止重复点击")]
    [SerializeField] private bool _disableClickWhenFocusingProvince = true;

    public ControlState CurrentState => _currentState;

    public BigScreenPlaybackState CurrentPlaybackState => _currentPlaybackState;

    /// <summary>默认省 adcode（如 330000）。</summary>
    public string DefaultProvinceCode =>
        string.IsNullOrWhiteSpace(_defaultProvinceCode) ? "330000" : _defaultProvinceCode.Trim();

    /// <summary>默认省中文名（如 浙江）。</summary>
    public string DefaultProvinceName =>
        string.IsNullOrWhiteSpace(_defaultProvinceName) ? "浙江" : _defaultProvinceName.Trim();

    /// <summary>是否处于暂停。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>大屏播放状态变化时触发。</summary>
    public event Action<BigScreenPlaybackState> OnPlaybackStateChanged;

    /// <summary>暂停/恢复时触发（参数：是否已暂停）。</summary>
    public event Action<bool> OnPauseStateChanged;

    private bool _isPaused;
    private float _timeScaleBeforePause = 1f;

    private void Awake()
    {
        // 运行时确保引用存在
        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }

        EnsureDefaultProvinceNameFromCode();
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        em.OnTransitionToPlateMapCompleted += HandleTransitionToPlateCompleted;
        em.OnTransitionToEarthCompleted += HandleTransitionToEarthCompleted;
        em.OnPlateMapFocusModuleCompleted += HandlePlateMapFocusModuleCompleted;
        em.OnPlateMapRestoreCameraCompleted += HandlePlateMapRestoreCameraCompleted;
        em.OnPlateMapDisplayFocus += HandlePlateMapDisplayFocus;
        em.OnPlateToVehicleViewTransitionCompleted += HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionCompleted += HandleVehicleToPlateViewTransitionCompleted;
        em.OnVehicleToPartTransitionCompleted += HandleVehicleToPartTransitionCompleted;
        em.OnVehicleToPartTransitionReverseCompleted += HandleVehicleToPartTransitionReverseCompleted;
        em.OnVehicleToAttackPathTransitionCompleted += HandleVehicleToAttackPathTransitionCompleted;
        em.OnAttackPathToVehicleTransitionCompleted += HandleAttackPathToVehicleTransitionCompleted;
        em.OnAttackPathToPartTransitionCompleted += HandleAttackPathToPartTransitionCompleted;

        // 初始：地球级别（默认禁用点击）
        ApplyStateSideEffects(_currentState);

            
        EventManager.Instance.OnTransitionToPlateMapCompleted += ()=>{ Debug.Log("地球 → 板块过渡动画全部播放完毕");};
        EventManager.Instance.OnTransitionToEarthCompleted += ()=>{Debug.Log("板块 → 地球过渡动画全部播放完毕");};
        EventManager.Instance.OnPlateMapDisplayFocus += moduleName =>{Debug.Log("板块模块开始聚焦：" + moduleName);};
        EventManager.Instance.OnPlateMapFocusModuleCompleted += moduleName =>{Debug.Log("板块模块聚焦动画完成：" + moduleName);};
        EventManager.Instance.OnPlateMapRestoreCameraCompleted += () =>{Debug.Log("板块相机还原动画完成");};
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        em.OnTransitionToPlateMapCompleted -= HandleTransitionToPlateCompleted;
        em.OnTransitionToEarthCompleted -= HandleTransitionToEarthCompleted;
        em.OnPlateMapFocusModuleCompleted -= HandlePlateMapFocusModuleCompleted;
        em.OnPlateMapRestoreCameraCompleted -= HandlePlateMapRestoreCameraCompleted;
        em.OnPlateMapDisplayFocus -= HandlePlateMapDisplayFocus;
        em.OnPlateToVehicleViewTransitionCompleted -= HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionCompleted -= HandleVehicleToPlateViewTransitionCompleted;
        em.OnVehicleToPartTransitionCompleted -= HandleVehicleToPartTransitionCompleted;
        em.OnVehicleToPartTransitionReverseCompleted -= HandleVehicleToPartTransitionReverseCompleted;
        em.OnVehicleToAttackPathTransitionCompleted -= HandleVehicleToAttackPathTransitionCompleted;
        em.OnAttackPathToVehicleTransitionCompleted -= HandleAttackPathToVehicleTransitionCompleted;
        em.OnAttackPathToPartTransitionCompleted -= HandleAttackPathToPartTransitionCompleted;
    }


    public void Update()
    {
        // 测试：P 切换暂停 / 恢复
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    #region 暂停 / 恢复

    /// <summary>暂停游戏：Time.timeScale=0，并暂停全部 DOTween。</summary>
    public void PauseGame()
    {
        if (_isPaused)
        {
            return;
        }

        _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        DG.Tweening.DOTween.PauseAll();
        _isPaused = true;
        Debug.Log("[GameManager] 游戏已暂停");
        OnPauseStateChanged?.Invoke(true);
    }

    /// <summary>恢复游戏：还原 timeScale，并播放全部 DOTween。</summary>
    public void ResumeGame()
    {
        if (!_isPaused)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        DG.Tweening.DOTween.PlayAll();
        _isPaused = false;
        Debug.Log("[GameManager] 游戏已恢复");
        OnPauseStateChanged?.Invoke(false);
    }

    /// <summary>切换暂停 / 恢复。</summary>
    public void TogglePause()
    {
        if (_isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    #endregion

    #region 状态推进（事件驱动）

    private void HandleTransitionToPlateCompleted()
    {
        // 地球 → 板块过渡完成：进入国家级别
        SetState(ControlState.CountryLevel);
        ApplyStateSideEffects(ControlState.CountryLevel);
    }

    private void HandleTransitionToEarthCompleted()
    {
        RestorePlateDisplayForEarthLevel();
        SetState(ControlState.EarthLevel);
        ApplyStateSideEffects(ControlState.EarthLevel);
    }

    /// <summary>地球级就绪：板块根可能已隐藏，仍须立刻还原各模块透明度。</summary>
    private void RestorePlateDisplayForEarthLevel()
    {
        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }

        _plateMapDisplayController?.RestoreAllModulesAlphaImmediate();
    }

    // 省级聚焦开始（相机动动画开始时）
    private void HandlePlateMapDisplayFocus(string moduleName)
    {
        if (_disableClickWhenFocusingProvince && _currentState == ControlState.CountryLevel)
        {
            SetPlateClickEnabled(false);
        }
    }

   // 省级模块完成聚焦：进入省级
    private void HandlePlateMapFocusModuleCompleted(string moduleName)
    {
        SetState(ControlState.ProvinceLevel);
        ApplyStateSideEffects(ControlState.ProvinceLevel);
    }

    // 省级还原完成：回到国家级别
    private void HandlePlateMapRestoreCameraCompleted()
    {
        SetState(ControlState.CountryLevel);
        ApplyStateSideEffects(ControlState.CountryLevel);
    }

    /// <summary>板块界面 → 车辆界面过渡完成：进入车辆级别。</summary>
    private void HandlePlateToVehicleViewTransitionCompleted(string provinceName)
    {
        SetState(ControlState.VehicleLevel);
        ApplyStateSideEffects(ControlState.VehicleLevel);
    }

    /// <summary>车辆界面 → 板块界面过渡完成：回到省级。</summary>
    private void HandleVehicleToPlateViewTransitionCompleted(string provinceName)
    {
        SetState(ControlState.ProvinceLevel);
        ApplyStateSideEffects(ControlState.ProvinceLevel);
    }

    /// <summary>车辆 → 零件过渡完成：进入零部件级。</summary>
    private void HandleVehicleToPartTransitionCompleted(string partName)
    {
        SetState(ControlState.PartLevel);
        ApplyStateSideEffects(ControlState.PartLevel);
    }

    /// <summary>零件 → 车辆过渡倒播完成：回到车辆级。</summary>
    private void HandleVehicleToPartTransitionReverseCompleted(string partName)
    {
        SetState(ControlState.VehicleLevel);
        ApplyStateSideEffects(ControlState.VehicleLevel);
    }

    /// <summary>车辆 → 攻击路径过渡完成：进入攻击路径级。</summary>
    private void HandleVehicleToAttackPathTransitionCompleted()
    {
        SetState(ControlState.AttackPathLevel);
        ApplyStateSideEffects(ControlState.AttackPathLevel);
    }

    /// <summary>攻击路径 → 车辆过渡倒播完成：回到车辆级。</summary>
    private void HandleAttackPathToVehicleTransitionCompleted()
    {
        SetState(ControlState.VehicleLevel);
        ApplyStateSideEffects(ControlState.VehicleLevel);
    }

    /// <summary>攻击路径 → 零件过渡完成：进入零部件级。</summary>
    private void HandleAttackPathToPartTransitionCompleted(string _, string __)
    {
        SetState(ControlState.PartLevel);
        ApplyStateSideEffects(ControlState.PartLevel);
    }

    #endregion

    #region 对外：切换/还原（MapApi 总控）

    /// <summary>地球级别 → 国家级别：播放地球 → 板块过渡动画。</summary>
    public void SwitchToCountryLevel()
    {
        if (_currentState == ControlState.CountryLevel)
        {
            return;
        }

        MapApi.Instance.TransitionToPlateMap();
    }

    /// <summary>国家级别 → 地球级别：播放板块 → 地球过渡动画。</summary>
    public void RestoreToEarthLevel()
    {
        if (_currentState == ControlState.EarthLevel)
        {
            return;
        }

        MapApi.Instance.TransitionToEarth();
    }

    /// <summary>
    /// 国家级别 → 省级：触发聚焦某个省模块（moduleName 默认使用场景 GameObject 名）。
    /// 注意：最终进入省级状态在 <see cref="EventManager.OnPlateMapFocusModuleCompleted"/> 里完成。
    /// moduleName 为空时，用默认省 code 解析板块名。
    /// </summary>
    public void SwitchToProvinceLevel(string provinceModuleName)
    {
        if (_currentState != ControlState.CountryLevel)
        {
            return;
        }

        string moduleName = ResolveProvinceModuleName(provinceModuleName);
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Debug.LogWarning(
                $"[GameManager] 无法聚焦省级：模块名为空，且默认省 code={DefaultProvinceCode} 未解析到板块。");
            return;
        }

        MapApi.Instance.FocusPlateMapModule(moduleName);
    }

    /// <summary>
    /// 按省 code 设置默认省；自动查找并保存省名。
    /// </summary>
    /// <param name="provinceCode">省级 adcode，如 330000。</param>
    public bool SetDefaultProvinceCode(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            Debug.LogWarning("[GameManager] SetDefaultProvinceCode: provinceCode 为空。");
            return false;
        }

        string code = provinceCode.Trim();
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(code, out string normalized))
        {
            normalized = code;
        }

        if (!TryResolveProvinceNameByCode(normalized, out string provinceName))
        {
            Debug.LogWarning($"[GameManager] SetDefaultProvinceCode: 未找到 code={normalized} 对应省名。");
            return false;
        }

        _defaultProvinceCode = normalized;
        _defaultProvinceName = provinceName;
        Debug.Log($"[GameManager] 默认省已更新 | code={_defaultProvinceCode} | name={_defaultProvinceName}");
        return true;
    }

    /// <summary>省名为空时返回默认省名。</summary>
    public string ResolveProvinceName(string provinceName)
    {
        return string.IsNullOrWhiteSpace(provinceName) ? DefaultProvinceName : provinceName.Trim();
    }

    /// <summary>省 code 为空时返回默认省 code。</summary>
    public string ResolveProvinceCode(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return DefaultProvinceCode;
        }

        string code = provinceCode.Trim();
        return PlateMapBoundaryDatabase.TryNormalizeProvinceCode(code, out string normalized)
            ? normalized
            : code;
    }

    /// <summary>
    /// 板块模块名为空时，用默认省 code 解析场景板块名；解析失败则返回空。
    /// </summary>
    public string ResolveProvinceModuleName(string provinceModuleName)
    {
        if (!string.IsNullOrWhiteSpace(provinceModuleName))
        {
            return provinceModuleName.Trim();
        }

        if (PlateMapAPI.Instance != null &&
            PlateMapAPI.Instance.TryResolvePlateMapName(DefaultProvinceCode, out string plateName) &&
            !string.IsNullOrWhiteSpace(plateName))
        {
            return plateName.Trim();
        }

        return string.Empty;
    }

    private void EnsureDefaultProvinceNameFromCode()
    {
        string code = DefaultProvinceCode;
        if (TryResolveProvinceNameByCode(code, out string name))
        {
            _defaultProvinceCode = code;
            _defaultProvinceName = name;
        }
    }

    private static bool TryResolveProvinceNameByCode(string provinceCode, out string provinceName)
    {
        provinceName = null;
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return false;
        }

        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string adcodeName) &&
            !string.IsNullOrWhiteSpace(adcodeName))
        {
            provinceName = adcodeName.Trim();
            return true;
        }

        if (PlateMapAPI.Instance != null &&
            PlateMapAPI.Instance.TryGetProvinceName(provinceCode, out string boundaryName) &&
            !string.IsNullOrWhiteSpace(boundaryName))
        {
            provinceName = boundaryName.Trim();
            return true;
        }

        return false;
    }

    /// <summary>省级 → 国家级别：还原相机并回到国家视图。</summary>
    public void RestoreToCountryLevelFromProvince()
    {
        if (_currentState != ControlState.ProvinceLevel)
        {
            return;
        }

        MapApi.Instance.RestorePlateMapCamera();
    }

    #endregion

    #region 大屏播放状态

    /// <summary>切换大屏业务播放状态。</summary>
    public void SetPlaybackState(BigScreenPlaybackState newState)
    {
        if (_currentPlaybackState == newState)
        {
            return;
        }

        _currentPlaybackState = newState;
        Debug.Log($"[GameManager] 大屏播放状态 → {newState}");
        OnPlaybackStateChanged?.Invoke(newState);
    }

    public static string GetPlaybackStateDisplayName(BigScreenPlaybackState state)
    {
        switch (state)
        {
            case BigScreenPlaybackState.Default:
                return "默认状态";
            case BigScreenPlaybackState.AlertPositioning:
                return "告警定位状态";
            case BigScreenPlaybackState.Threat:
                return "威胁状态";
            default:
                return state.ToString();
        }
    }

    #endregion

    #region 状态副作用

    private void SetState(ControlState newState)
    {
        _currentState = newState;
    }

    private void ApplyStateSideEffects(ControlState state)
    {
        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }

        // 只有“国家级别”允许点击（由 Inspector bool 控制）
        if (state == ControlState.CountryLevel)
        {
            SetPlateClickEnabled(_enablePlateClickAtCountryLevel);
            return;
        }

        // 其它级别默认禁用点击
        SetPlateClickEnabled(false);
    }

    private void SetPlateClickEnabled(bool enabled)
    {
        if (_plateMapDisplayController == null)
        {
            return;
        }

        _plateMapDisplayController.enabled = enabled;
    }

    #endregion
}

