using System;
using UnityEngine;

/// <summary>
/// 基于 Left / Right 两对角控制点做经纬度 ↔ 局部 XZ 映射。
/// 经度按度数线性插值；纬度按 Web Mercator（与 Online Maps Spherical Mercator 同族）插值，
/// 使热力点与世界地图投影一致。不扫描子模型网格，不读省界 Database。
/// 约定：西锚点=西南角(LonW,LatW)，东锚点=东北角(LonE,LatE)，且与 marker 局部位姿对齐。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapGeoConverter : MonoBehaviour
{
    /// <summary>Web Mercator 纬度钳制（度），避免极点发散。</summary>
    private const double MercatorMaxLatitude = 85.05112878;

    /// <summary>控制点：局部位置 + 该点对应的经纬度。</summary>
    [Serializable]
    public struct GeoAnchor
    {
        public Transform marker;
        public double longitude;
        public double latitude;
    }

    [Header("板块标识")]
    [Tooltip("单元 code：国内=省级 adcode；国外=国家 secondClassCode（如 392）；全国/大板块根可用 \"0\" 或 firstClassCode。仅用于事件总线映射。")]
    [SerializeField] private string _provinceCode = "370000";

    [Header("地图根节点")]
    [SerializeField] private Transform _mapRoot;

    [Header("西/东 控制点（模型内 Left / Right）")]
    [SerializeField] private GeoAnchor _westAnchor;
    [SerializeField] private GeoAnchor _eastAnchor;

    [Tooltip("勾选后按局部 X 决定西/东（X 较大为西缘）；默认不勾选，沿用手动指定的西/东引用。")]
    [SerializeField] private bool _autoBindWestEastByLocalX;

    [Header("省级聚焦")]
    [Tooltip("本板块省级相机聚焦中心世界坐标偏移；叠加在模块 bounds.center 上。Play 下可调，点 Inspector 保存按钮可写回编辑态。")]
    [SerializeField] private Vector3 _focusCenterWorldOffset = Vector3.zero;

    private PlateMapVehiclePointController _vehiclePointController;
    private bool _isReady;
    private Vector3 _lastNotifiedFocusCenterWorldOffset;

    public bool IsReady => _isReady;
    public string ProvinceCode => _provinceCode;
    public GeoAnchor WestAnchor => _westAnchor;
    public GeoAnchor EastAnchor => _eastAnchor;

    /// <summary>省级聚焦中心世界坐标偏移（叠加在模块包围盒中心上）。</summary>
    public Vector3 FocusCenterWorldOffset => _focusCenterWorldOffset;

    /// <summary>强制写入聚焦中心世界坐标偏移（同步通知缓存，避免误触发瞬时刷新）。</summary>
    public bool ApplyFocusCenterWorldOffset(Vector3 offset)
    {
        _focusCenterWorldOffset = offset;
        _lastNotifiedFocusCenterWorldOffset = offset;
        return true;
    }

    /// <summary>外接经纬度范围（取自两锚点经纬度）。</summary>
    public void GetProvinceLongitudeLatitudeBounds(
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = Math.Min(_westAnchor.longitude, _eastAnchor.longitude);
        eastLongitude = Math.Max(_westAnchor.longitude, _eastAnchor.longitude);
        southLatitude = Math.Min(_westAnchor.latitude, _eastAnchor.latitude);
        northLatitude = Math.Max(_westAnchor.latitude, _eastAnchor.latitude);
    }

    private void Awake()
    {
        Rebuild();
        _lastNotifiedFocusCenterWorldOffset = _focusCenterWorldOffset;
    }

    private void OnEnable()
    {
        CacheVehiclePointController();
        RegisterToVehiclePointEvents();
    }

    private void OnDisable()
    {
        UnregisterFromVehiclePointEvents();
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

    private string PlateMapKey
    {
        get
        {
            CacheVehiclePointController();
            return _vehiclePointController != null ? _vehiclePointController.gameObject.name : gameObject.name;
        }
    }

    private void RegisterToVehiclePointEvents()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.RegisterGeoConverterActions(
            PlateMapKey,
            Rebuild,
            () => _isReady,
            TryLongitudeLatitudeToLocal,
            GetProvinceLongitudeLatitudeBounds);

        if (!string.IsNullOrWhiteSpace(_provinceCode))
        {
            hub.RegisterProvinceCodeMapping(_provinceCode, PlateMapKey);
        }
    }

    private void UnregisterFromVehiclePointEvents()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.UnregisterGeoConverterActions(PlateMapKey);

        if (!string.IsNullOrWhiteSpace(_provinceCode))
        {
            hub.UnregisterProvinceCodeMapping(_provinceCode, PlateMapKey);
        }
    }

    /// <summary>仅根据 Left/Right 锚点重建映射。</summary>
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
            // LogManager.LogFeatureWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」未指定 Left/Right 控制点。");
            return;
        }

        if (!IsMappingGeometryValid())
        {
            // LogManager.LogFeatureWarning(
            //     $"[PlateMapGeoConverter] 板块「{PlateMapKey}」锚点无效：西东 X 或经度重合，或两锚点纬度/Z 无法构成有效映射。");
            // return;
        }

        _isReady = true;

        // LogManager.LogFeature(
        //     $"[PlateMapGeoConverter] 映射就绪 | code={_provinceCode} | 板块「{PlateMapKey}」 | " +
        //     $"西 lon={_westAnchor.longitude:F6} lat={_westAnchor.latitude:F6} @ {_westAnchor.marker.localPosition} | " +
        //     $"东 lon={_eastAnchor.longitude:F6} lat={_eastAnchor.latitude:F6} @ {_eastAnchor.marker.localPosition}");

        RefreshVehiclePointsDisplayIfPlaying();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    private bool IsMappingGeometryValid()
    {
        float dx = _eastAnchor.marker.localPosition.x - _westAnchor.marker.localPosition.x;
        float dz = _eastAnchor.marker.localPosition.z - _westAnchor.marker.localPosition.z;
        double dLon = _eastAnchor.longitude - _westAnchor.longitude;
        double dMerc = LatitudeToMercatorY(_eastAnchor.latitude) - LatitudeToMercatorY(_westAnchor.latitude);

        return Math.Abs(dx) > 1e-6f &&
               Math.Abs(dz) > 1e-6f &&
               Math.Abs(dLon) > 1e-9 &&
               Math.Abs(dMerc) > 1e-15;
    }

    private void RefreshVehiclePointsDisplayIfPlaying()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PlateMapVehiclePointEvents.Instance?.RequestRefreshVehiclePointsDisplay(PlateMapKey);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Rebuild();
            _lastNotifiedFocusCenterWorldOffset = _focusCenterWorldOffset;
            return;
        }

