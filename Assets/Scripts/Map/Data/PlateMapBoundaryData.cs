using System;

/// <summary>
/// 板块地图边界配置（与 PlateMapBoundaries.json 结构一致）。
/// provinceCode 为高德省级 adcode 字符串；"0" 表示全国整体大板块。
/// </summary>
[Serializable]
public class PlateMapBoundaryResponse
{
    public PlateMapBoundaryData[] entries;
}

/// <summary>
/// 单个省/全国板块的经纬度外接矩形边界。
/// </summary>
[Serializable]
public class PlateMapBoundaryData
{
    public string provinceCode;
    public string provinceName;
    public double westLongitude;
    public double eastLongitude;
    public double southLatitude;
    public double northLatitude;

    public void NormalizeBounds()
    {
        double west = Math.Min(westLongitude, eastLongitude);
        double east = Math.Max(westLongitude, eastLongitude);
        double south = Math.Min(southLatitude, northLatitude);
        double north = Math.Max(southLatitude, northLatitude);
        westLongitude = west;
        eastLongitude = east;
        southLatitude = south;
        northLatitude = north;
    }
}
