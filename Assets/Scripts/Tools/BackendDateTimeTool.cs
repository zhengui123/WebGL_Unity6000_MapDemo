using System;
using System.Globalization;

/// <summary>
/// 后端接口用时间字符串工具（格式与综合态势 POST 参数一致，如 2026-06-30 23:00:00）。
/// </summary>
public static class BackendDateTimeTool
{
    /// <summary>后端 date-time 格式：yyyy-MM-dd HH:mm:ss。</summary>
    public const string BackendDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>获取当前本地时间的后端参数字符串。</summary>
    public static string GetCurrentTimeString()
    {
        return Format(DateTime.Now);
    }

    /// <summary>将 <see cref="DateTime"/> 格式化为后端参数字符串。</summary>
    public static string Format(DateTime dateTime)
    {
        return dateTime.ToString(BackendDateTimeFormat, Invariant);
    }

    /// <summary>尝试解析后端 date-time 字符串。</summary>
    public static bool TryParse(string value, out DateTime dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParseExact(
            value.Trim(),
            BackendDateTimeFormat,
            Invariant,
            DateTimeStyles.None,
            out dateTime);
    }
}
