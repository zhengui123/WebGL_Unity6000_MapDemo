using UnityEngine;

public class PlateMapAPI : UnitySingle<PlateMapAPI>
{

    /// <summary>
    /// 从 JSON 更新车辆点位。
    /// </summary>
    /// <param name="plateMapName">板块名称。</param>
    /// <param name="vehiclePointsJson">车辆点位 JSON 字符串。</param>
    /// <param name="syncNow">是否立即同步。</param>
    /// <returns>是否成功。</returns>
    public bool UpdateVehiclePointsFromJson(string plateMapName, string vehiclePointsJson, bool syncNow = true)
    {
        if (!VehicleMapPointJson.TryParse(vehiclePointsJson, out VehicleMapPointData[] points, out string error))
        {
            Debug.LogError($"[PlateMapAPI] UpdateVehiclePointsFromJson 失败：{error}");
            return false;
        }

        return PlateMapVehiclePointEvents.Instance.PublishSetVehiclePoints(plateMapName, points, syncNow);
    }
}
