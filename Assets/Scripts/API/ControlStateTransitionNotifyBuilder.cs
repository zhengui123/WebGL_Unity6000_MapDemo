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
        string encryptVin = ResolveCurrentEncryptVin();
        string resolvedPartId = partId ?? string.Empty;
        int status = ResolveNotifyBigScreenStatus();
        return new ControlStateTransitionNotify
        {
            from = fromState,
            to = toState,
            status = status,
            provinceCode = provinceCode,
            vin = vin,
            encryptVin = encryptVin,
            partId = resolvedPartId,
            eventIds = ResolveThreatEventIds(
                status,
                toState,
                provinceCode,
                encryptVin,
                vin,
                resolvedPartId),
        };
    }

    /// <summary>
    /// status=2 时按目标级别取威胁 eventId；否则空数组。
    /// 国家=全部；省=当前省；车辆/攻击链路=当前 VIN（优先 encryptVin）；零件=当前停留绑定的单个 eventId。
    /// </summary>
    private static string[] ResolveThreatEventIds(
        int status,
        int toState,
        string provinceCode,
        string encryptVin,
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
                return CollectHighRiskEventIdsByVin(encryptVin, vin);
            case GameManager.ControlState.PartLevel:
                return CollectActivePartEventId(partId);
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

    private static string[] CollectHighRiskEventIdsByVin(string encryptVin, string vin)
    {
        string encryptKey = string.IsNullOrWhiteSpace(encryptVin) ? string.Empty : encryptVin.Trim();
        string vinKey = string.IsNullOrWhiteSpace(vin) ? string.Empty : vin.Trim();
        if (encryptKey.Length == 0 && vinKey.Length == 0)
        {
            return EmptyEventIds;
        }

        IReadOnlyList<HighRiskSecurityEventItem> events = HighRiskSecurityEventDataStore.Instance?.GetAllEvents();
        if (events == null || events.Count == 0)
        {
            return EmptyEventIds;
        }

        List<string> ids = new List<string>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || string.IsNullOrWhiteSpace(item.eventId))
            {
                continue;
            }

            string itemKey = item.PreferEncryptVin();
            bool matched = encryptKey.Length > 0
                && itemKey.Length > 0
                && string.Equals(itemKey, encryptKey, System.StringComparison.Ordinal);
            if (!matched
                && vinKey.Length > 0
                && !string.IsNullOrWhiteSpace(item.vin)
                && string.Equals(item.vin.Trim(), vinKey, System.StringComparison.Ordinal))
            {
                matched = true;
            }

            if (!matched)
            {
                continue;
            }

            ids.Add(item.eventId.Trim());
        }

        return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
    }

    /// <summary>
    /// 零件级：仅当前威胁停留绑定的单个 eventId。
    /// 优先 ActivePartEventId；无则回退该零件 pending 首条（兼容非威胁流程进零件）。
    /// </summary>
    private static string[] CollectActivePartEventId(string partId)
    {
        ThreatAlertFlowRunner threatRunner = ThreatAlertFlowRunner.Instance;
        if (threatRunner != null && !string.IsNullOrWhiteSpace(threatRunner.ActivePartEventId))
        {
            return new[] { threatRunner.ActivePartEventId.Trim() };
        }

        if (string.IsNullOrWhiteSpace(partId))
        {
            return EmptyEventIds;
        }

        string firstId = FindFirstPendingEventIdForPart(partId.Trim());
        return string.IsNullOrEmpty(firstId) ? EmptyEventIds : new[] { firstId };
    }

    private static string FindFirstPendingEventIdForPart(string partKey)
    {
        PartProtectionStatusData data = CarVehicleDataStore.Instance?.PartProtectionStatus?.data;
        string fromUnprotected = FindFirstPendingEventId(data?.unprotectedParts, partKey);
        if (!string.IsNullOrEmpty(fromUnprotected))
        {
            return fromUnprotected;
        }

        return FindFirstPendingEventId(data?.protectedParts, partKey);
    }

    private static string FindFirstPendingEventId(PartProtectionStatusPart[] parts, string partKey)
    {
        if (parts == null || parts.Length == 0)
        {
            return string.Empty;
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
                return string.Empty;
            }

            for (int j = 0; j < pending.Length; j++)
            {
                PartProtectionPendingEvent evt = pending[j];
                if (evt != null && !string.IsNullOrWhiteSpace(evt.eventId))
                {
                    return evt.eventId.Trim();
                }
            }

            return string.Empty;
        }

        return string.Empty;
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

    /// <summary>明文 VIN：威胁下钻缓存优先；无则空（不把 encryptVin 冒充 vin）。</summary>
    private static string ResolveCurrentVin()
    {
        ThreatAlertFlowRunner threatRunner = ThreatAlertFlowRunner.Instance;
        if (threatRunner != null && !string.IsNullOrWhiteSpace(threatRunner.ActiveVin))
        {
            return threatRunner.ActiveVin;
        }

        return string.Empty;
    }

    /// <summary>加密 VIN：威胁下钻缓存优先，否则回落车辆请求缓存 LastEncryptVin。</summary>
    private static string ResolveCurrentEncryptVin()
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
