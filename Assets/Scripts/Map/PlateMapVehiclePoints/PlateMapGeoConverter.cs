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

    // 纬度方向：模型局部 Z 南端/北端（由网格包围盒或手动指定）
    private float _southLocalZ;
    private float _northLocalZ;
    private bool _isReady;

    public bool IsReady => _isReady;
    public GeoAnchor WestAnchor => _westAnchor;
    public GeoAnchor EastAnchor => _eastAnchor;
    public double SouthLatitude => _southLatitude;
    public double NorthLatitude => _northLatitude;

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
        Rebuild();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Rebuild();
        }
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
            if (!TryComputeMeshLocalZBounds(out _southLocalZ, out _northLocalZ))
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

        Debug.Log(
            $"[PlateMapGeoConverter] 映射就绪 | 西({ _westAnchor.marker.name}) lon={_westAnchor.longitude:F6} lat={_westAnchor.latitude:F6} | " +
            $"东({ _eastAnchor.marker.name}) lon={_eastAnchor.longitude:F6} lat={_eastAnchor.latitude:F6} | " +
            $"Z [{_southLocalZ:F4},{_northLocalZ:F4}] -> 纬度 [{_southLatitude:F4},{_northLatitude:F4}]");
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

        // 经度：局部 X 在西/东锚点 X 之间插值
        float westX = _westAnchor.marker.localPosition.x;
        float eastX = _eastAnchor.marker.localPosition.x;
        float tLon = Mathf.InverseLerp(westX, eastX, localPosition.x);
        longitude = Mathf.Lerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, tLon);

        // 纬度：局部 Z 在南/北 Z 之间插值
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

        float westX = _westAnchor.marker.localPosition.x;
        float eastX = _eastAnchor.marker.localPosition.x;
        float tLon = Mathf.InverseLerp((float)_westAnchor.longitude, (float)_eastAnchor.longitude, (float)longitude);
        float x = Mathf.Lerp(westX, eastX, tLon);

        float tLat = Mathf.InverseLerp((float)_southLatitude, (float)_northLatitude, (float)latitude);
        float z = Mathf.Lerp(_southLocalZ, _northLocalZ, tLat);

        localPosition = new Vector3(x, 0f, z);
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

    /// <summary>遍历省界网格 Renderer，在 sd_map 局部空间合并 Z 包围盒，作为纬度映射范围。</summary>
    private bool TryComputeMeshLocalZBounds(out float southZ, out float northZ)
    {
        southZ = northZ = 0f;
        bool has = false;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

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

            if (r.transform.IsChildOf(_westAnchor.marker) || r.transform.IsChildOf(_eastAnchor.marker))
            {
                continue;
            }

            Bounds b = r.bounds;
            Vector3 cLocal = _mapRoot.InverseTransformPoint(b.center);
            Vector3 eLocal = _mapRoot.InverseTransformVector(b.extents);
            float z0 = cLocal.z - Mathf.Abs(eLocal.z);
            float z1 = cLocal.z + Mathf.Abs(eLocal.z);
            minZ = Mathf.Min(minZ, z0, z1);
            maxZ = Mathf.Max(maxZ, z0, z1);
            has = true;
        }

        if (!has)
        {
            return false;
        }

        southZ = minZ;
        northZ = maxZ;
        return true;
    }
}