#if UNITY_EDITOR
        if (_focusCenterWorldOffset == _lastNotifiedFocusCenterWorldOffset)
        {
            return;
        }

        _lastNotifiedFocusCenterWorldOffset = _focusCenterWorldOffset;
        PlateMapGeoConverter self = this;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (self == null)
            {
                return;
            }

            PlateMapDisplayController.Instance?.RefreshFocusedCameraImmediateIfOwnedBy(self);
        };
#endif
    }

    /// <summary>
    /// 经度：度数线性 → X；纬度：Mercator Y 线性 → Z（对角西南/东北锚点）。
    /// </summary>
    private Vector3 ComputeLocalPosition(double longitude, double latitude)
    {
        Vector3 westLocal = _westAnchor.marker.localPosition;
        Vector3 eastLocal = _eastAnchor.marker.localPosition;

        double tLon = InverseLerpDouble(_westAnchor.longitude, _eastAnchor.longitude, longitude);
        double mercSouth = LatitudeToMercatorY(_westAnchor.latitude);
        double mercNorth = LatitudeToMercatorY(_eastAnchor.latitude);
        double tLat = InverseLerpDouble(mercSouth, mercNorth, LatitudeToMercatorY(latitude));

        float x = (float)LerpDouble(westLocal.x, eastLocal.x, tLon);
        float z = (float)LerpDouble(westLocal.z, eastLocal.z, tLat);
        return new Vector3(x, 0f, z);
    }

    public bool TryLocalToLongitudeLatitude(Vector3 localPosition, out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;

        if (!_isReady)
        {
            return false;
        }

        Vector3 westLocal = _westAnchor.marker.localPosition;
        Vector3 eastLocal = _eastAnchor.marker.localPosition;

        double tLon = InverseLerpDouble(westLocal.x, eastLocal.x, localPosition.x);
        double tLat = InverseLerpDouble(westLocal.z, eastLocal.z, localPosition.z);
        longitude = LerpDouble(_westAnchor.longitude, _eastAnchor.longitude, tLon);

        double mercSouth = LatitudeToMercatorY(_westAnchor.latitude);
        double mercNorth = LatitudeToMercatorY(_eastAnchor.latitude);
        double mercY = LerpDouble(mercSouth, mercNorth, tLat);
        latitude = MercatorYToLatitude(mercY);
        return true;
    }

    /// <summary>WGS84 纬度（度）→ Web Mercator Y（无量纲，与 Spherical Mercator 同族）。</summary>
    private static double LatitudeToMercatorY(double latitudeDegrees)
    {
        double lat = Math.Max(-MercatorMaxLatitude, Math.Min(MercatorMaxLatitude, latitudeDegrees));
        double latRad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(Math.PI * 0.25 + latRad * 0.5));
    }

    /// <summary>Web Mercator Y → WGS84 纬度（度）。</summary>
    private static double MercatorYToLatitude(double mercatorY)
    {
        double latRad = 2.0 * Math.Atan(Math.Exp(mercatorY)) - Math.PI * 0.5;
        return latRad * 180.0 / Math.PI;
    }

    private static double InverseLerpDouble(double a, double b, double value)
    {
        double den = b - a;
        if (Math.Abs(den) < 1e-15)
        {
            return 0.0;
        }

        return (value - a) / den;
    }

    private static double LerpDouble(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

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

    public bool TryWorldToLongitudeLatitude(Vector3 worldPosition, out double longitude, out double latitude)
    {
        Vector3 local = _mapRoot.InverseTransformPoint(worldPosition);
        return TryLocalToLongitudeLatitude(local, out longitude, out latitude);
    }

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

    private void BindWestEastByLocalX()
    {
        Transform left = FindChildMarker("Left");
        Transform right = FindChildMarker("Right");
        if (left == null || right == null)
        {
            return;
        }

        // 本模型局部 X 向西增大：X 更大者为西缘
        bool leftIsWest = left.localPosition.x >= right.localPosition.x;
        Transform westMarker = leftIsWest ? left : right;
        Transform eastMarker = leftIsWest ? right : left;

        double lonA = _westAnchor.longitude;
        double lonB = _eastAnchor.longitude;
        double latA = _westAnchor.latitude;
        double latB = _eastAnchor.latitude;

        _westAnchor.marker = westMarker;
        _eastAnchor.marker = eastMarker;
        _westAnchor.longitude = Math.Min(lonA, lonB);
        _eastAnchor.longitude = Math.Max(lonA, lonB);
        // 纬度随原西/东序列化值与对应角点约定：较小 lat 给西侧字段、较大给东侧（对角西南/东北）
        _westAnchor.latitude = Math.Min(latA, latB);
        _eastAnchor.latitude = Math.Max(latA, latB);
    }

    private Transform FindChildMarker(string markerName)
    {
        if (_mapRoot == null)
        {
            return null;
        }

        Transform[] all = _mapRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == markerName)
            {
                return all[i];
            }
        }

        return null;
    }

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
}
