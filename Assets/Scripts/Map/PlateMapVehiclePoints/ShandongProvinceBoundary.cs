using System;
using UnityEngine;

/// <summary>
/// 山东省行政边界（WGS84 经纬度），支持 MultiPolygon 与内环（岛屿/飞地孔洞）点内判断。
/// 边界数据来自 DataV 370000 区划 GeoJSON。
/// </summary>
public sealed class ShandongProvinceBoundary
{
    // JsonUtility 反序列化用，字段名需与 ShandongBoundary.json 一致
    [Serializable]
    private class BoundaryJson
    {
        public float westLongitude;
        public float eastLongitude;
        public float southLatitude;
        public float northLatitude;
        public PolygonJson[] polygons;
    }

    [Serializable]
    private class PolygonJson
    {
        public float[] exteriorLon;
        public float[] exteriorLat;
        public HoleJson[] holes;
    }

    [Serializable]
    private class HoleJson
    {
        public float[] lon;
        public float[] lat;
    }

    /// <summary>闭合环：经度/纬度顶点序列（WGS84，度）。</summary>
    private struct Ring
    {
        public double[] Lon;
        public double[] Lat;
    }

    /// <summary>单个面：外环 + 可选内环（湖泊/内海孔洞）。</summary>
    private struct Polygon
    {
        public Ring Exterior;
        public Ring[] Holes;
    }

    /// <summary>山东省 MultiPolygon 拆成的多个面（含长岛等离岛）。</summary>
    private readonly Polygon[] _polygons;
    private readonly double _westLon;
    private readonly double _eastLon;
    private readonly double _southLat;
    private readonly double _northLat;

    public double WestLongitude => _westLon;
    public double EastLongitude => _eastLon;
    public double SouthLatitude => _southLat;
    public double NorthLatitude => _northLat;

    private ShandongProvinceBoundary(Polygon[] polygons, double west, double east, double south, double north)
    {
        _polygons = polygons;
        _westLon = west;
        _eastLon = east;
        _southLat = south;
        _northLat = north;
    }

    /// <summary>从 TextAsset 加载省界并构建运行时多边形数据。</summary>
    public static bool TryLoad(TextAsset textAsset, out ShandongProvinceBoundary boundary)
    {
        boundary = null;
        if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
        {
            return false;
        }

        BoundaryJson data = JsonUtility.FromJson<BoundaryJson>(textAsset.text);
        if (data?.polygons == null || data.polygons.Length == 0)
        {
            return false;
        }

        Polygon[] polygons = new Polygon[data.polygons.Length];
        for (int i = 0; i < data.polygons.Length; i++)
        {
            PolygonJson src = data.polygons[i];
            if (src.exteriorLon == null || src.exteriorLat == null || src.exteriorLon.Length < 3)
            {
                continue;
            }

            Ring exterior = CreateRing(src.exteriorLon, src.exteriorLat);
            Ring[] holes = Array.Empty<Ring>();
            if (src.holes != null && src.holes.Length > 0)
            {
                holes = new Ring[src.holes.Length];
                for (int h = 0; h < src.holes.Length; h++)
                {
                    holes[h] = CreateRing(src.holes[h].lon, src.holes[h].lat);
                }
            }

            polygons[i] = new Polygon { Exterior = exterior, Holes = holes };
        }

        boundary = new ShandongProvinceBoundary(
            polygons,
            data.westLongitude,
            data.eastLongitude,
            data.southLatitude,
            data.northLatitude);
        return true;
    }

    /// <summary>经纬度是否在山东省陆域范围内（含主陆与省辖岛屿，不含内湖孔洞）。</summary>
    public bool Contains(double longitude, double latitude)
    {
        // 先用外接矩形快速剔除，减少射线法调用次数
        if (longitude < _westLon || longitude > _eastLon || latitude < _southLat || latitude > _northLat)
        {
            return false;
        }

        // 任一面片满足「在外环内且不在任何内环内」即视为省内
        for (int i = 0; i < _polygons.Length; i++)
        {
            Polygon polygon = _polygons[i];
            if (polygon.Exterior.Lon == null || polygon.Exterior.Lon.Length < 3)
            {
                continue;
            }

            if (!IsPointInRing(longitude, latitude, polygon.Exterior))
            {
                continue;
            }

            bool inHole = false;
            if (polygon.Holes != null)
            {
                for (int h = 0; h < polygon.Holes.Length; h++)
                {
                    if (IsPointInRing(longitude, latitude, polygon.Holes[h]))
                    {
                        inHole = true;
                        break;
                    }
                }
            }

            if (!inHole)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>在外接矩形内拒绝采样，保证点在省界内。</summary>
    public bool TryGetRandomLongitudeLatitude(System.Random random, out double longitude, out double latitude, int maxAttempts = 512)
    {
        longitude = 0;
        latitude = 0;

        if (random == null)
        {
            return false;
        }

        // 拒绝采样：在矩形内均匀撒点，直到落在真实省界多边形内
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            double lon = _westLon + random.NextDouble() * (_eastLon - _westLon);
            double lat = _southLat + random.NextDouble() * (_northLat - _southLat);
            if (!Contains(lon, lat))
            {
                continue;
            }

            longitude = lon;
            latitude = lat;
            return true;
        }

        return false;
    }

    private static Ring CreateRing(float[] lon, float[] lat)
    {
        if (lon == null || lat == null || lon.Length != lat.Length)
        {
            return default;
        }

        double[] lonD = new double[lon.Length];
        double[] latD = new double[lat.Length];
        for (int i = 0; i < lon.Length; i++)
        {
            lonD[i] = lon[i];
            latD[i] = lat[i];
        }

        return new Ring { Lon = lonD, Lat = latD };
    }

    /// <summary>射线法判断点是否在闭合环内。</summary>
    private static bool IsPointInRing(double x, double y, Ring ring)
    {
        if (ring.Lon == null || ring.Lat == null || ring.Lon.Length < 3)
        {
            return false;
        }

        // 经典射线法：从点向右发水平射线，统计与多边形边交点数，奇数为内、偶数为外
        bool inside = false;
        int count = ring.Lon.Length;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double xi = ring.Lon[i];
            double yi = ring.Lat[i];
            double xj = ring.Lon[j];
            double yj = ring.Lat[j];

            // 边 (j→i) 是否跨过纬度 y；若跨过则求交点经度并与 x 比较
            bool intersect = yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi + 1e-15) + xi;
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
