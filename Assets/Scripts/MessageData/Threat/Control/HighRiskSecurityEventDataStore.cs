using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 高危安全事件接口结果缓存：全量列表 + 按省级 code 分组。
/// 入库时跳过 <see cref="ThreatExcludedEventIdStore"/> 中已排除的 eventId。
/// </summary>
public class HighRiskSecurityEventDataStore : UnitySingle<HighRiskSecurityEventDataStore>
{
    private readonly List<HighRiskSecurityEventItem> _events = new List<HighRiskSecurityEventItem>();
    private readonly Dictionary<string, List<HighRiskSecurityEventItem>> _eventsByProvince =
        new Dictionary<string, List<HighRiskSecurityEventItem>>();
    private readonly List<string> _qualifiedProvinceBuffer = new List<string>(8);

    private HighRiskSecurityEventResponse _lastResponse;

    /// <summary>数据更新后触发。</summary>
    public event Action DataChanged;

    public int Count => _events.Count;

    public int ProvinceGroupCount => _eventsByProvince.Count;

    public HighRiskSecurityEventResponse LastResponse => _lastResponse;

    /// <summary>开始国外分批拉取前清空缓存。</summary>
    public void BeginBatch()
    {
        _events.Clear();
        _eventsByProvince.Clear();
        _lastResponse = null;
    }

    /// <summary>以接口响应全量替换本地缓存（单次请求场景）。</summary>
    public void ReplaceFromResponse(HighRiskSecurityEventResponse response)
    {
        _lastResponse = response;
        _events.Clear();
        _eventsByProvince.Clear();

        if (response?.data != null && response.data.Length > 0)
        {
            for (int i = 0; i < response.data.Length; i++)
            {
                AddEventInternal(response.data[i], null);
            }
        }

        DataChanged?.Invoke();
    }

    /// <summary>合并单省（或单区域）接口响应；会先移除该省旧数据再写入新数据。</summary>
    public void MergeProvinceResponse(string provinceCode, HighRiskSecurityEventResponse response)
    {
        _lastResponse = response;
        RemoveProvinceEventsInternal(provinceCode, invokeChanged: false);

        if (response?.data != null && response.data.Length > 0)
        {
            string fallbackProvinceCode = NormalizeProvinceCodeOrNull(provinceCode);
            for (int i = 0; i < response.data.Length; i++)
            {
                AddEventInternal(response.data[i], fallbackProvinceCode);
            }
        }

        DataChanged?.Invoke();
    }

    public IReadOnlyList<HighRiskSecurityEventItem> GetAllEvents()
    {
        return _events;
    }

    public IReadOnlyList<HighRiskSecurityEventItem> GetEventsByProvince(string provinceCode)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return Array.Empty<HighRiskSecurityEventItem>();
        }

        if (!_eventsByProvince.TryGetValue(normalizedCode, out List<HighRiskSecurityEventItem> events))
        {
            return Array.Empty<HighRiskSecurityEventItem>();
        }

        return events;
    }

    public int GetProvinceEventCount(string provinceCode)
    {
        return GetEventsByProvince(provinceCode).Count;
    }

    public IReadOnlyList<string> GetProvincesMeetingThreshold(int threshold)
    {
        _qualifiedProvinceBuffer.Clear();
        foreach (KeyValuePair<string, List<HighRiskSecurityEventItem>> pair in _eventsByProvince)
        {
            if (pair.Value != null && pair.Value.Count >= threshold)
            {
                _qualifiedProvinceBuffer.Add(pair.Key);
            }
        }

        _qualifiedProvinceBuffer.Sort(StringComparer.Ordinal);
        return _qualifiedProvinceBuffer;
    }

    public bool RemoveProvinceEvents(string provinceCode)
    {
        bool removed = RemoveProvinceEventsInternal(provinceCode, invokeChanged: true);
        return removed;
    }

    public void Clear()
    {
        if (_events.Count == 0 && _lastResponse == null && _eventsByProvince.Count == 0)
        {
            return;
        }

        _events.Clear();
        _eventsByProvince.Clear();
        _lastResponse = null;
        DataChanged?.Invoke();
    }

    private bool RemoveProvinceEventsInternal(string provinceCode, bool invokeChanged)
    {
        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return false;
        }

        if (!_eventsByProvince.TryGetValue(normalizedCode, out List<HighRiskSecurityEventItem> provinceEvents) ||
            provinceEvents == null ||
            provinceEvents.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < provinceEvents.Count; i++)
        {
            HighRiskSecurityEventItem item = provinceEvents[i];
            if (item != null)
            {
                _events.Remove(item);
            }
        }

        _eventsByProvince.Remove(normalizedCode);
        if (invokeChanged)
        {
            DataChanged?.Invoke();
        }

        return true;
    }

    /// <summary>删除该省数据，并将其 eventId 记入排除表（后续接口入库跳过）。</summary>
    public bool RemoveProvinceEventsAndExclude(string provinceCode)
    {
        IReadOnlyList<HighRiskSecurityEventItem> events = GetEventsByProvince(provinceCode);
        ThreatExcludedEventIdStore.AddRange(events);
        return RemoveProvinceEvents(provinceCode);
    }

    private void AddEventInternal(HighRiskSecurityEventItem item, string fallbackProvinceCode)
    {
        if (item == null)
        {
            return;
        }

        if (ThreatExcludedEventIdStore.Contains(item.eventId))
        {
            return;
        }

        _events.Add(item);

        if (!TryResolveProvinceCode(item, out string provinceCode))
        {
            provinceCode = fallbackProvinceCode;
        }

        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return;
        }

        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return;
        }

        if (!_eventsByProvince.TryGetValue(normalizedCode, out List<HighRiskSecurityEventItem> provinceEvents))
        {
            provinceEvents = new List<HighRiskSecurityEventItem>();
            _eventsByProvince.Add(normalizedCode, provinceEvents);
        }

        provinceEvents.Add(item);
    }

    private static string NormalizeProvinceCodeOrNull(string provinceCode)
    {
        return PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalizedCode)
            ? normalizedCode
            : null;
    }

    private static bool TryResolveProvinceCode(HighRiskSecurityEventItem item, out string provinceCode)
    {
        provinceCode = null;
        if (item == null)
        {
            return false;
        }

        return PlateMapBoundaryDatabase.TryNormalizeProvinceCode(item.province, out provinceCode);
    }
}
