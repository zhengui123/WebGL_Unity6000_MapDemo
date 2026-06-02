using UnityEngine;
using System;

/// <summary>
/// 操控状态总控（地球/国家/省级/车辆/零部件）。
/// 负责驱动 <see cref="MapApi"/> 切换与订阅 <see cref="EventManager"/> 事件推进状态。
/// </summary>
public class GameManager : UnitySingle<GameManager>
{
    public enum ControlState
    {
        EarthLevel = 0,   // 初始地球级别
        CountryLevel = 1, // 国家级别
        ProvinceLevel = 2,// 省级
        VehicleLevel = 3, // 车辆级
        PartLevel = 4     // 零部件级
    }

    [Header("当前操控状态（只读运行时）")]
    [SerializeField] private ControlState _currentState = ControlState.EarthLevel;

    [Header("引用（可留空，运行时查找）")]
    [SerializeField] private PlateMapDisplayController _plateMapDisplayController;

    [Header("国家级别点击板块效果")]
    [Tooltip("进入国家级别后是否允许点击板块模块（默认不允许）")]
    [SerializeField] private bool _enablePlateClickAtCountryLevel = false;

    [Header("省级聚焦期间禁止重复点击")]
    [SerializeField] private bool _disableClickWhenFocusingProvince = true;

    public ControlState CurrentState => _currentState;

    private void Awake()
    {
        // 运行时确保引用存在
        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        em.OnTransitionToPlateMapCompleted += HandleTransitionToPlateCompleted;
        em.OnTransitionToEarthCompleted += HandleTransitionToEarthCompleted;
        em.OnPlateMapFocusModuleCompleted += HandlePlateMapFocusModuleCompleted;
        em.OnPlateMapRestoreCameraCompleted += HandlePlateMapRestoreCameraCompleted;
        em.OnPlateMapDisplayFocus += HandlePlateMapDisplayFocus;

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
    }


    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SwitchToCountryLevel();
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            RestoreToEarthLevel();
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            SwitchToProvinceLevel("polySurface3");
        }
        if (Input.GetKeyDown(KeyCode.Escape) && _currentState == ControlState.ProvinceLevel)
        {
            RestoreToCountryLevelFromProvince();
        }
    }

    #region 状态推进（事件驱动）

    private void HandleTransitionToPlateCompleted()
    {
        // 地球 → 板块过渡完成：进入国家级别
        SetState(ControlState.CountryLevel);
        ApplyStateSideEffects(ControlState.CountryLevel);
    }

    private void HandleTransitionToEarthCompleted()
    {
        // 板块 → 地球过渡完成：进入地球级别
        SetState(ControlState.EarthLevel);
        ApplyStateSideEffects(ControlState.EarthLevel);
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
    /// </summary>
    public void SwitchToProvinceLevel(string provinceModuleName)
    {
        if (_currentState != ControlState.CountryLevel)
        {
            return;
        }

        MapApi.Instance.FocusPlateMapModule(provinceModuleName);
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

