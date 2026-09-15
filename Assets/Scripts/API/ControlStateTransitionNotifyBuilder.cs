using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 组装 <see cref="ControlStateTransitionNotify"/>：status=2 时按当前层级填 eventIds。
/// </summary>
public static class ControlStateTransitionNotifyBuilder
{
    private static readonly string[] EmptyEventIds = System.Array.Empty<string>();

    /// <summary>过渡开始通知。</summary>
    public static ControlStateTransitionNotify BuildStarted(int fromState, int toState, string partId)
    {
        return Build(fromState, toState, partId);
    }

    /// <summary>过渡完成通知（from=-1）。</summary>
    public static ControlStateTransitionNotify BuildCompleted(int toState, string partId)
    {
        return Build(AndroidMessage.ControlStateTransitionCompletedFrom, toState, partId);
    }

    /// <summary>日志用：空数组显示 []，否则显示条数与 id 列表。</summary>
    public static string FormatEventIdsForLog(string[] eventIds)
    {
        if (eventIds == null || eventIds.Length == 0)
        {
            return "[]";
        }

        return $"[{eventIds.Length}] {string.Join(",", eventIds)}";
    }

    private static ControlStateTransitionNotify Build(int fromState, int toState, string partId)
    {
        string provinceCode = ResolveCurrentProvinceCode();
        string vin = ResolveCurrentVin();
        string resolvedPartId = partId ?? string.Empty;
        int status = ResolveNotifyBigScreenStatus();
        return new ControlStateTransitionNotify
        {
            from = fromState,
            to = toState,
            status = status,
            provinceCode = provinceCode,
            vin = vin,
            partId = resolvedPartId,
            eventIds = ResolveThreatEventIds(status, toState, provinceCode, vin, resolvedPartId),
        };
    }

    /// <summary>
    /// status=2 时按目标级别取威胁 eventId；否则空数组。
    /// 国家=全部；省=当前省；车辆/攻击链路=当前 VIN；零件=车辆态势当前零件 pendingEvents。
    /// </summary>
    private static string[] ResolveThreatEventIds(
        int status,
        int toState,
        string provinceCode,
        string vin,
        string partId)
    {
        if (status != (int)GameManager.BigScreenPlaybackState.Threat)
        {
            return EmptyEventIds;
        }

        switch ((GameManager.ControlState)toState)
        {
            case GameManager.ControlState.CountryLevel:
                return CollectHighRiskEventIds(HighRiskSecurityEventDataStore.Instance?.GetAllEvents());
            case GameManager.ControlState.ProvinceLevel:
                return CollectHighRiskEventIdsByProvince(provinceCode);
            case GameManager.ControlState.VehicleLevel:
            case GameManager.ControlState.AttackPathLevel:
                return CollectHighRiskEventIdsByVin(vin);
            case GameManager.ControlState.PartLevel:
                return CollectPartProtectionEventIds(partId);
            default:
                return EmptyEventIds;
        }
    }

    private static string[] CollectHighRiskEventIds(IReadOnlyList<HighRiskSecurityEventItem> events)
    {
        if (events == null || events.Count == 0)
        {
            return EmptyEventIds;
        }

        List<string> ids = new List<string>(events.Count);
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || string.IsNullOrWhiteSpace(item.eventId))
            {
                continue;
            }

