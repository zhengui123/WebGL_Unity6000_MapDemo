using System;
using UnityEngine;

/// <summary>
/// 基于 Left / Right 两控制点做经纬度 ↔ 局部 XZ 仿射映射。
/// 映射只依赖两锚点的 Transform（局部坐标）及其 longitude/latitude，不扫描子模型网格，不读省界 Database。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapGeoConverter : MonoBehaviour
{
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
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」未指定 Left/Right 控制点。");
            return;
        }

        if (!IsMappingGeometryValid())
        {
            Debug.LogWarning(
                $"[PlateMapGeoConverter] 板块「{PlateMapKey}」锚点无效：西东 X 或经度重合，或两锚点纬度/Z 无法构成有效映射。");
            return;
        }

        _isReady = true;

        Debug.Log(
            $"[PlateMapGeoConverter] 映射就绪 | code={_provinceCode} | 板块「{PlateMapKey}」 | " +
            $"西 lon={_westAnchor.longitude:F6} lat={_westAnchor.latitude:F6} @ {_westAnchor.marker.localPosition} | " +
            $"东 lon={_eastAnchor.longitude:F6} lat={_eastAnchor.latitude:F6} @ {_eastAnchor.marker.localPosition}");

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
        double dLat = _eastAnchor.latitude - _westAnchor.latitude;

        return Math.Abs(dx) > 1e-6f &&
               Math.Abs(dz) > 1e-6f &&
               Math.Abs(dLon) > 1e-9 &&
               Math.Abs(dLat) > 1e-9;
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
    /// 仿射：经度沿西→东锚点插值 X，纬度沿西→东锚点插值 Z。
    /// （约定两锚点为对角控制点：携带各自经度与纬度。）
    /// </summary>
    private Vector3 ComputeLocalPosition(double longitude, double latitude)
    {
        Vector3 westLocal = _westAnchor.marker.localPosition;
        Vector3 eastLocal = _eastAnchor.marker.localPosition;

        float tLon = Mathf.InverseLerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, (float)longitude);
        float tLat = Mathf.InverseLerp((float)_westAnchor.latitude, (float)_eastAnchor.latitude, (float)latitude);

        float x = Mathf.Lerp(westLocal.x, eastLocal.x, tLon);
        float z = Mathf.Lerp(westLocal.z, eastLocal.z, tLat);
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

        float tLon = Mathf.InverseLerp(westLocal.x, eastLocal.x, localPosition.x);
        float tLat = Mathf.InverseLerp(westLocal.z, eastLocal.z, localPosition.z);
        longitude = Mathf.Lerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, tLon);
        latitude = Mathf.Lerp((float)_westAnchor.latitude, (float)_eastAnchor.latitude, tLat);
        return true;
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
