using System;

/// <summary>
/// 告警事件详情查询响应（getSecurityEventDetail）。
/// </summary>
[Serializable]
public class SecurityEventDetailResponse
{
    public int code;
    public string msg;
    public SecurityEventDetailData data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>告警事件详情 data 节点（字段名与后端 snake_case JSON 一致）。</summary>
[Serializable]
public class SecurityEventDetailData
{
    public string event_id;
    public string event_name;
    public string event_level;
    public string happen_time;
    public string vin;
    public int risk_type;
    public string risk_type_name;
    public int risk_subtype;
    public string risk_subtype_name;
    public int part_type;
    public string part_type_name;
    public string source_ip;
    public string target_ip;
    public string vehicle_brand_name;
    public string vehicle_series_name;
    public string vehicle_model_name;
    public string message;
    public SecurityEventOriginalMap originalMap;
    public string record_data;
    public string metri_tag_pk_id;
    public string saasInnerEventType;

    /// <summary>由 <see cref="record_data"/> 解析得到的结构化数据（请求成功后填充）。</summary>
    public SecurityEventRecordData ParsedRecordData { get; private set; }

    /// <summary>解析 record_data 并写入 <see cref="ParsedRecordData"/>。</summary>
    public bool TryApplyRecordData(out string errorMessage)
    {
        if (!TryParseRecordData(out SecurityEventRecordData record, out errorMessage))
        {
            ParsedRecordData = null;
            return false;
        }

        ParsedRecordData = record;
        return true;
    }

    /// <summary>从已解析的 record_data 获取经纬度。</summary>
    public bool TryGetRecordLongitudeLatitude(out double longitude, out double latitude)
    {
        longitude = 0d;
        latitude = 0d;
        return ParsedRecordData != null
            && ParsedRecordData.TryGetLongitudeLatitude(out longitude, out latitude);
    }

    /// <summary>面板：事件名称 + 等级，如「攻防演练-攻击成功 高[10]」。</summary>
    public string BuildEventNameDisplay()
    {
        string levelLabel = FormatEventLevelLabel(event_level);
        if (string.IsNullOrEmpty(event_name))
        {
            return levelLabel;
        }

        if (string.IsNullOrEmpty(levelLabel))
        {
            return event_name;
        }

        return $"{event_name} {levelLabel}";
    }

    /// <summary>面板：品牌/车系/车型。</summary>
    public string BuildVehicleInfoDisplay(string emptyPlaceholder = "-")
    {
        string brand = TrimOrEmpty(vehicle_brand_name);
        string series = TrimOrEmpty(vehicle_series_name);
        string model = TrimOrEmpty(vehicle_model_name);

        if (string.IsNullOrEmpty(brand) && string.IsNullOrEmpty(series) && string.IsNullOrEmpty(model))
        {
            if (TryParseRecordData(out SecurityEventRecordData record, out _))
            {
                return record.BuildBrandSeriesModelDisplay(emptyPlaceholder);
            }

            return emptyPlaceholder;
        }

        return $"{brand}/{series}/{model}".Trim('/');
    }

    /// <summary>尝试解析 record_data JSON 字符串。</summary>
    public bool TryParseRecordData(out SecurityEventRecordData record, out string errorMessage)
    {
        record = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(record_data))
        {
            errorMessage = "record_data 为空。";
            return false;
        }

        if (!HttpJsonParser.TryParse(record_data, out record, out string parseError))
        {
            errorMessage = parseError;
            return false;
        }

        return record != null;
    }

    public static string FormatEventLevelLabel(string eventLevel)
    {
        if (string.IsNullOrWhiteSpace(eventLevel))
        {
            return string.Empty;
        }

        if (!int.TryParse(eventLevel.Trim(), out int level))
        {
            return eventLevel.Trim();
        }

        string prefix = level >= 7 ? "高" : level >= 4 ? "中" : "低";
        return $"{prefix}[{level}]";
    }

    private static string TrimOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

/// <summary>告警详情 originalMap 节点。</summary>
[Serializable]
public class SecurityEventOriginalMap
{
    public string ids_name;
    public string process_time;
    public string ids_version;
    public string ids_type;
    public int attack_status;
    public string unique_count;
}
