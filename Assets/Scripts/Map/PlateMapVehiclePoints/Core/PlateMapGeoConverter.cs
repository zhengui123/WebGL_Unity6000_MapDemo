using System;
using UnityEngine;

/// <summary>
/// 基于板块模型 Left（西）/ Right（东）控制点，在局部 XZ 平面做经纬度仿射映射。
/// 边界经纬度从 <see cref="PlateMapBoundaryDatabase"/> 按 <see cref="_provinceCode"/> 加载。
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

    [Header("板块标识")]
    [Tooltip("省级 adcode 字符串；0 表示全国整体大板块。与 PlateMapBoundaries.json 对应。")]
    [SerializeField] private string _provinceCode = "370000";

    [Header("地图根节点")]
    [SerializeField] private Transform _mapRoot;

    [Header("西/东 控制点（模型内 Left / Right）")]
    [SerializeField] private GeoAnchor _westAnchor;
    [SerializeField] private GeoAnchor _eastAnchor;

    [Tooltip("按局部 X 自动绑定西/东标记（X 较大为西缘）")]
    [SerializeField] private bool _autoBindWestEastByLocalX = true;

    private PlateMapVehiclePointController _vehiclePointController;

    private double _southLatitude;
    private double _northLatitude;
    private float _southLocalZ;
    private float _northLocalZ;
    private bool _isReady;

    public bool IsReady => _isReady;
    public string ProvinceCode => _provinceCode;
    public GeoAnchor WestAnchor => _westAnchor;
    public GeoAnchor EastAnchor => _eastAnchor;
    public double SouthLatitude => _southLatitude;
    public double NorthLatitude => _northLatitude;

    /// <summary>获取板块映射用的经纬度外接矩形（西、东、南、北）。</summary>
    public void GetProvinceLongitudeLatitudeBounds(
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = Math.Min(_westAnchor.longitude, _eastAnchor.longitude);
        eastLongitude = Math.Max(_westAnchor.longitude, _eastAnchor.longitude);
        southLatitude = Math.Min(_southLatitude, _northLatitude);
        northLatitude = Math.Max(_southLatitude, _northLatitude);
    }

    private void Awake()
    {
        Rebuild();
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

    /// <summary>根据 Left/Right 与网格 Z 范围重建映射；经纬度边界来自 Database。</summary>
    [ContextMenu("重建地理映射")]
    public void Rebuild()
    {
        _isReady = false;

        if (_mapRoot == null)
        {
            _mapRoot = transform;
        }

        if (!TryApplyBoundaryFromDatabase())
        {
            return;
        }

        TryFindMarkersByName();

        if (!TryComputeMeshLocalBounds(out float meshMinX, out float meshMaxX, out float meshMinZ, out float meshMaxZ))
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」无法从网格计算 XZ 范围。");
            return;
        }

        AssignSouthNorthLocalZFromMeshExtents(meshMinZ, meshMaxZ);

        if (Math.Abs(_northLocalZ - _southLocalZ) < 1e-6f)
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」纬度方向局部 Z 范围无效。");
            return;
        }

        EnsureWestEastMarkers(meshMinX, meshMaxX);

        if (_autoBindWestEastByLocalX)
        {
            BindWestEastByLocalX();
        }

        if (_westAnchor.marker == null || _eastAnchor.marker == null)
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」未找到 Left/Right 控制点。");
            return;
        }

        if (Math.Abs(_eastAnchor.marker.localPosition.x - _westAnchor.marker.localPosition.x) < 1e-6f)
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」西/东控制点局部 X 过近。");
            return;
        }

        _westAnchor.latitude = _southLatitude;
        _eastAnchor.latitude = _northLatitude;
        _isReady = true;

        Vector3 localNorth = _mapRoot.InverseTransformDirection(Vector3.forward);
        Debug.Log(
            $"[PlateMapGeoConverter] 映射就绪 | code={_provinceCode} | 板块「{PlateMapKey}」 | " +
            $"西 lon={_westAnchor.longitude:F6} lat={_westAnchor.latitude:F6} | " +
            $"东 lon={_eastAnchor.longitude:F6} lat={_eastAnchor.latitude:F6} | " +
            $"Z 南[{_southLocalZ:F4}] 北[{_northLocalZ:F4}] <- 纬度 [{_southLatitude:F4},{_northLatitude:F4}] | " +
            $"局部Z朝向北分量={localNorth.z:F4}");

        RefreshVehiclePointsDisplayIfPlaying();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Marker 为空时，在板块 mesh 外接矩形左下（西/南）与右上（东/北）创建 Left / Right 子物体。
    /// 本模型局部 X 向西增大：西缘取较大 X，东缘取较小 X。
    /// </summary>
    private void EnsureWestEastMarkers(float meshMinX, float meshMaxX)
    {
        float westLocalX = Mathf.Max(meshMinX, meshMaxX);
        float eastLocalX = Mathf.Min(meshMinX, meshMaxX);

        if (_westAnchor.marker == null)
        {
            _westAnchor.marker = CreateMarkerChild("Left", new Vector3(westLocalX, 0f, _southLocalZ));
        }

        if (_eastAnchor.marker == null)
        {
            _eastAnchor.marker = CreateMarkerChild("Right", new Vector3(eastLocalX, 0f, _northLocalZ));
        }
    }

    private Transform CreateMarkerChild(string markerName, Vector3 localPosition)
    {
        var markerObject = new GameObject(markerName);
        markerObject.transform.SetParent(_mapRoot, false);
        markerObject.transform.localPosition = localPosition;
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(markerObject, $"Create {markerName}");
            UnityEditor.EditorUtility.SetDirty(_mapRoot.gameObject);
        }
#endif

        return markerObject.transform;
    }

    private bool TryApplyBoundaryFromDatabase()
    {
        if (string.IsNullOrWhiteSpace(_provinceCode))
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 板块「{PlateMapKey}」未配置 provinceCode。");
            return false;
        }

        if (!PlateMapBoundaryDatabase.TryGet(_provinceCode, out PlateMapBoundaryData boundary))
        {
            Debug.LogWarning($"[PlateMapGeoConverter] 未找到 provinceCode={_provinceCode} 的边界数据。");
            return false;
        }

        _provinceCode = boundary.provinceCode;
        _westAnchor.longitude = boundary.westLongitude;
        _eastAnchor.longitude = boundary.eastLongitude;
        _southLatitude = boundary.southLatitude;
        _northLatitude = boundary.northLatitude;
        return true;
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
        }
    }

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

        Transform westMarker = left.localPosition.x >= right.localPosition.x ? left : right;
        Transform eastMarker = westMarker == left ? right : left;

        _westAnchor.marker = westMarker;
        _eastAnchor.marker = eastMarker;
        _westAnchor.longitude = Math.Min(_westAnchor.longitude, _eastAnchor.longitude);
        _eastAnchor.longitude = Math.Max(_westAnchor.longitude, _eastAnchor.longitude);
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
            if (all[i].name == markerName)
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
}
