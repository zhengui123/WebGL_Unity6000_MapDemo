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
