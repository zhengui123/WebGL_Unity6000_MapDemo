using System;

/// <summary>
/// 告警事件详情查询请求体（getSecurityEventDetail）。
/// </summary>
[Serializable]
public class SecurityEventDetailRequest
{
    public string eventId;
    public string processStartTime;
    public string processEndTime;
    public bool passwd;

    public const string DefaultEventId = "ee8d2bc64cbf4bfbb33b875a717c416a";
    public const string DefaultProcessStartTime = "2026-06-26 16:13:30";
    public const string DefaultProcessEndTime = "2026-06-26 16:13:30";

    /// <summary>默认测试请求 JSON。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"eventId\": \"ee8d2bc64cbf4bfbb33b875a717c416a\",\n" +
        "  \"processStartTime\": \"2026-06-26 16:13:30\",\n" +
        "  \"processEndTime\": \"2026-06-26 16:13:30\",\n" +
        "  \"passwd\": false\n" +
        "}";

    public static SecurityEventDetailRequest CreateDefaultTest()
    {
        return new SecurityEventDetailRequest
        {
            eventId = DefaultEventId,
            processStartTime = DefaultProcessStartTime,
            processEndTime = DefaultProcessEndTime,
            passwd = false,
        };
    }

    public static SecurityEventDetailRequest Create(
        string eventId,
        string processStartTime = null,
        string processEndTime = null,
        bool passwd = false)
    {
        return new SecurityEventDetailRequest
        {
            eventId = string.IsNullOrWhiteSpace(eventId) ? DefaultEventId : eventId.Trim(),
            processStartTime = string.IsNullOrWhiteSpace(processStartTime) ? DefaultProcessStartTime : processStartTime.Trim(),
            processEndTime = string.IsNullOrWhiteSpace(processEndTime) ? DefaultProcessEndTime : processEndTime.Trim(),
            passwd = passwd,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
