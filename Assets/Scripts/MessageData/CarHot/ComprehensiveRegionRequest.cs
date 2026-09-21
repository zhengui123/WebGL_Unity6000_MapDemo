using System;

/// <summary>
/// 综合区域态势通用请求体。
/// firstClassCode 为板块 code（国内或国外某一板块）；热力图请求不填 province。
/// </summary>
[Serializable]
public class ComprehensiveRegionRequest
{
    public string startTime;
    public string endTime;
    public string province = string.Empty;
    public string region = string.Empty;
    public string country = string.Empty;
    public string firstClassCode = string.Empty;
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
        "  \"firstClassCode\": \"\",\n" +
        "  \"isReplay\": false\n" +
        "}";

    /// <summary>创建请求体；起止时间为 null/空时用当日 0 点～当前。</summary>
    public static ComprehensiveRegionRequest Create(
        string province = null,
        string region = null,
        string country = null,
        string startTime = null,
        string endTime = null,
        bool isReplay = false,
        string firstClassCode = null)
    {
        return new ComprehensiveRegionRequest
        {
            startTime = BackendDateTimeTool.ResolveStartTimeOrToday(startTime),
            endTime = BackendDateTimeTool.ResolveEndTimeOrNow(endTime),
            province = province ?? string.Empty,
            region = region ?? string.Empty,
            country = country ?? string.Empty,
            firstClassCode = firstClassCode ?? string.Empty,
            isReplay = isReplay,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
