using System;

/// <summary>
/// 告警事件分页查询请求体（getBasicEventPage）。
/// </summary>
[Serializable]
public class BasicEventPageRequest
{
    public int pageNo = 1;
    public int pageSize = 10;
    public string startTime = string.Empty;
    public string endTime = string.Empty;

    /// <summary>Demo 默认请求参数 JSON。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"pageNo\": 1,\n" +
        "  \"pageSize\": 10,\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"\"\n" +
        "}";

    public static BasicEventPageRequest Create(
        int pageNo = 1,
        int pageSize = 10,
        string startTime = null,
        string endTime = null)
    {
        return new BasicEventPageRequest
        {
            pageNo = pageNo < 1 ? 1 : pageNo,
            pageSize = pageSize < 1 ? 10 : pageSize,
            startTime = startTime ?? string.Empty,
            endTime = endTime ?? string.Empty,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
