using System;
using System.Collections.Generic;

/// <summary>
/// HTTP 车辆位置数据接收与本地缓存：多次调用时按 VinEncrypt 合并更新。
/// </summary>
public class HttpVehicleLocationDataStore : UnitySingle<HttpVehicleLocationDataStore>
{
    private readonly Dictionary<string, HttpVehicleLocationRecord> _recordsByVinEncrypt =
        new Dictionary<string, HttpVehicleLocationRecord>();

    /// <summary>数据变更后触发（新增或更新）。</summary>
    public event Action DataChanged;

    public int Count => _recordsByVinEncrypt.Count;

    /// <summary>合并接口响应；相同 VinEncrypt 覆盖旧记录。</summary>
    public int MergeFromResponse(LatestVinLocationResponse response)
    {
        if (response == null || response.data == null || response.data.Length == 0)
        {
            return 0;
        }

        return MergeItems(response.data);
    }

    /// <summary>合并车辆列表；返回本次新增或更新的条数。</summary>
    public int MergeItems(LatestVinLocationItem[] items)
    {
        if (items == null || items.Length == 0)
        {
            return 0;
        }

        int changedCount = 0;
        for (int i = 0; i < items.Length; i++)
        {
            LatestVinLocationItem item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.vinEncrypt))
            {
                continue;
            }

            HttpVehicleLocationRecord record = HttpVehicleLocationRecord.FromApiItem(item);
            if (record == null)
            {
                continue;
            }

            string key = item.vinEncrypt.Trim();
            _recordsByVinEncrypt[key] = record;
            changedCount++;
        }

        if (changedCount > 0)
        {
            DataChanged?.Invoke();
        }

        return changedCount;
    }

    public bool TryGetByVinEncrypt(string vinEncrypt, out HttpVehicleLocationRecord record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(vinEncrypt))
        {
            return false;
        }

        return _recordsByVinEncrypt.TryGetValue(vinEncrypt.Trim(), out record);
    }

    public HttpVehicleLocationRecord[] GetAllRecords()
    {
        if (_recordsByVinEncrypt.Count == 0)
        {
            return Array.Empty<HttpVehicleLocationRecord>();
        }

        HttpVehicleLocationRecord[] records = new HttpVehicleLocationRecord[_recordsByVinEncrypt.Count];
        _recordsByVinEncrypt.Values.CopyTo(records, 0);
        return records;
    }

    public void Clear()
    {
        if (_recordsByVinEncrypt.Count == 0)
        {
            return;
        }

        _recordsByVinEncrypt.Clear();
        DataChanged?.Invoke();
    }
}
