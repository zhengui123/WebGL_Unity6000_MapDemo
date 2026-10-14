using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 组装 <see cref="ControlStateTransitionNotify"/>：status=2 时按当前层级填 eventIds。
/// </summary>
public static class ControlStateTransitionNotifyBuilder
{
    private static readonly string[] EmptyEventIds = System.Array.Empty<string>();

    /// <summary>过渡开始通知。</summary>
    public static ControlStateTransitionNotify BuildStarted(
        int fromState,
        int toState,
        string partId,
        int partType = 0)
    {
        return Build(fromState, toState, partId, partType);
    }

    /// <summary>过渡完成通知（from=-1）。</summary>
    public static ControlStateTransitionNotify BuildCompleted(int toState, string partId, int partType = 0)
    {
        return Build(AndroidMessage.ControlStateTransitionCompletedFrom, toState, partId, partType);
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

    private static ControlStateTransitionNotify Build(
        int fromState,
        int toState,
        string partId,
        int partType = 0)
    {
        string provinceCode = ResolveCurrentProvinceCode();
        string vin = ResolveCurrentVin();
        string encryptVin = ResolveCurrentEncryptVin();
        string resolvedPartId = partId ?? string.Empty;
        int resolvedPartType = ResolveNotifyPartType(toState, partType);
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
            partType = resolvedPartType,
            eventIds = ResolveThreatEventIds(
                status,
                toState,
                provinceCode,
                encryptVin,
                vin,
                resolvedPartId,
                resolvedPartType),
        };
    }

    private static int ResolveNotifyPartType(int toState, int partType)
    {
        if (toState != (int)GameManager.ControlState.PartLevel)
        {
            return 0;
        }

        if (PartTypeMatchUtil.IsValidPartType(partType))
        {
            return partType;
        }

        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        return controller != null ? controller.LastPartType : 0;
    }

    /// <summary>
    /// status=2 时按目标级别取威胁 eventId；否则空数组。
    /// 国家=全部；省=当前省；车辆/攻击链路=当前 VIN（优先 encryptVin）；零件=当前停留零件的全部 pending eventId。
    /// </summary>
    private static string[] ResolveThreatEventIds(
        int status,
        int toState,
        string provinceCode,
        string encryptVin,
        string vin,
        string partId,
        int partType = 0)
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
                return CollectActivePartEventIds(partId, partType);
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
    /// 零件级：当前威胁停留绑定的全部 eventId。
    /// 优先 ActivePartEventIds；无则回退该零件 pending 全量（兼容非威胁流程进零件）。
    /// </summary>
    private static string[] CollectActivePartEventIds(string partId, int partType = 0)
    {
        ThreatAlertFlowRunner threatRunner = ThreatAlertFlowRunner.Instance;
        if (threatRunner != null)
        {
            string[] activeIds = threatRunner.ActivePartEventIds;
            if (activeIds != null && activeIds.Length > 0)
            {
                return activeIds;
            }
        }

        return CollectAllPendingEventIdsForPart(partId, partType);
    }

    private static string[] CollectAllPendingEventIdsForPart(string partKey, int partType = 0)
    {
        PartProtectionStatusData data = CarVehicleDataStore.Instance?.PartProtectionStatus?.data;
        string[] fromUnprotected = CollectPendingEventIdsFromParts(data?.unprotectedParts, partKey, partType);
        if (fromUnprotected.Length > 0)
        {
            return fromUnprotected;
        }

        return CollectPendingEventIdsFromParts(data?.protectedParts, partKey, partType);
    }

    private static string[] CollectPendingEventIdsFromParts(
        PartProtectionStatusPart[] parts,
        string partKey,
        int partType = 0)
    {
        if (parts == null || parts.Length == 0)
        {
            return EmptyEventIds;
        }

        bool hasType = PartTypeMatchUtil.IsValidPartType(partType);
        string nameKey = string.IsNullOrWhiteSpace(partKey) ? null : partKey.Trim();

        for (int i = 0; i < parts.Length; i++)
        {
            PartProtectionStatusPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            bool matched = hasType && part.partType == partType;
            if (!matched
                && nameKey != null
                && !string.IsNullOrWhiteSpace(part.partTypeName)
                && string.Equals(part.partTypeName.Trim(), nameKey, System.StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
            }

            if (!matched)
            {
                continue;
            }

            PartProtectionPendingEvent[] pending = part.pendingEvents;
            if (pending == null || pending.Length == 0)
            {
                return EmptyEventIds;
            }

            int count = Mathf.Min(pending.Length, MessageListPanel.MaxMessageCount);
            List<string> ids = new List<string>(count);
            for (int j = 0; j < count; j++)
            {
                PartProtectionPendingEvent evt = pending[j];
                if (evt == null || string.IsNullOrWhiteSpace(evt.eventId))
                {
                    continue;
                }

                ids.Add(evt.eventId.Trim());
            }

            return ids.Count == 0 ? EmptyEventIds : ids.ToArray();
        }

        return EmptyEventIds;
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
