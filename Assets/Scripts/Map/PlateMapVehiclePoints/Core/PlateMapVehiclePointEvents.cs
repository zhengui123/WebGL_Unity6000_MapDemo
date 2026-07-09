using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>写入板块车辆点位；<paramref name="syncNow"/> 为 true 时立即刷新 GPU 显示。</summary>
public delegate void PlateMapSetVehiclePointsAction(VehicleMapPointData[] points, bool syncNow);

/// <summary>读取板块当前缓存的车辆点位数组。</summary>
public delegate VehicleMapPointData[] PlateMapGetCurrentVehiclePointsAction();

/// <summary>逐点过滤：返回 false 时该点不参与合并与绘制（如省界外）。</summary>
public delegate bool PlateMapShouldIncludePointAction(VehicleMapPointData data);

/// <summary>显示前批量变换点位（如去重、裁剪）；未注册时原样返回。</summary>
public delegate VehicleMapPointData[] PlateMapTransformPointsBeforeDisplayAction(VehicleMapPointData[] source);

/// <summary>按当前数据与过滤规则重建 GPU 显示（不修改点位源数据）。</summary>
public delegate void PlateMapRefreshVehiclePointsDisplayAction();

/// <summary>重建板块地理映射（由 <see cref="PlateMapGeoConverter"/> 实现）。</summary>
public delegate void PlateMapGeoConverterRebuildAction();

/// <summary>地理转换是否就绪（锚点与纬度范围有效）。</summary>
public delegate bool PlateMapIsGeoConverterReadyAction();

/// <summary>WGS84 经纬度 → 地图根节点局部坐标（Y 由调用方贴地）。</summary>
public delegate bool PlateMapTryLonLatToLocalAction(double longitude, double latitude, out Vector3 localPosition);

/// <summary>获取板块映射用的经纬度外接矩形（西、东、南、北）。</summary>
public delegate void PlateMapGetProvinceBoundsAction(
    out double westLongitude,
    out double eastLongitude,
    out double southLatitude,
    out double northLatitude);

/// <summary>
/// 板块地图车辆点位事件总线（单例）。
/// <para>
/// 解耦「数据写入 / 地理转换 / 显示过滤」：各板块组件在 <c>OnEnable</c> 注册、<c>OnDisable</c> 注销，
/// 外部通过 <see cref="PlateMapAPI"/> 或 <see cref="PublishSetVehiclePoints"/> 按名称推送数据。
/// </para>
/// <para>字典 key 统一为板块地图根物体的 <c>gameObject.name</c>；业务侧推荐通过 <see cref="PlateMapAPI"/> 按 provinceCode 调用。</para>
/// </summary>
/// <remarks>
/// 典型注册方：
/// <list type="bullet">
/// <item><see cref="PlateMapVehiclePointController"/> — Set / Get 点位</item>
/// <item><see cref="PlateMapGeoConverter"/> — 经纬度转换四件套 + provinceCode 映射</item>
/// </list>
/// </remarks>
[DisallowMultipleComponent]
public class PlateMapVehiclePointEvents : UnitySingle<PlateMapVehiclePointEvents>
{
    /// <summary>单板块全部回调句柄，避免为每种委托维护独立字典。</summary>
    private sealed class PlateHandlers
    {
      
        /// <summary>由 Controller 注册，接收外部点位写入。</summary>
        public PlateMapSetVehiclePointsAction SetVehiclePoints;

        /// <summary>由 Controller 注册，供查询当前点位。</summary>
        public PlateMapGetCurrentVehiclePointsAction GetCurrentVehiclePoints;

        /// <summary>可选；Demo 或业务层注册，绘制前逐点过滤。</summary>
        public PlateMapShouldIncludePointAction ShouldIncludePoint;

        /// <summary>可选；显示前对整批点位做变换。</summary>
        public PlateMapTransformPointsBeforeDisplayAction TransformBeforeDisplay;

        /// <summary>由 Controller 注册，仅刷新 GPU 显示。</summary>
        public PlateMapRefreshVehiclePointsDisplayAction RefreshVehiclePointsDisplay;

