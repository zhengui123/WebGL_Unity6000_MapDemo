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
    public const string DefaultProcessStartTime = "2026-06-30 17:41:23";
    public const string DefaultProcessEndTime = "2026-06-30 17:41:23";
    public const int DefaultTenantId = 1;

    /// <summary>默认测试请求 JSON（对齐接口文档示例）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"eventId\": \"123dfdsafffff\",\n" +
        "  \"processStartTime\": \"2026-06-30 17:41:23\",\n" +
        "  \"processEndTime\": \"2026-06-30 17:41:23\",\n" +
        "  \"columns\": [],\n" +
        "  \"tenantId\": 1\n" +
        "}";

    public static SecurityEventDetailRequest CreateDefaultTest()
    {
        return new SecurityEventDetailRequest
        {
            eventId = DefaultEventId,
            processStartTime = DefaultProcessStartTime,
            processEndTime = DefaultProcessEndTime,
            columns = Array.Empty<string>(),
            tenantId = DefaultTenantId,
        };
    }

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
            processStartTime = string.IsNullOrWhiteSpace(processStartTime)
                ? DefaultProcessStartTime
                : processStartTime.Trim(),
            processEndTime = string.IsNullOrWhiteSpace(processEndTime)
                ? DefaultProcessEndTime
                : processEndTime.Trim(),
            columns = columns ?? Array.Empty<string>(),
            tenantId = tenantId ?? DefaultTenantId,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
