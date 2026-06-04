using System;

/// <summary>
/// 中国省级地图聚焦数据（省名 + 中心经纬度 + 推荐 zoom）。
/// </summary>
[Serializable]
public class ChinaProvinceMapFocusData
{
    /// <summary>省/直辖市/自治区名称（标准简称，如「山东」「北京」）。</summary>
    public string ProvinceName;

    /// <summary>省域几何中心经度（WGS84）。</summary>
    public double Longitude;

    /// <summary>省域几何中心纬度（WGS84）。</summary>
    public double Latitude;

    /// <summary>聚焦到该省时的推荐缩放级别。</summary>
    public int Zoom;

    public ChinaProvinceMapFocusData(string provinceName, double longitude, double latitude, int zoom)
    {
        ProvinceName = provinceName;
        Longitude = longitude;
        Latitude = latitude;
        Zoom = zoom;
    }
}