            ids.Add(item.eventId.Trim());
        }

        return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
    }

    /// <summary>
    /// 按威胁条目 <see cref="HighRiskSecurityEventItem.province"/> 过滤。
    /// 不用 GetEventsByProvince：规范化失败时会直接空，漏掉 province 原始值能对上的条目。
    /// </summary>
    private static string[] CollectHighRiskEventIdsByProvince(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return EmptyEventIds;
        }

        IReadOnlyList<HighRiskSecurityEventItem> events = HighRiskSecurityEventDataStore.Instance?.GetAllEvents();
        if (events == null || events.Count == 0)
        {
            return EmptyEventIds;
        }

        string provinceKey = provinceCode.Trim();
        bool hasNormalizedQuery = PlateMapBoundaryDatabase.TryNormalizeProvinceCode(
            provinceKey,
            out string normalizedQuery);
        List<string> ids = new List<string>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null
                || string.IsNullOrWhiteSpace(item.eventId)
                || !MatchesProvinceField(item.province, provinceKey, hasNormalizedQuery, normalizedQuery))
            {
                continue;
            }

            ids.Add(item.eventId.Trim());
        }

        return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
    }

    private static bool MatchesProvinceField(
        string itemProvince,
        string provinceKey,
        bool hasNormalizedQuery,
        string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(itemProvince))
        {
            return false;
        }

        string itemKey = itemProvince.Trim();
        if (string.Equals(itemKey, provinceKey, System.StringComparison.Ordinal))
        {
            return true;
        }

        if (!hasNormalizedQuery)
        {
            return false;
        }

        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(itemKey, out string normalizedItem))
        {
            return false;
        }

        return string.Equals(normalizedItem, normalizedQuery, System.StringComparison.Ordinal);
    }

    private static string[] CollectHighRiskEventIdsByVin(string vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return EmptyEventIds;
        }

        IReadOnlyList<HighRiskSecurityEventItem> events = HighRiskSecurityEventDataStore.Instance?.GetAllEvents();
        if (events == null || events.Count == 0)
        {
            return EmptyEventIds;
        }

        string vinKey = vin.Trim();
        List<string> ids = new List<string>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null
                || string.IsNullOrWhiteSpace(item.eventId)
                || string.IsNullOrWhiteSpace(item.vin)
                || !string.Equals(item.vin.Trim(), vinKey, System.StringComparison.Ordinal))
            {
                continue;
            }

            ids.Add(item.eventId.Trim());
        }

        return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
    }

    /// <summary>当前零件在防护状态中的 pendingEvents.eventId（先未防护再已防护，与轮播顺序一致）。</summary>
    private static string[] CollectPartProtectionEventIds(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return EmptyEventIds;
        }

        PartProtectionStatusData data = CarVehicleDataStore.Instance?.PartProtectionStatus?.data;
        if (data == null)
        {
            return EmptyEventIds;
        }

        string partKey = partId.Trim();
        List<string> ids = new List<string>();
        AppendPartPendingEventIds(ids, data.unprotectedParts, partKey);
        AppendPartPendingEventIds(ids, data.protectedParts, partKey);
        return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
    }

    private static void AppendPartPendingEventIds(
        List<string> ids,
        PartProtectionStatusPart[] parts,
        string partKey)
    {
        if (parts == null || parts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartProtectionStatusPart part = parts[i];
            if (part == null
                || string.IsNullOrWhiteSpace(part.partTypeName)
                || !string.Equals(part.partTypeName.Trim(), partKey, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PartProtectionPendingEvent[] pending = part.pendingEvents;
            if (pending == null)
            {
                continue;
            }

            for (int j = 0; j < pending.Length; j++)
            {
                PartProtectionPendingEvent evt = pending[j];
                if (evt == null || string.IsNullOrWhiteSpace(evt.eventId))
                {
                    continue;
                }

                ids.Add(evt.eventId.Trim());
            }
        }
    }

    private static string ResolveCurrentProvinceCode()
    {
        ThreatAlertFlowRunner threatRunner = ThreatAlertFlowRunner.Instance;
        if (threatRunner != null && !string.IsNullOrWhiteSpace(threatRunner.ActiveProvinceCode))
        {
            return threatRunner.ActiveProvinceCode;
        }

        if (PlateProvinceFocusResolver.TryGetFocusedPlateProvinceCode(out string focusedCode) &&
            !string.IsNullOrWhiteSpace(focusedCode))
        {
            return focusedCode;
        }

        if (PlateProvinceFocusResolver.TryGetCachedProvinceCode(out string cached) &&
            !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        if (GameManager.Instance != null)
        {
            string code = GameManager.Instance.ResolveProvinceCode(null);
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }
        }

        if (MapApi.Instance != null)
        {
            string code = MapApi.Instance.GetDefaultProvinceCode();
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code;
        }

        return string.Empty;
    }

    private static string ResolveCurrentVin()
    {
        ThreatAlertFlowRunner threatRunner = ThreatAlertFlowRunner.Instance;
        if (threatRunner != null && !string.IsNullOrWhiteSpace(threatRunner.ActiveEncryptVin))
        {
            return threatRunner.ActiveEncryptVin;
        }

        CarVehicleDataStore store = CarVehicleDataStore.Instance;
        if (store == null || string.IsNullOrWhiteSpace(store.LastEncryptVin))
        {
            return string.Empty;
        }

        return store.LastEncryptVin;
    }

    private static int ResolveNotifyBigScreenStatus()
    {
        GameManager gm = GameManager.Instance;
        return gm != null
            ? (int)gm.CurrentPlaybackState
            : (int)GameManager.BigScreenPlaybackState.Default;
    }
}
