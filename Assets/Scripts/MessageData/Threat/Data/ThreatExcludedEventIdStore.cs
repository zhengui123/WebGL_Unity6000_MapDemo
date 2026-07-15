using System.Collections.Generic;

/// <summary>
/// 已被威胁流程删除的告警 eventId；接口再次入库时跳过这些 ID。
/// </summary>
public static class ThreatExcludedEventIdStore
{
    private static readonly HashSet<string> ExcludedEventIds = new HashSet<string>();

    public static int Count => ExcludedEventIds.Count;

    public static bool Contains(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        return ExcludedEventIds.Contains(eventId.Trim());
    }

    public static void Add(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        ExcludedEventIds.Add(eventId.Trim());
    }

    public static void AddRange(IEnumerable<HighRiskSecurityEventItem> events)
    {
        if (events == null)
        {
            return;
        }

        foreach (HighRiskSecurityEventItem item in events)
        {
            if (item != null)
            {
                Add(item.eventId);
            }
        }
    }

    public static void Clear()
    {
        ExcludedEventIds.Clear();
    }
}
