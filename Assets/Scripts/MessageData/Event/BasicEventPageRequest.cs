using System;

/// <summary>
/// 告警事件分页查询请求体（getBasicEventPage）。
/// </summary>
[Serializable]
public class BasicEventPageRequest
{
    public int pageNum = 1;
    public int pageSize = 10;
    public string startTime = string.Empty;
    public string endTime = string.Empty;

    /// <summary>Demo 默认请求参数 JSON。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"pageNum\": 1,\n" +
        "  \"pageSize\": 10,\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"\"\n" +
        "}";

    public static BasicEventPageRequest Create(
        int pageNum = 1,
        int pageSize = 10,
        string startTime = null,
        string endTime = null)
    {
        return new BasicEventPageRequest
        {
            pageNum = pageNum < 1 ? 1 : pageNum,
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