        /// <summary>由 GeoConverter 注册，触发锚点/网格映射重建。</summary>
        public PlateMapGeoConverterRebuildAction GeoConverterRebuild;

        /// <summary>由 GeoConverter 注册，查询映射是否可用。</summary>
        public PlateMapIsGeoConverterReadyAction IsGeoConverterReady;

        /// <summary>由 GeoConverter 注册，经纬度转局部坐标。</summary>
        public PlateMapTryLonLatToLocalAction TryLonLatToLocal;

        /// <summary>由 GeoConverter 注册，返回省内经纬度包围盒。</summary>
        public PlateMapGetProvinceBoundsAction GetProvinceBounds;

        /// <summary>全部槽位为空时可从字典移除该板块条目。</summary>
        public bool IsEmpty =>
            SetVehiclePoints == null &&
            GetCurrentVehiclePoints == null &&
            ShouldIncludePoint == null &&
            TransformBeforeDisplay == null &&
            RefreshVehiclePointsDisplay == null &&
            GeoConverterRebuild == null &&
            IsGeoConverterReady == null &&
            TryLonLatToLocal == null &&
            GetProvinceBounds == null;
    }

    /// <summary>当前默认 provinceCode（业务可设置；POI 旧接口回退用）。</summary>
    public string activeProvinceCode = HttpProjectConfig.DefaultProvinceCode;

    /// <summary>板块名</summary>
    public string plateMapName;

    public List<string> plateMapNameList = new List<string>();

    /// <summary>板块名 → 该板块已注册的回调集合。</summary>
    private readonly Dictionary<string, PlateHandlers> _plates = new();

    /// <summary>省级 adcode（string，"0"=全国）→ 场景板块 GameObject 名称。</summary>
    private readonly Dictionary<string, string> _provinceCodeToPlateMapName = new();

    /// <summary>板块隐藏时仍保留的车辆点位数据源（Controller 未注册 Set 回调时由 Hub 持有）。</summary>
    private readonly Dictionary<string, VehicleMapPointData[]> _vehiclePointsCache = new();

    /// <summary>板块重新显示后需重建 GPU 热力图显示的板块名。</summary>
    private readonly HashSet<string> _pendingDisplayRefresh = new();

    /// <summary>点位即将被 Controller 覆盖前广播（参数为即将写入的新数组）。</summary>
    public event Action<string, VehicleMapPointData[]> VehiclePointsWillChangeAction;

    /// <summary>点位已写入 Controller 后广播（参数为当前缓存数组）。</summary>
    public event Action<string, VehicleMapPointData[]> VehiclePointsChangedAction;

    /// <summary>Controller 开始重建 GPU 实例化显示时广播。</summary>
    public event Action<string> RebuildStartedAction;

    #region 注册 / 注销（板块名为 gameObject.name）

