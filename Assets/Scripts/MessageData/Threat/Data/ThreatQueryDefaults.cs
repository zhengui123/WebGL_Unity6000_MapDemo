/// <summary>
/// 威胁态势接口默认查询时间（测试用，可按需修改）。
/// </summary>
public static class ThreatQueryDefaults
{
    /// <summary>默认开始时间。</summary>
    public const string StartTime = "2026-06-20 23:00:00";

    /// <summary>默认结束时间。</summary>
    public const string EndTime = "2026-06-30 23:00:00";

    /// <summary>解析开始时间：空/null 时回退到 <see cref="StartTime"/>。</summary>
    public static string ResolveStartTime(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? StartTime : value.Trim();
    }

    /// <summary>解析结束时间：空/null 时回退到 <see cref="EndTime"/>。</summary>
    public static string ResolveEndTime(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? EndTime : value.Trim();
    }
}
