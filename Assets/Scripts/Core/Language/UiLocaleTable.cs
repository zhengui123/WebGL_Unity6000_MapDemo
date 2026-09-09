using System.Collections.Generic;

/// <summary>场景 UI 中英文文案表（仅固定标签；后端内容不翻译）。</summary>
public static class UiLocaleTable
{
    private readonly struct Entry
    {
        public readonly string Zh;
        public readonly string En;

        public Entry(string zh, string en)
        {
            Zh = zh;
            En = en;
        }
    }

    private static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
    {
        { UiTextKeys.GjTitle, new Entry("告警事件", "Alert Event") },
        { UiTextKeys.GjLabelEventName, new Entry("事件名称：", "Event Name:") },
        { UiTextKeys.GjLabelRiskName, new Entry("风险名称：", "Risk Name:") },
        { UiTextKeys.GjLabelHappenTime, new Entry("发生时间：", "Time:") },
        { UiTextKeys.GjLabelVin, new Entry("VIN：", "VIN:") },
        { UiTextKeys.GjLabelPartType, new Entry("零部件类型：", "Part Type:") },
        { UiTextKeys.GjLabelVehicleInfo, new Entry("品牌/车系/车型：", "Brand / Series / Model:") },
        { UiTextKeys.MsgStateProtected, new Entry("已防护", "Protected") },
        { UiTextKeys.MsgStateUnprotected, new Entry("未防护", "Unprotected") },
    };

    public static string Get(string key, UiLanguage language)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (!Table.TryGetValue(key, out Entry entry))
        {
            return key;
        }

        return language == UiLanguage.English ? entry.En : entry.Zh;
    }
}
