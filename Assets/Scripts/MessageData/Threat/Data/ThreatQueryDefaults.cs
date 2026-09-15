/// <summary>
/// 威胁态势接口默认查询时间：空参时为当日 0 点～当前时间。
/// </summary>
public static class ThreatQueryDefaults
{
    /// <summary>默认开始时间（当日 00:00:00）。</summary>
    public static string StartTime => BackendDateTimeTool.GetTodayStartTimeString();

    /// <summary>默认结束时间（当前时间）。</summary>
    public static string EndTime => BackendDateTimeTool.GetCurrentTimeString();

    /// <summary>解析开始时间：空/null 时回退到当日 0 点。</summary>
    public static string ResolveStartTime(string value)
    {
        return BackendDateTimeTool.ResolveStartTimeOrToday(value);
    }

    /// <summary>解析结束时间：空/null 时回退到当前时间。</summary>
    public static string ResolveEndTime(string value)
    {
        return BackendDateTimeTool.ResolveEndTimeOrNow(value);
    }
}
