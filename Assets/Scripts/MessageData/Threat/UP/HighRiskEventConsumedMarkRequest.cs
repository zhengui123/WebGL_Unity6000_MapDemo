using System;

/// <summary>
/// 高危安全事件消费标记请求体（highRiskEventConsumedMark）。
/// </summary>
[Serializable]
public class HighRiskEventConsumedMarkRequest
{
    /// <summary>已消费（下钻展示）的事件 ID 列表。</summary>
    public string[] eventIds;

    /// <summary>标记截止时间（后端格式 yyyy-MM-dd HH:mm:ss）。</summary>
    public string endTime = string.Empty;

    /// <summary>创建请求体；endTime 为空时使用当前时间。</summary>
    public static HighRiskEventConsumedMarkRequest Create(string[] eventIds, string endTime = null)
    {
        return new HighRiskEventConsumedMarkRequest
        {
            eventIds = eventIds ?? Array.Empty<string>(),
            endTime = BackendDateTimeTool.ResolveEndTimeOrNow(endTime),
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
