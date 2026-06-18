using System;
using UnityEngine;

/// <summary>
/// 基于 sd_map 上 Left（西）/ Right（东）两个控制点，在模型局部 XZ 平面做经纬度仿射映射。
/// X 轴对应经度，Z 轴对应纬度（南→北由 Inspector 或网格包围盒确定）。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapGeoConverter : MonoBehaviour
{
    /// <summary>地图控制点：模型子物体位置 + 对应的真实经纬度。</summary>
    [Serializable]
    public struct GeoAnchor
    {
        public Transform marker;
        public double longitude;
        public double latitude;
    }

    [Header("地图根节点（sd_map）")]
    [SerializeField] private Transform _mapRoot;

    [Header("西/东 控制点（模型内 Left / Right）")]
    [SerializeField] private GeoAnchor _westAnchor;
    [SerializeField] private GeoAnchor _eastAnchor;

    [Tooltip("按局部 X 自动绑定西/东标记（X 较大为西缘，避免模型朝向导致 Left/Right 名实不符）")]
    [SerializeField] private bool _autoBindWestEastByLocalX = true;

    [Tooltip("根据标记点局部 Z 反算并写回锚点纬度（仅用于校准显示，经度仍由 X 映射）")]
    [SerializeField] private bool _syncAnchorLatitudeFromMarkerZ = true;

    [Header("纬度映射（Z 轴）")]
    [Tooltip("勾选后根据地图网格包围盒自动取 Z 范围；否则使用下方手动 Z")]
    [SerializeField] private bool _autoLatitudeBoundsFromMesh = true;

    [SerializeField] private double _southLatitude = 34.377;
    [SerializeField] private double _northLatitude = 38.401;

    [SerializeField] private float _manualSouthLocalZ;
    [SerializeField] private float _manualNorthLocalZ;

    [Header("省界过滤")]
    [Tooltip("勾选后按山东省界 JSON 过滤显示；取消勾选则全量显示（不修改原始点位数据）")]
    [SerializeField] private bool _useProvinceBoundary;
    [SerializeField] private PlateMapShandongProvincePointFilter _provinceFilter = new PlateMapShandongProvincePointFilter();
    [Tooltip("仅在没有同物体 PlateMapVehiclePointController 时生效")]
    [SerializeField] private string _plateMapKeyOverride;

    private PlateMapVehiclePointController _vehiclePointController;

    // 纬度方向：模型局部 Z 南端/北端（由网格包围盒或手动指定）
    private float _southLocalZ;
    private float _northLocalZ;
    private bool _isReady;
    private bool _lastUseProvinceBoundary;

    public bool IsReady => _isReady;
    public GeoAnchor WestAnchor => _westAnchor;
    public GeoAnchor EastAnchor => _eastAnchor;
    public double SouthLatitude => _southLatitude;
    public double NorthLatitude => _northLatitude;
    public bool UseProvinceBoundary => _useProvinceBoundary;

    /// <summary>运行时动态开关省界过滤，并立即刷新当前板块显示。</summary>
    public void SetUseProvinceBoundary(bool useProvinceBoundary)
    {
        if (_useProvinceBoundary == useProvinceBoundary)
        {
            return;
        }

        _useProvinceBoundary = useProvinceBoundary;
        _lastUseProvinceBoundary = useProvinceBoundary;
        ApplyProvinceBoundaryFilterState();
    }

    /// <summary>获取山东省映射用的经纬度外接矩形（西经、东经、南纬、北纬）。</summary>
    public void GetProvinceLongitudeLatitudeBounds(out double westLongitude, out double eastLongitude, out double southLatitude, out double northLatitude)
    {
        westLongitude = Math.Min(_westAnchor.longitude, _eastAnchor.longitude);
        eastLongitude = Math.Max(_westAnchor.longitude, _eastAnchor.longitude);
        southLatitude = Math.Min(_southLatitude, _northLatitude);
        northLatitude = Math.Max(_southLatitude, _northLatitude);
    }

    private void Awake()
    {
        TryAutoAssignProvinceBoundaryJson();
        Rebuild();
    }

    private void OnEnable()
    {
        _lastUseProvinceBoundary = _useProvinceBoundary;
        CacheVehiclePointController();
        RegisterToVehiclePointEvents();
        PlateMapVehiclePointEvents.Instance.VehiclePointsChangedAction += OnVehiclePointsChanged;

        // Start 不会在禁用后再启用时重跑，省界过滤须在 OnEnable 重新注册
        if (Application.isPlaying)
        {
            ApplyProvinceBoundaryFilterState();
        }
    }

    private void Start()
    {
        // 首次 Play 时 OnEnable 已注册；保留 Start 以覆盖仅 Awake 后 Hub 尚未就绪的边界情况
        if (!_useProvinceBoundary)
        {
            return;
        }

        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub != null && !hub.HasShouldIncludePointAction(PlateMapKey))
        {
            ApplyProvinceBoundaryFilterState();
        }
    }

    private void OnDisable()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub != null)
        {
            hub.VehiclePointsChangedAction -= OnVehiclePointsChanged;
        }

        UnregisterProvinceBoundaryFilterIfNeeded();
        UnregisterFromVehiclePointEvents();
    }

    private void OnVehiclePointsChanged(string plateMapName, VehicleMapPointData[] points)
    {
        if (plateMapName != PlateMapKey)
        {
            return;
        }

        if (!_useProvinceBoundary)
        {
            return;
        }

        PlateMapVehiclePointEvents.Instance?.RequestRefreshVehiclePointsDisplay(PlateMapKey);
    }

    private void CacheVehiclePointController()
    {
        if (_vehiclePointController != null)
        {
            return;
        }

        _vehiclePointController = GetComponent<PlateMapVehiclePointController>();
        if (_vehiclePointController != null)
        {
            return;
        }

        if (_mapRoot != null)
        {
            _vehiclePointController = _mapRoot.GetComponentInChildren<PlateMapVehiclePointController>(true);
        }
    }

    /// <summary>与 PlateMapVehiclePointController 注册的板块名保持一致（以其 GameObject 名为准）。</summary>
    private string PlateMapKey
    {
        get
        {
            CacheVehiclePointController();
            if (_vehiclePointController != null)
            {
                string controllerKey = _vehiclePointController.gameObject.name;
                if (!string.IsNullOrWhiteSpace(_plateMapKeyOverride) &&
                    _plateMapKeyOverride.Trim() != controllerKey)
                {
                    Debug.LogWarning(
                        $"[PlateMapGeoConverter] PlateMapKeyOverride「{_plateMapKeyOverride}」与 Controller 物体名「{controllerKey}」不一致，已使用 Controller 名称。");
                }

                return controllerKey;
            }

            return string.IsNullOrWhiteSpace(_plateMapKeyOverride) ? gameObject.name : _plateMapKeyOverride.Trim();
        }
    }

    private VehicleMapPointData[] TryGetCurrentVehiclePoints(PlateMapVehiclePointEvents hub)
    {
        VehicleMapPointData[] fromHub = hub.InvokeGetCurrentVehiclePoints(PlateMapKey);
        if (fromHub != null)
        {
            return fromHub;
        }

        CacheVehiclePointController();
        return _vehiclePointController != null ? _vehiclePointController.VehiclePoints : null;
    }

    private void Update()
    {
        if (!Application.isPlaying || _useProvinceBoundary == _lastUseProvinceBoundary)
        {
            return;
        }

        _lastUseProvinceBoundary = _useProvinceBoundary;
        ApplyProvinceBoundaryFilterState();
    }

    private void RegisterToVehiclePointEvents()
    {
        PlateMapVehiclePointEvents.Instance.RegisterGeoConverterActions(
            PlateMapKey,
            Rebuild,
            () => _isReady,
            TryLongitudeLatitudeToLocal,
            GetProvinceLongitudeLatitudeBounds);
    }

    private void UnregisterFromVehiclePointEvents()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.UnregisterGeoConverterActions(PlateMapKey);
    }

    private void RegisterProvinceBoundaryFilterIfNeeded()
    {
        if (!_useProvinceBoundary)
        {
            return;
        }

        if (!_provinceFilter.EnsureProvinceBoundaryLoaded())
        {
            Debug.LogWarning(
                $"[PlateMapGeoConverter] 板块「{PlateMapKey}」已开启省界过滤，但 ShandongBoundary.json 未配置或未加载。");
            return;
        }

        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        hub.RegisterShouldIncludePointAction(PlateMapKey, ShouldIncludePointForDisplay);
    }

    private void UnregisterProvinceBoundaryFilterIfNeeded()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.UnregisterShouldIncludePointAction(PlateMapKey);
    }

    private bool ShouldIncludePointForDisplay(VehicleMapPointData data)
    {
        if (!_useProvinceBoundary)
        {
            return true;
        }

        return _provinceFilter.ContainsInProvince(data.longitude, data.latitude);
    }

    private void ApplyProvinceBoundaryFilterState()
    {
        UnregisterProvinceBoundaryFilterIfNeeded();
        RegisterProvinceBoundaryFilterIfNeeded();

        if (!Application.isPlaying)
        {
            return;
        }

        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        LogProvinceBoundaryFilterStats(hub);

        if (!hub.RequestRefreshVehiclePointsDisplay(PlateMapKey))
        {
            Debug.LogWarning(
                $"[PlateMapGeoConverter] 省界过滤已{(_useProvinceBoundary ? "开启" : "关闭")}，" +
                $"但板块「{PlateMapKey}」未找到 Controller 显示刷新回调（请确认 GameObject 名与 Controller 一致）。");
        }
    }

    private void TryAutoAssignProvinceBoundaryJson()
    {
        _provinceFilter.TryAutoAssignBoundaryJsonAsset();
    }

    private void LogProvinceBoundaryFilterStats(PlateMapVehiclePointEvents hub)
    {
        VehicleMapPointData[] current = TryGetCurrentVehiclePoints(hub);
        int total = current?.Length ?? 0;
        bool hasGetCallback = hub.InvokeGetCurrentVehiclePoints(PlateMapKey) != null;
        CacheVehiclePointController();
        int controllerDirectCount = _vehiclePointController?.VehiclePoints?.Length ?? 0;

        if (!_useProvinceBoundary)
        {
            Debug.Log(
                $"[PlateMapGeoConverter] 省界过滤已关闭 | 板块「{PlateMapKey}」显示 {total}/{total} 点 | " +
                $"Controller直连={controllerDirectCount}");
            return;
        }

        if (!_provinceFilter.EnsureProvinceBoundaryLoaded())
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 省界过滤开启失败：JSON 未加载 | 板块「{PlateMapKey}」。");
            return;
        }

        int kept = 0;
        if (current != null)
        {
            for (int i = 0; i < current.Length; i++)
            {
                VehicleMapPointData point = current[i];
                if (_provinceFilter.ContainsInProvince(point.longitude, point.latitude))
                {
                    kept++;
                }
            }
        }

        bool hasFilterHandler = hub.HasShouldIncludePointAction(PlateMapKey);
        Debug.Log(
            $"[PlateMapGeoConverter] 省界过滤已开启 | 板块「{PlateMapKey}」省内 {kept}/{total} 点 | " +
            $"过滤回调注册={hasFilterHandler} | Controller直连={controllerDirectCount} | Get回调有效={hasGetCallback}");
    }

    /// <summary>
    /// 根据 Left/Right 与网格范围重建映射参数。
    /// 经度仅由西/东锚点的 X 与经纬度线性插值；纬度由全省南/北纬度与局部 Z 范围线性插值。
    /// </summary>
    [ContextMenu("重建地理映射")]
    public void Rebuild()
    {
        _isReady = false;

        if (_mapRoot == null)
        {
            _mapRoot = transform;
        }

        TryFindMarkersByName();
        if (_autoBindWestEastByLocalX)
        {
            BindWestEastByLocalX();
        }

        if (_westAnchor.marker == null || _eastAnchor.marker == null)
        {
            Debug.LogWarning("[PlateMapGeoConverter] 未找到 Left/Right 控制点。");
            return;
        }

        EnsureDefaultLongitudeLatitude();

        if (Math.Abs(_eastAnchor.marker.localPosition.x - _westAnchor.marker.localPosition.x) < 1e-6f)
        {
            Debug.LogWarning("[PlateMapGeoConverter] 西/东控制点局部 X 过近，无法映射经度。");
            return;
        }

        if (_autoLatitudeBoundsFromMesh)
        {
            if (TryComputeMeshLocalBounds(out _, out _, out float meshMinZ, out float meshMaxZ))
            {
                AssignSouthNorthLocalZFromMeshExtents(meshMinZ, meshMaxZ);
            }
            else
            {
                _southLocalZ = _manualSouthLocalZ;
                _northLocalZ = _manualNorthLocalZ;
            }
        }
        else
        {
            _southLocalZ = _manualSouthLocalZ;
            _northLocalZ = _manualNorthLocalZ;
        }

        if (Math.Abs(_northLocalZ - _southLocalZ) < 1e-6f)
        {
            Debug.LogWarning("[PlateMapGeoConverter] 纬度方向局部 Z 范围无效。");
            return;
        }

        if (_syncAnchorLatitudeFromMarkerZ)
        {
            SyncAnchorLatitudeFromMarkerZ(ref _westAnchor);
            SyncAnchorLatitudeFromMarkerZ(ref _eastAnchor);
        }

        _isReady = true;

        Vector3 localNorth = _mapRoot.InverseTransformDirection(Vector3.forward);
        Debug.Log(
            $"[PlateMapGeoConverter] 映射就绪 | 西({_westAnchor.marker.name}) lon={_westAnchor.longitude:F6} lat={_westAnchor.latitude:F6} | " +
            $"东({_eastAnchor.marker.name}) lon={_eastAnchor.longitude:F6} lat={_eastAnchor.latitude:F6} | " +
            $"Z 南[{_southLocalZ:F4}] 北[{_northLocalZ:F4}] <- 纬度 [{_southLatitude:F4},{_northLatitude:F4}] | " +
            $"局部Z朝向北分量={localNorth.z:F4}（世界Z+为北）");

        RefreshVehiclePointsDisplayIfPlaying();
    }

    private void RefreshVehiclePointsDisplayIfPlaying()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PlateMapVehiclePointEvents.Instance?.RequestRefreshVehiclePointsDisplay(PlateMapKey);
    }

    // 编辑器修改 Inspector 时触发
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Rebuild();
            return;
        }

        if (_useProvinceBoundary != _lastUseProvinceBoundary)
        {
            _lastUseProvinceBoundary = _useProvinceBoundary;
            ApplyProvinceBoundaryFilterState();
        }
    }

    /// <summary>经纬度线性映射到 sd_map 局部坐标。</summary>
    private Vector3 ComputeLocalPosition(double longitude, double latitude)
    {
        float westX = _westAnchor.marker.localPosition.x;
        float eastX = _eastAnchor.marker.localPosition.x;
        float tLon = Mathf.InverseLerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, (float)longitude);
        float x = Mathf.Lerp(westX, eastX, tLon);

        float tLat = Mathf.InverseLerp((float)_southLatitude, (float)_northLatitude, (float)latitude);
        float z = Mathf.Lerp(_southLocalZ, _northLocalZ, tLat);
        return new Vector3(x, 0f, z);
    }

    /// <summary>模型局部坐标 → 经纬度。</summary>
    public bool TryLocalToLongitudeLatitude(Vector3 localPosition, out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;

        if (!_isReady)
        {
            return false;
        }

        float westX = _westAnchor.marker.localPosition.x;
        float eastX = _eastAnchor.marker.localPosition.x;
        float tLon = Mathf.InverseLerp(westX, eastX, localPosition.x);
        longitude = Mathf.Lerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, tLon);

        float tLat = Mathf.InverseLerp(_southLocalZ, _northLocalZ, localPosition.z);
        latitude = Mathf.Lerp((float)_southLatitude, (float)_northLatitude, tLat);
        return true;
    }

    /// <summary>经纬度 → 模型局部坐标（Y 取 0，由调用方再贴地）。</summary>
    public bool TryLongitudeLatitudeToLocal(double longitude, double latitude, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;

        if (!_isReady)
        {
            return false;
        }

        localPosition = ComputeLocalPosition(longitude, latitude);
        return true;
    }

    /// <summary>世界坐标先转到 sd_map 局部，再求经纬度（用于点击拾取等）。</summary>
    public bool TryWorldToLongitudeLatitude(Vector3 worldPosition, out double longitude, out double latitude)
    {
        Vector3 local = _mapRoot.InverseTransformPoint(worldPosition);
        return TryLocalToLongitudeLatitude(local, out longitude, out latitude);
    }

    /// <summary>经纬度先转局部坐标，再 TransformPoint 到世界空间。</summary>
    public bool TryLongitudeLatitudeToWorld(double longitude, double latitude, out Vector3 worldPosition)
    {
        if (!TryLongitudeLatitudeToLocal(longitude, latitude, out Vector3 local))
        {
            worldPosition = Vector3.zero;
            return false;
        }

        worldPosition = _mapRoot.TransformPoint(local);
        return true;
    }

    /// <summary>校正西/东锚点经度默认值与大小关系（山东省参考经度范围）。</summary>
    private void EnsureDefaultLongitudeLatitude()
    {
        // 山东省西缘 / 东缘参考值（WGS84）；若 Inspector 填反或为空则纠正
        const double defaultWestLon = 114.819;
        const double defaultEastLon = 122.714;

        if (_westAnchor.longitude > _eastAnchor.longitude)
        {
            double tmp = _westAnchor.longitude;
            _westAnchor.longitude = _eastAnchor.longitude;
            _eastAnchor.longitude = tmp;
        }

        if (_westAnchor.longitude < 70 || _westAnchor.longitude > 140)
        {
            _westAnchor.longitude = defaultWestLon;
        }

        if (_eastAnchor.longitude < 70 || _eastAnchor.longitude > 140)
        {
            _eastAnchor.longitude = defaultEastLon;
        }

        if (Math.Abs(_westAnchor.longitude - _eastAnchor.longitude) < 0.01)
        {
            _westAnchor.longitude = defaultWestLon;
            _eastAnchor.longitude = defaultEastLon;
        }
    }

    /// <summary>按局部 X 较大者为西缘绑定 Left/Right，避免模型朝向与命名不一致。</summary>
    private void BindWestEastByLocalX()
    {
        Transform left = FindChildMarker("Left");
        Transform right = FindChildMarker("Right");
        if (left == null || right == null)
        {
            return;
        }

        double westLon = Math.Min(_westAnchor.longitude, _eastAnchor.longitude);
        double eastLon = Math.Max(_westAnchor.longitude, _eastAnchor.longitude);
        if (westLon < 70)
        {
            westLon = 114.819;
        }

        if (eastLon < 70)
        {
            eastLon = 122.714;
        }

        // 本模型局部 X 向西增大：X 较大者为西缘控制点
        Transform westMarker = left.localPosition.x >= right.localPosition.x ? left : right;
        Transform eastMarker = westMarker == left ? right : left;

        _westAnchor.marker = westMarker;
        _eastAnchor.marker = eastMarker;
        _westAnchor.longitude = westLon;
        _eastAnchor.longitude = eastLon;
    }

    /// <summary>根据锚点局部 Z 反算纬度并写回，用于 Inspector 校准显示。</summary>
    private void SyncAnchorLatitudeFromMarkerZ(ref GeoAnchor anchor)
    {
        if (anchor.marker == null)
        {
            return;
        }

        float tLat = Mathf.InverseLerp(_southLocalZ, _northLocalZ, anchor.marker.localPosition.z);
        anchor.latitude = Mathf.Lerp((float)_southLatitude, (float)_northLatitude, tLat);
    }

    /// <summary>在地图根下按名称查找子 Transform（含未激活对象）。</summary>
    private Transform FindChildMarker(string markerName)
    {
        if (_mapRoot == null)
        {
            return null;
        }

        Transform[] all = _mapRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == markerName)
            {
                return all[i];
            }
        }

        return null;
    }

    /// <summary>未手动指定时按子节点名 Left/Right 查找控制点。</summary>
    private void TryFindMarkersByName()
    {
        if (_westAnchor.marker == null)
        {
            _westAnchor.marker = FindChildMarker("Left");
        }

        if (_eastAnchor.marker == null)
        {
            _eastAnchor.marker = FindChildMarker("Right");
        }
    }

    /// <summary>合并地图网格在 sd_map 局部空间的 XZ 包围盒。</summary>
    private bool TryComputeMeshLocalBounds(
        out float minX,
        out float maxX,
        out float minZ,
        out float maxZ)
    {
        minX = maxX = minZ = maxZ = 0f;
        bool has = false;
        float boundsMinX = float.MaxValue;
        float boundsMaxX = float.MinValue;
        float boundsMinZ = float.MaxValue;
        float boundsMaxZ = float.MinValue;

        Renderer[] renderers = _mapRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            if (r.transform == _westAnchor.marker || r.transform == _eastAnchor.marker)
            {
                continue;
            }

            if (_westAnchor.marker != null &&
                (r.transform.IsChildOf(_westAnchor.marker) || r.transform == _westAnchor.marker))
            {
                continue;
            }

            if (_eastAnchor.marker != null &&
                (r.transform.IsChildOf(_eastAnchor.marker) || r.transform == _eastAnchor.marker))
            {
                continue;
            }

            Bounds b = r.bounds;
            Vector3 cLocal = _mapRoot.InverseTransformPoint(b.center);
            Vector3 eLocal = _mapRoot.InverseTransformVector(b.extents);
            float x0 = cLocal.x - Mathf.Abs(eLocal.x);
            float x1 = cLocal.x + Mathf.Abs(eLocal.x);
            float z0 = cLocal.z - Mathf.Abs(eLocal.z);
            float z1 = cLocal.z + Mathf.Abs(eLocal.z);
            boundsMinX = Mathf.Min(boundsMinX, x0, x1);
            boundsMaxX = Mathf.Max(boundsMaxX, x0, x1);
            boundsMinZ = Mathf.Min(boundsMinZ, z0, z1);
            boundsMaxZ = Mathf.Max(boundsMaxZ, z0, z1);
            has = true;
        }

        if (!has)
        {
            return false;
        }

        minX = boundsMinX;
        maxX = boundsMaxX;
        minZ = boundsMinZ;
        maxZ = boundsMaxZ;
        return true;
    }

    /// <summary>
    /// 按世界坐标北向（Z+）确定 mesh 包围盒哪一端是南/北缘。
    /// sd_map 根节点常带 Z180°，不能假设局部 minZ 即为南端。
    /// </summary>
    private void AssignSouthNorthLocalZFromMeshExtents(float meshMinZ, float meshMaxZ)
    {
        Vector3 localNorth = _mapRoot.InverseTransformDirection(Vector3.forward);
        if (localNorth.z >= 0f)
        {
            _southLocalZ = meshMinZ;
            _northLocalZ = meshMaxZ;
            return;
        }

        _southLocalZ = meshMaxZ;
        _northLocalZ = meshMinZ;
    }

    /// <summary>遍历地图网格 Renderer，在 sd_map 局部空间合并 Z 包围盒，作为纬度映射范围。</summary>
    private bool TryComputeMeshLocalZBounds(out float southZ, out float northZ)
    {
        if (TryComputeMeshLocalBounds(out float minX, out float maxX, out float minZ, out float maxZ))
        {
            AssignSouthNorthLocalZFromMeshExtents(minZ, maxZ);
            southZ = _southLocalZ;
            northZ = _northLocalZ;
            return true;
        }

        southZ = northZ = 0f;
        return false;
    }
}