    /// <summary>注册 provinceCode 与场景板块名的映射（由 <see cref="PlateMapGeoConverter"/> 调用）。</summary>
    public void RegisterProvinceCodeMapping(string provinceCode, string plateMapName)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode) ||
            string.IsNullOrWhiteSpace(plateMapName))
        {
            return;
        }

        _provinceCodeToPlateMapName[normalizedCode] = plateMapName.Trim();
    }

    /// <summary>注销 provinceCode 映射。</summary>
    public void UnregisterProvinceCodeMapping(string provinceCode, string plateMapName)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return;
        }

        if (_provinceCodeToPlateMapName.TryGetValue(normalizedCode, out string registeredName) &&
            registeredName == plateMapName.Trim())
        {
            _provinceCodeToPlateMapName.Remove(normalizedCode);
        }
    }

    /// <summary>按 provinceCode 解析场景板块名；未注册时返回 null。</summary>
    public string ResolvePlateMapNameByProvinceCode(string provinceCode)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return null;
        }

        if (_provinceCodeToPlateMapName.TryGetValue(normalizedCode, out string plateMapName) &&
            !string.IsNullOrWhiteSpace(plateMapName))
        {
            return plateMapName;
        }

        return null;
    }

    /// <summary>注册点位写入回调；同一板块重复注册会覆盖上一次。</summary>
    public void RegisterSetVehiclePointsAction(string plateMapName, PlateMapSetVehiclePointsAction action)
    {
        GetOrCreateHandlers(plateMapName).SetVehiclePoints = action;

        // 板块晚于 HTTP 推送注册时，立即用 Hub 缓存覆盖 Inspector 默认值并刷新显示。
        if (TryGetCachedVehiclePoints(plateMapName, out VehicleMapPointData[] cached))
        {
            action.Invoke(CloneVehiclePointArray(cached), true);
        }
    }

    /// <summary>注销点位写入；对应板块无其他回调时移除字典项。</summary>
    public void UnregisterSetVehiclePointsAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.SetVehiclePoints = null);
    }

    /// <summary>注册读取当前点位回调。</summary>
    public void RegisterGetCurrentVehiclePointsAction(string plateMapName, PlateMapGetCurrentVehiclePointsAction action)
    {
        GetOrCreateHandlers(plateMapName).GetCurrentVehiclePoints = action;
    }

    /// <summary>注销读取当前点位回调。</summary>
    public void UnregisterGetCurrentVehiclePointsAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.GetCurrentVehiclePoints = null);
    }

    /// <summary>注册逐点过滤；未注册时 <see cref="InvokeShouldIncludePoint"/> 默认返回 true。</summary>
    public void RegisterShouldIncludePointAction(string plateMapName, PlateMapShouldIncludePointAction action)
    {
        GetOrCreateHandlers(plateMapName).ShouldIncludePoint = action;
    }

    /// <summary>注销逐点过滤。</summary>
    public void UnregisterShouldIncludePointAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.ShouldIncludePoint = null);
    }

    /// <summary>注册显示前批量变换。</summary>
    public void RegisterTransformPointsBeforeDisplayAction(
        string plateMapName,
        PlateMapTransformPointsBeforeDisplayAction action)
    {
        GetOrCreateHandlers(plateMapName).TransformBeforeDisplay = action;
    }

    /// <summary>注销显示前批量变换。</summary>
    public void UnregisterTransformPointsBeforeDisplayAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.TransformBeforeDisplay = null);
    }

    /// <summary>注册仅刷新 GPU 显示（不修改点位源数据）。</summary>
    public void RegisterRefreshVehiclePointsDisplayAction(
        string plateMapName,
        PlateMapRefreshVehiclePointsDisplayAction action)
    {
        GetOrCreateHandlers(plateMapName).RefreshVehiclePointsDisplay = action;
    }

    /// <summary>注销 GPU 显示刷新回调。</summary>
    public void UnregisterRefreshVehiclePointsDisplayAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.RefreshVehiclePointsDisplay = null);
    }

    /// <summary>板块是否已注册逐点过滤。</summary>
    public bool HasShouldIncludePointAction(string plateMapName)
    {
        return TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.ShouldIncludePoint != null;
    }

    /// <summary>板块是否已注册显示前批量变换。</summary>
    public bool HasTransformPointsBeforeDisplayAction(string plateMapName)
    {
        return TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.TransformBeforeDisplay != null;
    }

    /// <summary>
    /// 一次性注册地理转换相关四个回调（由 <see cref="PlateMapGeoConverter"/> 在 OnEnable 调用）。
    /// </summary>
    public void RegisterGeoConverterActions(
        string plateMapName,
        PlateMapGeoConverterRebuildAction rebuildAction,
        PlateMapIsGeoConverterReadyAction readyAction,
        PlateMapTryLonLatToLocalAction lonLatToLocalAction,
        PlateMapGetProvinceBoundsAction boundsAction)
    {
        PlateHandlers handlers = GetOrCreateHandlers(plateMapName);
        handlers.GeoConverterRebuild = rebuildAction;
        handlers.IsGeoConverterReady = readyAction;
        handlers.TryLonLatToLocal = lonLatToLocalAction;
        handlers.GetProvinceBounds = boundsAction;
    }

    /// <summary>注销地理转换四件套。</summary>
    public void UnregisterGeoConverterActions(string plateMapName)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            return;
        }

        handlers.GeoConverterRebuild = null;
        handlers.IsGeoConverterReady = null;
        handlers.TryLonLatToLocal = null;
        handlers.GetProvinceBounds = null;
        RemoveIfEmpty(plateMapName, handlers);
    }

    #endregion

    #region 对外发布（外部系统入口）

    public void SetPlateMapName(string plateMapName)
    {
        this.plateMapName = plateMapName;
        plateMapNameList.Add(plateMapName);
    }

    public void RemovePlateMapName(string plateMapName)
    {
        plateMapNameList.Remove(plateMapName);
    }

    /// <summary>
    /// 按板块名推送车辆点位（<see cref="PlateMapAPI.UpdateVehiclePointsFromJson"/> 的最终落点）。
    /// 板块隐藏（AllPlateMap SetActive false）时仍写入 Hub 缓存；显示层由 Controller 在重新启用时刷新。
    /// </summary>
    /// <summary>按 provinceCode 推送车辆点位到对应场景板块。</summary>
    public bool PublishSetVehiclePointsByProvinceCode(string provinceCode, VehicleMapPointData[] points, bool syncNow = true)
    {
        string plateMapName = ResolvePlateMapNameByProvinceCode(provinceCode);
        if (string.IsNullOrWhiteSpace(plateMapName))
        {
            Debug.LogWarning($"[PlateMapVehiclePointEvents] 未注册 provinceCode={provinceCode} 的场景板块。");
            return false;
        }

        return PublishSetVehiclePoints(plateMapName, points, syncNow);
    }

    /// <returns>是否已调用 Controller 的 Set 回调（未注册时仅写入缓存并返回 false）。</returns>
    public bool PublishSetVehiclePoints(string plateMapName, VehicleMapPointData[] points, bool syncNow = true)
    {
        string resolvedName = ResolvePlateMapNameForVehiclePoints(plateMapName);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return false;
        }

        VehicleMapPointData[] snapshot = CloneVehiclePointArray(points);
        SetCachedVehiclePoints(resolvedName, snapshot);
        RaiseVehiclePointsWillChange(resolvedName, snapshot);

        if (TryGetHandlers(resolvedName, out PlateHandlers handlers) && handlers.SetVehiclePoints != null)
        {
            handlers.SetVehiclePoints.Invoke(snapshot, syncNow);
            return true;
        }

        RaiseVehiclePointsChanged(resolvedName, snapshot);
        if (syncNow)
        {
            MarkPendingDisplayRefresh(resolvedName);
        }

        return false;
    }

    /// <summary>
    /// 解析可写入车辆点位的板块名：优先 preferred → Hub.plateMapName → 已注册列表 → 常见别名 → 任意已注册 Controller。
    /// </summary>
    public string ResolvePlateMapNameForVehiclePoints(string preferredName)
    {
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            string trimmed = preferredName.Trim();
            if (HasActiveSetVehiclePointsHandler(trimmed))
            {
                return trimmed;
            }
        }

        if (!string.IsNullOrWhiteSpace(plateMapName) && HasActiveSetVehiclePointsHandler(plateMapName))
        {
            return plateMapName;
        }

        for (int i = plateMapNameList.Count - 1; i >= 0; i--)
        {
            string name = plateMapNameList[i];
            if (!string.IsNullOrWhiteSpace(name) && HasActiveSetVehiclePointsHandler(name))
            {
                return name;
            }
        }

        if (TryResolvePlateMapNameFromFallbacks(out string fallbackName))
        {
            return fallbackName;
        }

        foreach (KeyValuePair<string, PlateHandlers> entry in _plates)
        {
            if (entry.Value.SetVehiclePoints != null)
            {
                return entry.Key;
            }
        }

        return string.IsNullOrWhiteSpace(preferredName) ? null : preferredName.Trim();
    }

    public bool HasActiveSetVehiclePointsHandler(string plateMapName)
    {
        return TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.SetVehiclePoints != null;
    }

    public static VehicleMapPointData[] CloneVehiclePointArray(VehicleMapPointData[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<VehicleMapPointData>();
        }

        VehicleMapPointData[] copy = new VehicleMapPointData[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static bool TryResolvePlateMapNameFromFallbacks(out string plateMapName)
    {
        PlateMapVehiclePointEvents hub = Instance;
        if (hub == null)
        {
            plateMapName = null;
            return false;
        }

        string[] fallbacks =
        {
            Instance.ResolvePlateMapNameByProvinceCode(HttpProjectConfig.DefaultProvinceCode),
            HttpProjectConfig.DefaultPlateMapName,
            "sd_map",
            "sd_map (1)",
        };

        for (int i = 0; i < fallbacks.Length; i++)
        {
            string candidate = fallbacks[i];
            if (!string.IsNullOrWhiteSpace(candidate) && hub.HasActiveSetVehiclePointsHandler(candidate))
            {
                plateMapName = candidate;
                return true;
            }
        }

        plateMapName = null;
        return false;
    }

    /// <summary>请求指定板块按当前过滤规则重建 GPU 显示（不修改源数据）。</summary>
    public bool RequestRefreshVehiclePointsDisplay(string plateMapName)
    {
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.RefreshVehiclePointsDisplay == null)
        {
            Debug.LogWarning($"[PlateMapVehiclePointEvents] 未注册板块「{plateMapName}」的 RefreshVehiclePointsDisplay。");
            return false;
        }

        handlers.RefreshVehiclePointsDisplay.Invoke();
        return true;
    }

    /// <summary>请求指定板块重建地理映射（无注册时静默忽略）。</summary>
    public void PublishGeoConverterRebuild(string plateMapName)
    {
        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.GeoConverterRebuild != null)
        {
            handlers.GeoConverterRebuild.Invoke();
        }
    }

    #endregion

    #region 触发与查询（模块内部由 Controller / Demo 调用）

    /// <summary>由 Controller 在覆盖点位前触发 <see cref="VehiclePointsWillChangeAction"/>。</summary>
    public void RaiseVehiclePointsWillChange(string plateMapName, VehicleMapPointData[] points)
    {
        VehiclePointsWillChangeAction?.Invoke(plateMapName, points);
    }

    /// <summary>由 Controller 在点位写入后触发 <see cref="VehiclePointsChangedAction"/>。</summary>
    public void RaiseVehiclePointsChanged(string plateMapName, VehicleMapPointData[] points)
    {
        VehiclePointsChangedAction?.Invoke(plateMapName, points);
    }

    /// <summary>由 Controller 在开始 GPU 重建前触发 <see cref="RebuildStartedAction"/>。</summary>
    public void RaiseRebuildStarted(string plateMapName)
    {
        RebuildStartedAction?.Invoke(plateMapName);
    }

    /// <summary>绘制合并前逐点询问是否保留；无过滤注册时默认保留。</summary>
    public bool InvokeShouldIncludePoint(string plateMapName, VehicleMapPointData data)
    {
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.ShouldIncludePoint == null)
        {
            return true;
        }

        return handlers.ShouldIncludePoint.Invoke(data);
    }

    /// <summary>合并显示前对点位数组做可选变换；无注册时返回 <paramref name="source"/>。</summary>
    public VehicleMapPointData[] InvokeTransformPointsBeforeDisplay(string plateMapName, VehicleMapPointData[] source)
    {
        if (source == null)
        {
            return null;
        }

        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.TransformBeforeDisplay != null)
        {
            return handlers.TransformBeforeDisplay.Invoke(source);
        }

        return source;
    }

    /// <summary>经纬度转地图局部坐标；失败时 <paramref name="localPosition"/> 为零向量。</summary>
    public bool InvokeTryLongitudeLatitudeToLocal(
        string plateMapName,
        double longitude,
        double latitude,
        out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.TryLonLatToLocal == null)
        {
            return false;
        }

        return handlers.TryLonLatToLocal.Invoke(longitude, latitude, out localPosition);
    }

    /// <summary>查询板块地理转换是否就绪。</summary>
    public bool InvokeIsGeoConverterReady(string plateMapName)
    {
        return TryGetHandlers(plateMapName, out PlateHandlers handlers) &&
               handlers.IsGeoConverterReady != null &&
               handlers.IsGeoConverterReady.Invoke();
    }

    /// <summary>获取板块经纬度外接矩形；失败时输出参数均为 0。</summary>
    public bool InvokeGetProvinceLongitudeLatitudeBounds(
        string plateMapName,
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = eastLongitude = southLatitude = northLatitude = 0;
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.GetProvinceBounds == null)
        {
            return false;
        }

        handlers.GetProvinceBounds.Invoke(out westLongitude, out eastLongitude, out southLatitude, out northLatitude);
        return true;
    }

    /// <summary>读取板块当前点位；未注册 Get 回调时回退到 Hub 缓存。</summary>
    public VehicleMapPointData[] InvokeGetCurrentVehiclePoints(string plateMapName)
    {
        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.GetCurrentVehiclePoints != null)
        {
            return handlers.GetCurrentVehiclePoints.Invoke();
        }

        return TryGetCachedVehiclePoints(plateMapName, out VehicleMapPointData[] cached) ? cached : null;
    }

    /// <summary>写入板块车辆点位缓存（板块隐藏时仍可接收 API 数据）。</summary>
    public void SetCachedVehiclePoints(string plateMapName, VehicleMapPointData[] points)
    {
        if (string.IsNullOrWhiteSpace(plateMapName))
        {
            return;
        }

        _vehiclePointsCache[plateMapName] = CloneVehiclePointArray(points);
    }

    /// <summary>Controller 首次注册时，若 Hub 尚无缓存则用 Inspector 初始数据填充。</summary>
    public void SeedCachedVehiclePointsIfEmpty(string plateMapName, VehicleMapPointData[] points)
    {
        if (string.IsNullOrWhiteSpace(plateMapName) || points == null || points.Length == 0)
        {
            return;
        }

        if (!_vehiclePointsCache.ContainsKey(plateMapName))
        {
            _vehiclePointsCache[plateMapName] = points;
        }
    }

    /// <summary>读取 Hub 缓存的车辆点位。</summary>
    public bool TryGetCachedVehiclePoints(string plateMapName, out VehicleMapPointData[] points)
    {
        points = null;
        if (string.IsNullOrWhiteSpace(plateMapName))
        {
            return false;
        }

        return _vehiclePointsCache.TryGetValue(plateMapName, out points) && points != null;
    }

    /// <summary>板块隐藏期间收到 syncNow 推送时标记，重新显示后需刷新 GPU。</summary>
    public void MarkPendingDisplayRefresh(string plateMapName)
    {
        if (!string.IsNullOrWhiteSpace(plateMapName))
        {
            _pendingDisplayRefresh.Add(plateMapName);
        }
    }

    /// <summary>是否存在待刷新 GPU 显示的板块。</summary>
    public bool ConsumePendingDisplayRefresh(string plateMapName)
    {
        if (string.IsNullOrWhiteSpace(plateMapName))
        {
            return false;
        }

        return _pendingDisplayRefresh.Remove(plateMapName);
    }

    #endregion

    /// <summary>按板块名获取或新建 <see cref="PlateHandlers"/> 条目。</summary>
    private PlateHandlers GetOrCreateHandlers(string plateMapName)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            handlers = new PlateHandlers();
            _plates[plateMapName] = handlers;
        }

        return handlers;
    }

    /// <summary>尝试获取已注册板块的回调集合。</summary>
    private bool TryGetHandlers(string plateMapName, out PlateHandlers handlers)
    {
        return _plates.TryGetValue(plateMapName, out handlers);
    }

    /// <summary>清空指定槽位并在板块条目全空时移除 key。</summary>
    private void ClearHandler(string plateMapName, Action<PlateHandlers> clear)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            return;
        }

        clear(handlers);
        RemoveIfEmpty(plateMapName, handlers);
    }

    /// <summary>板块全部回调注销后从字典删除，避免残留空条目。</summary>
    private void RemoveIfEmpty(string plateMapName, PlateHandlers handlers)
    {
        if (handlers.IsEmpty)
        {
            _plates.Remove(plateMapName);
        }
    }
}
