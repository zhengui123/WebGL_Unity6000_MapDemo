using System;
using UnityEngine;

/// <summary>
/// 山东省界点位过滤与省内随机经纬度采样（测试/Demo 用）。
/// 采样/判定范围与 <see cref="PlateMapGeoConverter"/> 西东锚点经度、南北纬度保持一致。
/// </summary>
[Serializable]
public class PlateMapShandongProvincePointFilter
{
    [SerializeField] private TextAsset _shandongBoundaryJson;
    [SerializeField] private bool _strictProvinceBoundary = true;
    [SerializeField] private int _randomMaxAttemptsPerPoint = 512;

    [Tooltip("随机采样与过滤时优先使用 GeoConverter 的经纬度外接矩形（西/东锚点 + 南/北纬度）")]
    [SerializeField] private bool _useGeoConverterBounds = true;
    [Tooltip("与 PlateMapGeoConverter 默认西锚经度一致")]
    [SerializeField] private double _fallbackWestLongitude = 114.819;
    [Tooltip("与 PlateMapGeoConverter 默认东锚经度一致")]
    [SerializeField] private double _fallbackEastLongitude = 122.714;
    [SerializeField] private double _fallbackSouthLatitude = 34.377;
    [SerializeField] private double _fallbackNorthLatitude = 38.401;
    [Range(0f, 0.2f)]
    [SerializeField] private float _randomBoundsInset = 0.02f;

    private ShandongProvinceBoundary _provinceBoundary;

    public bool StrictProvinceBoundary => _strictProvinceBoundary;

    /// <summary>若 Inspector 未指定，则尝试加载默认 Data 路径下的省界 JSON。</summary>
    public void TryAutoAssignBoundaryJsonAsset()
    {
#if UNITY_EDITOR
        if (_shandongBoundaryJson == null)
        {
            _shandongBoundaryJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Map/PlateMapVehiclePoints/Data/ShandongBoundary.json");
        }
#endif
    }

    /// <summary>严格模式下：在省界多边形内，且在地图 GeoConverter 经纬度矩形内。</summary>
    public bool Contains(double longitude, double latitude, string plateMapName = null)
    {
        if (!_strictProvinceBoundary)
        {
            return true;
        }

        if (!EnsureProvinceBoundaryLoaded())
        {
            return false;
        }

        if (!_provinceBoundary.Contains(longitude, latitude))
        {
            return false;
        }

        return IsWithinMapLongitudeLatitudeBounds(longitude, latitude, plateMapName);
    }

    /// <summary>懒加载省界 JSON（Inspector 指定、默认 Data 路径或 Resources/ShandongBoundary）。</summary>
    public bool EnsureProvinceBoundaryLoaded()
    {
        if (_provinceBoundary != null)
        {
            return true;
        }

        if (_shandongBoundaryJson != null && ShandongProvinceBoundary.TryLoad(_shandongBoundaryJson, out _provinceBoundary))
        {
            return true;
        }

#if UNITY_EDITOR
        if (_shandongBoundaryJson == null)
        {
            TextAsset editorDefault = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Map/PlateMapVehiclePoints/Data/ShandongBoundary.json");
            if (editorDefault != null && ShandongProvinceBoundary.TryLoad(editorDefault, out _provinceBoundary))
            {
                return true;
            }
        }
#endif

        TextAsset fallback = Resources.Load<TextAsset>("ShandongBoundary");
        if (fallback != null && ShandongProvinceBoundary.TryLoad(fallback, out _provinceBoundary))
        {
            return true;
        }

        Debug.LogWarning("[PlateMapShandongProvincePointFilter] 省界 JSON 未加载，请在 Inspector 指定 ShandongBoundary.json。");
        return false;
    }

    /// <summary>
    /// 在 fallback 经纬度外接矩形内均匀采样（不使用省界多边形、不读取 GeoConverter 矩形）。
    /// </summary>
    public bool TrySampleRandomInFallbackRectangle(System.Random rng, out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;

        if (rng == null)
        {
            return false;
        }

        double westLon = _fallbackWestLongitude;
        double eastLon = _fallbackEastLongitude;
        double southLat = _fallbackSouthLatitude;
        double northLat = _fallbackNorthLatitude;

        if (westLon >= eastLon || southLat >= northLat)
        {
            return false;
        }

        longitude = westLon + rng.NextDouble() * (eastLon - westLon);
        latitude = southLat + rng.NextDouble() * (northLat - southLat);
        return true;
    }

    /// <summary>
    /// 在「省界多边形 ∩ GeoConverter 矩形」内采样 WGS84 经纬度。
    /// </summary>
    public bool TrySampleRandomLongitudeLatitude(string plateMapName, System.Random rng, out double longitude, out double latitude)
    {
        TryGetMapLongitudeLatitudeBounds(plateMapName, out double westLon, out double eastLon, out double southLat, out double northLat);
        ApplyBoundsInset(ref westLon, ref eastLon, ref southLat, ref northLat, _randomBoundsInset);

        if (_strictProvinceBoundary)
        {
            if (!EnsureProvinceBoundaryLoaded())
            {
                longitude = 0;
                latitude = 0;
                return false;
            }

            return _provinceBoundary.TryGetRandomLongitudeLatitude(
                rng,
                out longitude,
                out latitude,
                _randomMaxAttemptsPerPoint,
                westLon,
                eastLon,
                southLat,
                northLat);
        }

        longitude = westLon + rng.NextDouble() * (eastLon - westLon);
        latitude = southLat + rng.NextDouble() * (northLat - southLat);
        return true;
    }

