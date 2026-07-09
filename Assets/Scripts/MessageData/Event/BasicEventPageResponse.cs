using System;

/// <summary>
/// 告警事件分页查询响应（getBasicEventPage）。
/// </summary>
[Serializable]
public class BasicEventPageResponse
{
    public int code;
    public string msg;
    public BasicEventPageData data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;

    /// <summary>取列表首条告警事件；无数据时返回 null。</summary>
    public BasicEventItem GetFirstEvent()
    {
        return data != null ? data.GetFirstItem() : null;
    }
}

/// <summary>分页数据容器。</summary>
[Serializable]
public class BasicEventPageData
{
    public BasicEventItem[] list;
    public int total;
    public int pageNo;
    public int pageSize;
    public int pages;
    public int eventCount;

    public BasicEventItem GetFirstItem()
    {
        return list != null && list.Length > 0 ? list[0] : null;
    }
}

/// <summary>单条告警事件（与后端 list 项 JSON 字段一致）。</summary>
[Serializable]
public class BasicEventItem
{
    public string eventId;
    public string eventName;
    public int riskType;
    public string riskTypeName;
    public string vin;
    public int eventLevel;
    public string eventLevelName;
    public int partType;
    public string partTypeName;
    public string happenTime;
    public string processTime;
    public string recordData;
    public string metriTagPkId;
    public string sensitiveDataId;

    /// <summary>面板展示：事件名称 + 等级，如「低危基本事件6 低[1]」。</summary>
    public string BuildEventNameDisplay()
    {
        if (string.IsNullOrEmpty(eventLevelName))
        {
            return eventName ?? string.Empty;
        }

        if (string.IsNullOrEmpty(eventName))
        {
            return eventLevelName;
        }

        return $"{eventName} {eventLevelName}";
    }

    /// <summary>尝试解析 recordData JSON 字符串。</summary>
    public bool TryParseRecordData(out BasicEventRecordData record, out string errorMessage)
    {
        record = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(recordData))
        {
            errorMessage = "recordData 为空。";
            return false;
        }

        if (!HttpJsonParser.TryParse(recordData, out record, out string parseError))
        {
            errorMessage = parseError;
            return false;
        }

        return record != null;
    }

    /// <summary>面板展示：品牌/车系/车型；优先 recordData，否则返回占位符。</summary>
    public string BuildVehicleInfoDisplay(string emptyPlaceholder = "-")
    {
        if (!TryParseRecordData(out BasicEventRecordData record, out _))
        {
            return emptyPlaceholder;
        }

        return record.BuildBrandSeriesModelDisplay(emptyPlaceholder);
    }
}
