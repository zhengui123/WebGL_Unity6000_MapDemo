using System;

/// <summary>
/// 综合区域态势通用请求体（province 为省级 adcode 字符串，空表示全国默认请求）。
/// </summary>
[Serializable]
public class ComprehensiveRegionRequest
{
    public string startTime;
    public string endTime;
    public string province = string.Empty;
    public string region = string.Empty;
    public string country = string.Empty;
    public bool isReplay;

    public ComprehensiveRegionRequest()
    {
        BackendDateTimeTool.GetTodayQueryWindow(out startTime, out endTime);
    }

    /// <summary>全国默认请求参数 JSON（文档示意；运行时默认窗为当日 0 点～当前）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"\",\n" +
        "  \"province\": \"\",\n" +
        "  \"region\": \"\",\n" +
        "  \"country\": \"\",\n" +
        "  \"isReplay\": false\n" +
        "}";

    /// <summary>创建请求体；起止时间为 null/空时用当日 0 点～当前。</summary>
    public static ComprehensiveRegionRequest Create(
        string province = null,
        string region = null,
        string country = null,
        string startTime = null,
        string endTime = null,
        bool isReplay = false)
    {
        return new ComprehensiveRegionRequest
        {
            startTime = BackendDateTimeTool.ResolveStartTimeOrToday(startTime),
            endTime = BackendDateTimeTool.ResolveEndTimeOrNow(endTime),
            province = province ?? string.Empty,
            region = region ?? string.Empty,
            country = country ?? string.Empty,
            isReplay = isReplay,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