    /// <summary>仅按省界多边形判定（显示过滤用，不叠加地图外接矩形）。</summary>
    public bool ContainsInProvince(double longitude, double latitude)
    {
        if (!EnsureProvinceBoundaryLoaded())
        {
            return false;
        }

        return _provinceBoundary.Contains(longitude, latitude);
    }

    /// <summary>省界多边形内判定（兼容旧调用，plateMapName 参数已忽略）。</summary>
    public bool ContainsInProvince(double longitude, double latitude, string plateMapName)
    {
        return ContainsInProvince(longitude, latitude);
    }

    /// <summary>仅保留省界多边形内的点位。</summary>
    public VehicleMapPointData[] FilterVehiclePointsInProvince(VehicleMapPointData[] source)
    {
        if (source == null || source.Length == 0)
        {
            return source;
        }

        if (!EnsureProvinceBoundaryLoaded())
        {
            return Array.Empty<VehicleMapPointData>();
        }

        var kept = new System.Collections.Generic.List<VehicleMapPointData>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            VehicleMapPointData p = source[i];
            if (ContainsInProvince(p.longitude, p.latitude))
            {
                kept.Add(p);
            }
        }

        return kept.ToArray();
    }

    /// <summary>仅保留省界多边形内的点位（兼容旧调用，plateMapName 参数已忽略）。</summary>
    public VehicleMapPointData[] FilterVehiclePointsInProvince(VehicleMapPointData[] source, string plateMapName)
    {
        return FilterVehiclePointsInProvince(source);
    }

    /// <summary>严格模式下：在省界多边形内，且在地图 GeoConverter 经纬度矩形内。</summary>
    public bool ContainsWithMapBounds(double longitude, double latitude, string plateMapName = null)
    {
        if (!ContainsInProvince(longitude, latitude))
        {
            return false;
        }

        return IsWithinMapLongitudeLatitudeBounds(longitude, latitude, plateMapName);
    }

    // Contains 用于 Demo 采样（省界 ∩ 地图矩形）；GeoConverter 显示过滤使用 ContainsInProvince
    public VehicleMapPointData[] FilterVehiclePoints(VehicleMapPointData[] source, string plateMapName = null)
    {
        if (source == null || source.Length == 0 || !_strictProvinceBoundary)
        {
            return source;
        }

        if (!EnsureProvinceBoundaryLoaded())
        {
            return Array.Empty<VehicleMapPointData>();
        }

        var kept = new System.Collections.Generic.List<VehicleMapPointData>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            VehicleMapPointData p = source[i];
            if (Contains(p.longitude, p.latitude, plateMapName))
            {
                kept.Add(p);
            }
        }

        return kept.ToArray();
    }

    /// <summary>从 GeoConverter 或 fallback 读取与地图贴图一致的经纬度外接矩形。</summary>
    private void TryGetMapLongitudeLatitudeBounds(
        string plateMapName,
        out double westLon,
        out double eastLon,
        out double southLat,
        out double northLat)
    {
        westLon = _fallbackWestLongitude;
        eastLon = _fallbackEastLongitude;
        southLat = _fallbackSouthLatitude;
        northLat = _fallbackNorthLatitude;

        if (!_useGeoConverterBounds || string.IsNullOrWhiteSpace(plateMapName))
        {
            return;
        }

        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub != null &&
            hub.InvokeIsGeoConverterReady(plateMapName) &&
            hub.InvokeGetProvinceLongitudeLatitudeBounds(plateMapName, out westLon, out eastLon, out southLat, out northLat))
        {
            return;
        }
    }

    private bool IsWithinMapLongitudeLatitudeBounds(double longitude, double latitude, string plateMapName)
    {
        if (!_useGeoConverterBounds)
        {
            return true;
        }

        TryGetMapLongitudeLatitudeBounds(plateMapName, out double westLon, out double eastLon, out double southLat, out double northLat);
        return ShandongProvinceBoundary.IsWithinLongitudeLatitudeBounds(
            longitude, latitude, westLon, eastLon, southLat, northLat);
    }

    private static void ApplyBoundsInset(
        ref double westLon,
        ref double eastLon,
        ref double southLat,
        ref double northLat,
        float inset)
    {
        if (inset <= 0f)
        {
            return;
        }

        double lonSpan = (eastLon - westLon) * inset;
        double latSpan = (northLat - southLat) * inset;
        westLon += lonSpan;
        eastLon -= lonSpan;
        southLat += latSpan;
        northLat -= latSpan;
    }
}
