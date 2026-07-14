using System;

/// <summary>
/// 本地缓存的热力点坐标（与接口 data 项一致：x=经度，y=纬度）。
/// </summary>
[Serializable]
public class HttpVehicleLocationRecord
{
    public double Longitude;
    public double Latitude;

    public static HttpVehicleLocationRecord FromApiItem(LatestVinLocationItem item)
    {
        if (item == null)
        {
            return null;
        }

        return new HttpVehicleLocationRecord
        {
            Longitude = item.x,
            Latitude = item.y,
        };
    }

    /// <summary>
    /// 转为地图车辆点位。Controller 合并逻辑要求 vehicleId 非空，此处用索引占位（非 vin）。
    /// </summary>
    public VehicleMapPointData ToVehicleMapPointData(int index, float alertValue = 1f)
    {
        return new VehicleMapPointData
        {
            vehicleId = $"P{index}",
            longitude = Longitude,
            latitude = Latitude,
            alertValue = alertValue,
        };
    }

    /// <summary>将接口点列表转为地图点位数组。</summary>
    public static VehicleMapPointData[] ToVehicleMapPointArray(LatestVinLocationItem[] items, float alertValue = 1f)
    {
        if (items == null || items.Length == 0)
        {
            return Array.Empty<VehicleMapPointData>();
        }

        VehicleMapPointData[] points = new VehicleMapPointData[items.Length];
        int count = 0;
        for (int i = 0; i < items.Length; i++)
        {
            HttpVehicleLocationRecord record = FromApiItem(items[i]);
            if (record == null)
            {
                continue;
            }

            points[count] = record.ToVehicleMapPointData(count, alertValue);
            count++;
        }

        if (count == 0)
        {
            return Array.Empty<VehicleMapPointData>();
        }

        if (count == points.Length)
        {
            return points;
        }

        VehicleMapPointData[] trimmed = new VehicleMapPointData[count];
        Array.Copy(points, trimmed, count);
        return trimmed;
    }
}
