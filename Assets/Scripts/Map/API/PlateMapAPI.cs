using System;
using UnityEngine;

/// <summary>
/// 板块地图统一 API：按单元 code 路由车辆热力图与 POI 绘制（经 WorldMap 解析）。
/// 国内：省级 adcode；"0"=全国。国外：secondClassCode 国家数字码；大板块用 firstClassCode。
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

    /// <summary>经纬度转板块物体本地坐标（世界映射后再 InverseTransform；POI 等挂接用）。</summary>
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

    /// <summary>解析单元 code（国内省 adcode / 国外国家 SOC）对应的场景板块 GameObject 名称（经 WorldMap）。</summary>
    public bool TryResolvePlateMapName(string provinceCode, out string plateMapName)
    {
        plateMapName = null;
        if (WorldMapPlateResolver.TryResolveUnitModuleName(provinceCode, out plateMapName) &&
            !string.IsNullOrWhiteSpace(plateMapName))
        {
            return true;
        }

        Debug.LogWarning(
            $"[PlateMapAPI] 未找到 code={provinceCode} 对应场景板块（WorldMap：请确认 GeoConverter 已注册或对照表有中文名）。");
        return false;
    }

    /// <summary>从边界库 / WorldMap 对照表读取单元中文名。</summary>
    public bool TryGetProvinceName(string provinceCode, out string provinceName)
    {
        provinceName = null;
        if (WorldMapRegionCodeTable.TryResolveUnitDisplayName(provinceCode, out string displayName) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(displayName, "全国", StringComparison.Ordinal))
        {
            provinceName = displayName;
            return true;
        }

        if (!PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData boundary))
        {
            return false;
        }

        provinceName = boundary.provinceName;
        return true;
    }
}
