using System;
using System.Globalization;

/// <summary>
/// 综合区域态势通用请求体（province 为空表示全国/全部）。
/// </summary>
[Serializable]
public class ComprehensiveRegionRequest
{
    public string startTime = HttpProjectConfig.DefaultQueryStartTime;
    public string endTime = HttpProjectConfig.DefaultQueryEndTime;
    public string province = string.Empty;
    public string region = string.Empty;
    public string country = string.Empty;

    /// <summary>Demo 默认请求参数 JSON（格式化，便于 UI 展示）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"2026-06-30 23:00:00\",\n" +
        "  \"province\": \"\",\n" +
        "  \"region\": \"\",\n" +
        "  \"country\": \"\"\n" +
        "}";

    /// <summary>创建请求体；起止时间与 province/region/country 均可为 null（使用项目默认或空字符串）。</summary>
    public static ComprehensiveRegionRequest Create(
        string province = null,
        string region = null,
        string country = null,
        string startTime = null,
        string endTime = null)
    {
        return new ComprehensiveRegionRequest
        {
            startTime = startTime ?? HttpProjectConfig.DefaultQueryStartTime,
            endTime = endTime ?? HttpProjectConfig.DefaultQueryEndTime,
            province = province ?? string.Empty,
            region = region ?? string.Empty,
            country = country ?? string.Empty,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
