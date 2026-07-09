using System;
using System.Globalization;

/// <summary>
/// 本地缓存的车辆位置记录（由接口数据转换，以 VinEncrypt 为唯一键）。
/// </summary>
[Serializable]
public class HttpVehicleLocationRecord
{
    public string VinEncrypt;
    public string Vin;
    public double Longitude;
    public double Latitude;
    public string Province;
    public string City;
    public string District;
    public string Region;
    public string Country;

    public static HttpVehicleLocationRecord FromApiItem(LatestVinLocationItem item)
    {
        if (item == null)
        {
            return null;
        }

        return new HttpVehicleLocationRecord
        {
            VinEncrypt = item.vinEncrypt,
            Vin = item.vin,
            Longitude = ParseCoordinate(item.longitude),
            Latitude = ParseCoordinate(item.latitude),
            Province = NormalizeNullableString(item.province),
            City = NormalizeNullableString(item.city),
            District = NormalizeNullableString(item.district),
            Region = NormalizeNullableString(item.region),
            Country = NormalizeNullableString(item.country),
        };
    }

    /// <summary>转为地图车辆点位：vehicleId 对应 vinEncrypt，alertValue 默认 1。</summary>
    public VehicleMapPointData ToVehicleMapPointData(float alertValue = 1f)
    {
        return new VehicleMapPointData
        {
            vehicleId = VinEncrypt,
            longitude = Longitude,
            latitude = Latitude,
            alertValue = alertValue,
        };
    }

    /// <summary>将接口车辆列表转为地图点位数组（跳过无 vinEncrypt 的项）。</summary>
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
            if (record == null || string.IsNullOrWhiteSpace(record.VinEncrypt))
            {
                continue;
            }

            points[count++] = record.ToVehicleMapPointData(alertValue);
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

    private static double ParseCoordinate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0d;
    }

    private static string NormalizeNullableString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
