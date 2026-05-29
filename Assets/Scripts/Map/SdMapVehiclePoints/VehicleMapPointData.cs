using System;
using UnityEngine;

/// <summary>
/// 车辆点位数据（经纬度 + 告警值）。
/// 序列化在 SdMapVehiclePointController._vehiclePoints 中，可由 Inspector 编辑或代码/随机生成写入。
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
