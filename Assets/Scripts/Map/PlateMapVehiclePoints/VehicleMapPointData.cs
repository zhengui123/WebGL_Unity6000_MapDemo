using System;
using UnityEngine;

/// <summary>
/// 车辆点位数据（经纬度 + 告警值）。
/// 由 PlateMapVehiclePointController 持有；业务/Demo 经 PlateMapVehiclePointEvents 单例写入。
/// </summary>
[Serializable]
public struct VehicleMapPointData
{
    [Tooltip("车辆唯一标识")]
    public string vehicleId;

    [Tooltip("WGS84 经度（度）")]
    public double longitude;

    [Tooltip("WGS84 纬度（度）")]
    public double latitude;

    [Tooltip("业务数值；在控制器的数据源最小/最大之间插值得到点位颜色")]
    public float alertValue;
}
