using System;
using System.Globalization;

/// <summary>
/// 高危安全事件查询响应（highRiskSecurityEvent）。
/// </summary>
[Serializable]
public class HighRiskSecurityEventResponse
{
    public int code;
    public string msg;
    public HighRiskSecurityEventItem[] data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>单条高危安全事件。</summary>
[Serializable]
public class HighRiskSecurityEventItem
{
    public string eventId;
    public string vin;
    public int eventLevel;
    public string province;
    public string city;
    public string district;
    public string region;
    public string country;
    public string processTime;
    public string longitude;
    public string latitude;

    public bool TryGetLongitudeLatitude(out double longitudeValue, out double latitudeValue)
    {
        longitudeValue = 0d;
        latitudeValue = 0d;

        bool hasLongitude = TryParseCoordinate(longitude, out longitudeValue);
        bool hasLatitude = TryParseCoordinate(latitude, out latitudeValue);
        return hasLongitude && hasLatitude;
    }

    private static bool TryParseCoordinate(string value, out double result)
    {
        result = 0d;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return double.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }
}
