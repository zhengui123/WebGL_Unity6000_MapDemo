using System;

/// <summary>
/// 板块地图边界配置（与 Resources/PlateMapBoundaries.json /
/// Resources/PlateMapForeignBoundaries.json 结构一致）。
/// provinceCode：国内为省级 adcode / "0"；国外为国家 ISO 数字码或大板块 firstClassCode。
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
