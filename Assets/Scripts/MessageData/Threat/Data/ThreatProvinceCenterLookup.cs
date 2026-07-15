using UnityEngine;

/// <summary>
/// 省级中心经纬度：优先 ChinaProvinceMapDatabase（代码内嵌，打包可读），
/// 失败时回退边界矩形中心（Resources/PlateMapBoundaries）。
/// </summary>
public static class ThreatProvinceCenterLookup
{
    public static bool TryGetCenter(string provinceCode, out double longitude, out double latitude)
    {
        longitude = 0d;
        latitude = 0d;

        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return false;
        }

        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string shortName) &&
            ChinaProvinceMapDatabase.TryGet(shortName, out ChinaProvinceMapFocusData focus))
        {
            longitude = focus.Longitude;
            latitude = focus.Latitude;
            return true;
        }

        if (PlateMapAPI.Instance != null &&
            PlateMapAPI.Instance.TryGetProvinceName(provinceCode, out string boundaryName) &&
            ChinaProvinceMapDatabase.TryGet(boundaryName, out focus))
        {
            longitude = focus.Longitude;
            latitude = focus.Latitude;
            return true;
        }

        if (PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData bounds))
        {
            longitude = (bounds.westLongitude + bounds.eastLongitude) * 0.5d;
            latitude = (bounds.southLatitude + bounds.northLatitude) * 0.5d;
            return true;
        }

        Debug.LogWarning($"[ThreatProvinceCenterLookup] 未找到省中心：code={provinceCode}");
        return false;
    }
}
