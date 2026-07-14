using UnityEngine;

/// <summary>
/// 车辆热力点显示与轮询调度：仅国家级/省级轮询；按 provinceCode 启停各板块 Controller 绘制。
/// </summary>
[DisallowMultipleComponent]
public class CarHotManager : UnitySingle<CarHotManager>
{
    [Header("调试")]
    [SerializeField] private bool _logStateChanges = true;

    [Tooltip("当前应显示热力图的省份 code；0=全国。")]
    [SerializeField] private string _activeProvinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;

    private string _focusedModuleName;

    /// <summary>当前热力图目标省份 code。</summary>
    public string ActiveProvinceCode => _activeProvinceCode;

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnTransitionToPlateMapCompleted += HandleEnterCountryLevel;
        em.OnTransitionToEarthCompleted += HandleLeaveHeatmapLevels;
        em.OnPlateMapFocusModuleCompleted += HandleEnterProvinceLevel;
        em.OnPlateMapRestoreCameraCompleted += HandleEnterCountryLevel;
        em.OnPlateToVehicleViewTransitionCompleted += HandleLeaveHeatmapLevels;
        em.OnVehicleToPlateViewTransitionCompleted += HandleReturnToProvinceFromVehicle;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnTransitionToPlateMapCompleted -= HandleEnterCountryLevel;
        em.OnTransitionToEarthCompleted -= HandleLeaveHeatmapLevels;
        em.OnPlateMapFocusModuleCompleted -= HandleEnterProvinceLevel;
        em.OnPlateMapRestoreCameraCompleted -= HandleEnterCountryLevel;
        em.OnPlateToVehicleViewTransitionCompleted -= HandleLeaveHeatmapLevels;
        em.OnVehicleToPlateViewTransitionCompleted -= HandleReturnToProvinceFromVehicle;

