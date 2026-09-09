using System.Collections.Generic;

/// <summary>场景 UI 中英文文案表（仅固定标签；后端内容不翻译）。</summary>
public static class UiLocaleTable
{
    private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
    {
        { UiTextKeys.GjTitle, "告警事件" },
        { UiTextKeys.GjLabelEventName, "事件名称：" },
        { UiTextKeys.GjLabelRiskName, "风险名称：" },
        { UiTextKeys.GjLabelHappenTime, "发生时间：" },
        { UiTextKeys.GjLabelVin, "VIN：" },
        { UiTextKeys.GjLabelPartType, "零部件类型：" },
        { UiTextKeys.GjLabelVehicleInfo, "品牌/车系/车型：" },
        { UiTextKeys.MsgStateProtected, "已防护" },
        { UiTextKeys.MsgStateUnprotected, "未防护" },
    };

    private static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        { UiTextKeys.GjTitle, "Alert Event" },
        { UiTextKeys.GjLabelEventName, "Event Name:" },
        { UiTextKeys.GjLabelRiskName, "Risk Name:" },
        { UiTextKeys.GjLabelHappenTime, "Time:" },
        { UiTextKeys.GjLabelVin, "VIN:" },
        { UiTextKeys.GjLabelPartType, "Part Type:" },
        { UiTextKeys.GjLabelVehicleInfo, "Brand / Series / Model:" },
        { UiTextKeys.MsgStateProtected, "Protected" },
        { UiTextKeys.MsgStateUnprotected, "Unprotected" },
    };

    public static string Get(string key, UiLanguage language)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        Dictionary<string, string> table = language == UiLanguage.English ? En : Zh;
        if (table.TryGetValue(key, out string value))
        {
            return value;
        }

        if (Zh.TryGetValue(key, out string fallback))
        {
            return fallback;
        }

        return key;
    }
}
