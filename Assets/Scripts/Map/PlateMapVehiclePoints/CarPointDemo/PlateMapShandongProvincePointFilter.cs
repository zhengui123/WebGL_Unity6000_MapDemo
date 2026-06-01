using System;
using UnityEngine;

/// <summary>
/// 山东省界点位过滤与省内随机经纬度采样（测试/Demo 用，与正式打点显示解耦）。
/// </summary>
[Serializable]
public class PlateMapShandongProvincePointFilter
{
    [SerializeField] private TextAsset _shandongBoundaryJson;
    [SerializeField] private bool _strictProvinceBoundary = true;
    [SerializeField] private int _randomMaxAttemptsPerPoint = 512;

    [SerializeField] private bool _useGeoConverterBounds = true;
    [SerializeField] private double _fallbackWestLongitude = 114.819;
    [SerializeField] private double _fallbackEastLongitude = 122.714;
    [SerializeField] private double _fallbackSouthLatitude = 34.377;
    [SerializeField] private double _fallbackNorthLatitude = 38.401;
    [Range(0f, 0.2f)]
    [SerializeField] private float _randomBoundsInset = 0.02f;

    private ShandongProvinceBoundary _provinceBoundary;

    public bool StrictProvinceBoundary => _strictProvinceBoundary;

    /// <summary>严格省界模式下，点是否在山东省陆域内。</summary>
    public bool Contains(double longitude, double latitude)
    {
        if (!_strictProvinceBoundary)
        {
            return true;
        }

        if (!EnsureProvinceBoundaryLoaded())
        {
            return false;
        }

        return _provinceBoundary.Contains(longitude, latitude);
    }

    /// <summary>懒加载省界 JSON（Inspector 指定或 Resources/ShandongBoundary）。</summary>
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

        TextAsset fallback = Resources.Load<TextAsset>("ShandongBoundary");
        return fallback != null && ShandongProvinceBoundary.TryLoad(fallback, out _provinceBoundary);
    }

    /// <summary>采样一对省内（或矩形内缩进后）的 WGS84 经纬度（经 <see cref="PlateMapVehiclePointEvents"/> 获取地理范围）。</summary>
    public bool TrySampleRandomLongitudeLatitude(string plateMapName, System.Random rng, out double longitude, out double latitude)
    {
        if (_strictProvinceBoundary)
        {
            if (!EnsureProvinceBoundaryLoaded())
            {
                longitude = 0;
                latitude = 0;
                return false;
            }

            return _provinceBoundary.TryGetRandomLongitudeLatitude(
                rng, out longitude, out latitude, _randomMaxAttemptsPerPoint);
        }

        TryGetShandongLongitudeLatitudeBounds(plateMapName, out double westLon, out double eastLon, out double southLat, out double northLat);
        ApplyBoundsInset(ref westLon, ref eastLon, ref southLat, ref northLat, _randomBoundsInset);
        longitude = westLon + rng.NextDouble() * (eastLon - westLon);
        latitude = southLat + rng.NextDouble() * (northLat - southLat);
        return true;
    }

    /// <summary>过滤掉省界外的点位（非严格模式时原样返回）。</summary>
    public VehicleMapPointData[] FilterVehiclePoints(VehicleMapPointData[] source)
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
            if (_provinceBoundary.Contains(p.longitude, p.latitude))
            {
                kept.Add(p);
            }
        }

        return kept.ToArray();
    }

    private void TryGetShandongLongitudeLatitudeBounds(
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
        if (hub.InvokeIsGeoConverterReady(plateMapName) &&
            hub.InvokeGetProvinceLongitudeLatitudeBounds(plateMapName, out westLon, out eastLon, out southLat, out northLat))
        {
            return;
        }
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
