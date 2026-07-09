using System;

/// <summary>
/// 告警事件 record_data 原始 JSON 结构。
/// </summary>
[Serializable]
public class SecurityEventRecordData
{
    public string vin;
    public string source_ip;
    public string target_ip;
    public string risk_sub_type;
    public string risk_subtype;
    public string vehicle_brand_name;
    public string vehicle_series_name;
    public string vehicle_model_name;
    public string message;
    public string messageabc;
    public string model;
    public string part_type;
    public string part_id;
    public string ids_name;
    public string ids_type;
    public string attack_status;
    public string longitude;
    public string latitude;
    public string name;
    public string level;
    public string happen_time;
    public string version;

    /// <summary>尝试解析经纬度（record_data 中为字符串）。</summary>
    public bool TryGetLongitudeLatitude(out double longitudeValue, out double latitudeValue)
    {
        longitudeValue = 0d;
        latitudeValue = 0d;
        if (string.IsNullOrWhiteSpace(longitude) || string.IsNullOrWhiteSpace(latitude))
        {
            return false;
        }

        return double.TryParse(longitude.Trim(), out longitudeValue)
            && double.TryParse(latitude.Trim(), out latitudeValue);
    }

    public string BuildBrandSeriesModelDisplay(string emptyPlaceholder = "-")
    {
        string brand = FirstNonEmpty(vehicle_brand_name);
        string series = FirstNonEmpty(vehicle_series_name);
        string vehicleModel = FirstNonEmpty(vehicle_model_name, model);

        if (string.IsNullOrEmpty(brand) && string.IsNullOrEmpty(series) && string.IsNullOrEmpty(vehicleModel))
        {
            return emptyPlaceholder;
        }

        return $"{brand}/{series}/{vehicleModel}".Trim('/');
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                return values[i].Trim();
            }
        }

        return string.Empty;
    }
}
