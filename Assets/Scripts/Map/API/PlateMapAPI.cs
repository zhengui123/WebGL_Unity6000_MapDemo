using UnityEngine;

/// <summary>
/// 板块地图统一 API：按省级 adcode（string）路由车辆热力图与 POI 绘制。
/// provinceCode 为 "0" 时表示全国整体大板块，其余与 GaodeProvinceAdcode 一致。
/// </summary>
public class PlateMapAPI : UnitySingle<PlateMapAPI>
{
    /// <summary>从 JSON 更新指定板块的车辆点位。</summary>
    public bool UpdateVehiclePointsFromJson(string provinceCode, string vehiclePointsJson, bool syncNow = true)
    {
        if (!VehicleMapPointJson.TryParse(vehiclePointsJson, out VehicleMapPointData[] points, out string error))
        {
            Debug.LogError($"[PlateMapAPI] UpdateVehiclePointsFromJson 失败：{error}");
            return false;
        }

        return UpdateVehiclePoints(provinceCode, points, syncNow);
    }

    /// <summary>更新指定板块的车辆点位并刷新 GPU 显示。</summary>
    public bool UpdateVehiclePoints(string provinceCode, VehicleMapPointData[] points, bool syncNow = true)
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            Debug.LogError("[PlateMapAPI] PlateMapVehiclePointEvents 未初始化。");
            return false;
        }

        return hub.PublishSetVehiclePointsByProvinceCode(provinceCode, points, syncNow);
    }

    /// <summary>请求指定板块按当前数据重建 GPU 车辆热力图显示。</summary>
    public bool RefreshVehicleHeatmap(string provinceCode)
    {
        if (!TryResolvePlateMapName(provinceCode, out string plateMapName))
        {
            return false;
        }

        return PlateMapVehiclePointEvents.Instance.RequestRefreshVehiclePointsDisplay(plateMapName);
    }

    /// <summary>查询板块地理转换是否就绪。</summary>
    public bool IsGeoConverterReady(string provinceCode)
    {
        return TryResolvePlateMapName(provinceCode, out string plateMapName) &&
               PlateMapVehiclePointEvents.Instance.InvokeIsGeoConverterReady(plateMapName);
    }

    /// <summary>经纬度转板块地图局部坐标。</summary>
    public bool TryLongitudeLatitudeToLocal(
        string provinceCode,
        double longitude,
        double latitude,
        out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (!TryResolvePlateMapName(provinceCode, out string plateMapName))
        {
            return false;
        }

        return PlateMapVehiclePointEvents.Instance.InvokeTryLongitudeLatitudeToLocal(
            plateMapName,
            longitude,
            latitude,
            out localPosition);
    }

    /// <summary>获取板块经纬度外接矩形。</summary>
    public bool TryGetBoundary(
        string provinceCode,
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = eastLongitude = southLatitude = northLatitude = 0;
        if (PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData boundary))
        {
            westLongitude = boundary.westLongitude;
            eastLongitude = boundary.eastLongitude;
            southLatitude = boundary.southLatitude;
            northLatitude = boundary.northLatitude;
            return true;
        }

        if (!TryResolvePlateMapName(provinceCode, out string plateMapName))
        {
            return false;
        }

        return PlateMapVehiclePointEvents.Instance.InvokeGetProvinceLongitudeLatitudeBounds(
            plateMapName,
            out westLongitude,
            out eastLongitude,
            out southLatitude,
            out northLatitude);
    }

    /// <summary>解析 provinceCode 对应的场景板块 GameObject 名称。</summary>
    public bool TryResolvePlateMapName(string provinceCode, out string plateMapName)
    {
        plateMapName = null;
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            Debug.LogWarning("[PlateMapAPI] PlateMapVehiclePointEvents 未初始化。");
            return false;
        }

        plateMapName = hub.ResolvePlateMapNameByProvinceCode(provinceCode);
        if (string.IsNullOrWhiteSpace(plateMapName))
        {
            Debug.LogWarning($"[PlateMapAPI] 未找到 provinceCode={provinceCode} 对应的场景板块（请确认 GeoConverter 已挂载并填写 code）。");
            return false;
        }

        return true;
    }

    /// <summary>从边界数据库读取省级中文名。</summary>
    public bool TryGetProvinceName(string provinceCode, out string provinceName)
    {
        provinceName = null;
        if (!PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData boundary))
        {
            return false;
        }

        provinceName = boundary.provinceName;
        return true;
    }
}