        StopHeatmapPolling();
    }

    private void Start()
    {
        SyncFromGameManagerState();
    }

    /// <summary>按 GameManager 当前 ControlState 同步轮询与显隐。</summary>
    public void SyncFromGameManagerState()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        switch (gm.CurrentState)
        {
            case GameManager.ControlState.CountryLevel:
                EnterNationalHeatmap();
                break;
            case GameManager.ControlState.ProvinceLevel:
                EnterProvinceHeatmap(ResolveProvinceCodeFromFocusedModule());
                break;
            default:
                LeaveHeatmapLevels();
                break;
        }
    }

    private void HandleEnterCountryLevel()
    {
        EnterNationalHeatmap();
    }

    private void HandleEnterProvinceLevel(string moduleName)
    {
        _focusedModuleName = moduleName;
        EnterProvinceHeatmap(ResolveProvinceCodeFromModuleName(moduleName));
    }

    private void HandleReturnToProvinceFromVehicle(string provinceName)
    {
        string moduleHint = !string.IsNullOrWhiteSpace(_focusedModuleName)
            ? _focusedModuleName
            : provinceName;
        EnterProvinceHeatmap(ResolveProvinceCodeFromModuleName(moduleHint));
    }

    private void HandleLeaveHeatmapLevels()
    {
        LeaveHeatmapLevels();
    }

    private void HandleLeaveHeatmapLevels(string _)
    {
        LeaveHeatmapLevels();
    }

    /// <summary>国家界面：只显示 code=0 热力图，默认参数轮询。</summary>
    public void EnterNationalHeatmap()
    {
        _activeProvinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;
        ApplyControllerEnableState(_activeProvinceCode);
        BeginPollingForProvince(string.Empty);
        LogState("国家热力图（code=0）");
    }

    /// <summary>省级界面：只显示当前省热力图，请求携带该省 code。</summary>
    public void EnterProvinceHeatmap(string provinceCode)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalized) ||
            normalized == PlateMapBoundaryDatabase.NationalProvinceCode)
        {
            Debug.LogWarning(
                $"[CarHotManager] 省级热力图 provinceCode 无效：{provinceCode}，回退全国。");
            EnterNationalHeatmap();
            return;
        }

        _activeProvinceCode = normalized;
        ApplyControllerEnableState(_activeProvinceCode);
        BeginPollingForProvince(_activeProvinceCode);
        LogState($"省级热力图（code={_activeProvinceCode}）");
    }

    /// <summary>离开国家/省级：停止轮询。</summary>
    public void LeaveHeatmapLevels()
    {
        StopHeatmapPolling();
        LogState("离开国家/省级，已停止热力图轮询");
    }

    private void BeginPollingForProvince(string apiProvinceCode)
    {
        VehicleHeatmapApiController api = VehicleHeatmapApiController.Instance;
        if (api == null)
        {
            Debug.LogWarning("[CarHotManager] VehicleHeatmapApiController 未找到。");
            return;
        }

        api.SetProvinceCode(apiProvinceCode, requestImmediately: false);
        if (!api.IsPolling)
        {
            api.StartPolling();
        }
        else
        {
            api.RequestOnce();
        }
    }

    private void StopHeatmapPolling()
    {
        VehicleHeatmapApiController api = VehicleHeatmapApiController.Instance;
        if (api != null && api.IsPolling)
        {
            api.StopPolling();
        }
    }

    /// <summary>
    /// 仅启用目标 provinceCode 对应板块上的 PlateMapVehiclePointController；其余关闭（停止绘制）。
    /// </summary>
    public void ApplyControllerEnableState(string activeProvinceCode)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(activeProvinceCode, out string targetCode))
        {
            targetCode = PlateMapBoundaryDatabase.NationalProvinceCode;
        }

        PlateMapGeoConverter[] converters =
            FindObjectsByType<PlateMapGeoConverter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < converters.Length; i++)
        {
            PlateMapGeoConverter converter = converters[i];
            if (converter == null)
            {
                continue;
            }

            PlateMapVehiclePointController controller =
                converter.GetComponent<PlateMapVehiclePointController>();
            if (controller == null)
            {
                continue;
            }

            string code = converter.ProvinceCode;
            if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(code, out string normalized))
            {
                normalized = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
            }

            bool shouldEnable = normalized == targetCode;
            if (controller.enabled != shouldEnable)
            {
                controller.enabled = shouldEnable;
            }
        }
    }

    private string ResolveProvinceCodeFromFocusedModule()
    {
        PlateMapDisplayController display = PlateMapDisplayController.Instance;
        if (display != null && display.FocusedModule != null)
        {
            return ResolveProvinceCodeFromModuleName(display.FocusedModule.gameObject.name);
        }

        if (!string.IsNullOrWhiteSpace(_focusedModuleName))
        {
            return ResolveProvinceCodeFromModuleName(_focusedModuleName);
        }

        return PlateMapBoundaryDatabase.NationalProvinceCode;
    }

    /// <summary>从板块模块名解析省份 code（读 PlateMapGeoConverter）。</summary>
    public static string ResolveProvinceCodeFromModuleName(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return PlateMapBoundaryDatabase.NationalProvinceCode;
        }

        PlateMapGeoConverter geo = FindGeoConverterForModuleName(moduleName);
        if (geo == null)
        {
            Debug.LogWarning($"[CarHotManager] 无法从模块「{moduleName}」解析 provinceCode。");
            return PlateMapBoundaryDatabase.NationalProvinceCode;
        }

        if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(geo.ProvinceCode, out string normalized))
        {
            return normalized;
        }

        return geo.ProvinceCode;
    }

    private static PlateMapGeoConverter FindGeoConverterForModuleName(string moduleName)
    {
        // 1) GeoConverter / Controller 所在物体名与模块名一致
        PlateMapGeoConverter[] converters =
            FindObjectsByType<PlateMapGeoConverter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < converters.Length; i++)
        {
            PlateMapGeoConverter converter = converters[i];
            if (converter != null && converter.gameObject.name == moduleName)
            {
                return converter;
            }
        }

        // 2) DisplayModule 名匹配，再向上/下找 GeoConverter
        PlateMapDisplayModule[] modules =
            FindObjectsByType<PlateMapDisplayModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < modules.Length; i++)
        {
            PlateMapDisplayModule module = modules[i];
            if (module == null || module.gameObject.name != moduleName)
            {
                continue;
            }

            PlateMapGeoConverter geo = module.GetComponentInParent<PlateMapGeoConverter>();
            if (geo == null)
            {
                geo = module.GetComponentInChildren<PlateMapGeoConverter>(true);
            }

            return geo;
        }

        // 3) 场景中任意同名 GameObject
        GameObject named = GameObject.Find(moduleName);
        if (named != null)
        {
            PlateMapGeoConverter geo = named.GetComponentInParent<PlateMapGeoConverter>();
            if (geo == null)
            {
                geo = named.GetComponentInChildren<PlateMapGeoConverter>(true);
            }

            return geo;
        }

        return null;
    }

    private void LogState(string message)
    {
        if (_logStateChanges)
        {
            Debug.Log($"[CarHotManager] {message}");
        }
    }
}
