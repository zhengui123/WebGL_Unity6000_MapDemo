using System;

/// <summary>
/// 事件溯源详情查询请求体（getSourceEventDetail）。
/// </summary>
[Serializable]
public class SecurityEventDetailRequest
{
    public string eventId;
    public string processStartTime;
    public string processEndTime;
    public string[] columns;
    public int tenantId;

    public const string DefaultEventId = "123dfdsafffff";
    /// <summary>文档/Demo 占位示例；运行时 Create 空参改为当日 0 点～当前。</summary>
    public const string DefaultProcessStartTime = "2026-06-30 17:41:23";
    public const string DefaultProcessEndTime = "2026-06-30 17:41:23";
    public const int DefaultTenantId = 1;

    /// <summary>默认测试请求 JSON（文档示意；运行时时间为当日 0 点～当前）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"eventId\": \"123dfdsafffff\",\n" +
        "  \"processStartTime\": \"\",\n" +
        "  \"processEndTime\": \"\",\n" +
        "  \"columns\": [],\n" +
        "  \"tenantId\": 1\n" +
        "}";

    public static SecurityEventDetailRequest CreateDefaultTest()
    {
        return Create(DefaultEventId, processStartTime: null, processEndTime: null);
    }

    /// <summary>创建请求体；时间为 null/空时：processStart=当日 0 点，processEnd=当前时间。</summary>
    public static SecurityEventDetailRequest Create(
        string eventId,
        string processStartTime = null,
        string processEndTime = null,
        string[] columns = null,
        int? tenantId = null)
    {
        return new SecurityEventDetailRequest
        {
            eventId = string.IsNullOrWhiteSpace(eventId) ? DefaultEventId : eventId.Trim(),
            processStartTime = BackendDateTimeTool.ResolveStartTimeOrToday(processStartTime),
            processEndTime = BackendDateTimeTool.ResolveEndTimeOrNow(processEndTime),
            columns = columns ?? Array.Empty<string>(),
            tenantId = tenantId ?? DefaultTenantId,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
