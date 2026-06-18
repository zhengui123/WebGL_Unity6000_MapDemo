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
            if (exterior.Lon == null || exterior.Lon.Length < 3)
            {
                continue;
            }

            Ring[] holes = Array.Empty<Ring>();
            if (src.holes != null && src.holes.Length > 0)
            {
                var holeList = new System.Collections.Generic.List<Ring>(src.holes.Length);
                for (int h = 0; h < src.holes.Length; h++)
                {
                    Ring hole = CreateRing(src.holes[h].lon, src.holes[h].lat);
                    if (hole.Lon != null && hole.Lon.Length >= 3)
                    {
                        holeList.Add(hole);
                    }
                }

                if (holeList.Count > 0)
                {
                    holes = holeList.ToArray();
                }
            }

            polygons[i] = new Polygon { Exterior = exterior, Holes = holes };
        }

        int validCount = 0;
        for (int i = 0; i < polygons.Length; i++)
        {
            if (polygons[i].Exterior.Lon != null && polygons[i].Exterior.Lon.Length >= 3)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return false;
        }

        if (validCount != polygons.Length)
        {
            var compact = new Polygon[validCount];
            int write = 0;
            for (int i = 0; i < polygons.Length; i++)
            {
                if (polygons[i].Exterior.Lon != null && polygons[i].Exterior.Lon.Length >= 3)
                {
                    compact[write++] = polygons[i];
                }
            }

            polygons = compact;
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

    /// <summary>在外接矩形（可与地图锚点范围求交）内拒绝采样，保证点在省界多边形内。</summary>
    public bool TryGetRandomLongitudeLatitude(
        System.Random random,
        out double longitude,
        out double latitude,
        int maxAttempts = 512,
        double clipWest = double.NaN,
        double clipEast = double.NaN,
        double clipSouth = double.NaN,
        double clipNorth = double.NaN)
    {
        longitude = 0;
        latitude = 0;

        if (random == null)
        {
            return false;
        }

        double west = double.IsNaN(clipWest) ? _westLon : Math.Max(clipWest, _westLon);
        double east = double.IsNaN(clipEast) ? _eastLon : Math.Min(clipEast, _eastLon);
        double south = double.IsNaN(clipSouth) ? _southLat : Math.Max(clipSouth, _southLat);
        double north = double.IsNaN(clipNorth) ? _northLat : Math.Min(clipNorth, _northLat);

        if (west >= east || south >= north)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            double lon = west + random.NextDouble() * (east - west);
            double lat = south + random.NextDouble() * (north - south);
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

    /// <summary>点是否在指定经纬度矩形内（与 <see cref="PlateMapGeoConverter.GetProvinceLongitudeLatitudeBounds"/> 一致）。</summary>
    public static bool IsWithinLongitudeLatitudeBounds(
        double longitude,
        double latitude,
        double westLongitude,
        double eastLongitude,
        double southLatitude,
        double northLatitude)
    {
        return longitude >= westLongitude &&
               longitude <= eastLongitude &&
               latitude >= southLatitude &&
               latitude <= northLatitude;
    }

    /// <summary>将 JSON 浮点数组转为双精度环；去除 GeoJSON 闭合重复顶点。</summary>
    private static Ring CreateRing(float[] lon, float[] lat)
    {
        if (lon == null || lat == null || lon.Length != lat.Length || lon.Length < 3)
        {
            return default;
        }

        int count = lon.Length;
        if (count > 3 &&
            Math.Abs(lon[0] - lon[count - 1]) < 1e-8 &&
            Math.Abs(lat[0] - lat[count - 1]) < 1e-8)
        {
            count--;
        }

        if (count < 3)
        {
            return default;
        }

        double[] lonD = new double[count];
        double[] latD = new double[count];
        for (int i = 0; i < count; i++)
        {
            lonD[i] = lon[i];
            latD[i] = lat[i];
        }

        return new Ring { Lon = lonD, Lat = latD };
    }

    /// <summary>射线法 + 非零环绕数，兼容 GeoJSON 外环顺/逆时针。</summary>
    private static bool IsPointInRing(double x, double y, Ring ring)
    {
        if (ring.Lon == null || ring.Lat == null || ring.Lon.Length < 3)
        {
            return false;
        }

        int winding = 0;
        int count = ring.Lon.Length;
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            double yi = ring.Lat[i];
            double yj = ring.Lat[j];
            if (yi <= y)
            {
                if (yj > y && Cross(ring.Lon[i], yi, ring.Lon[j], yj, x, y) > 0d)
                {
                    winding++;
                }
            }
            else if (yj <= y && Cross(ring.Lon[i], yi, ring.Lon[j], yj, x, y) < 0d)
            {
                winding--;
            }
        }

        return winding != 0;
    }

    private static double Cross(double ax, double ay, double bx, double by, double px, double py)
    {
        return (bx - ax) * (py - ay) - (by - ay) * (px - ax);
    }
}
