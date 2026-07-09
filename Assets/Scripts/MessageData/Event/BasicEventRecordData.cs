using System;

/// <summary>
/// 告警事件 recordData 原始 JSON 结构（字段按后端实际返回可增补）。
/// </summary>
[Serializable]
public class BasicEventRecordData
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
