using System;

/// <summary>
/// HTTP 热力点本地缓存：每次接口成功全量替换（先清空再写入）。
/// </summary>
public class HttpVehicleLocationDataStore : UnitySingle<HttpVehicleLocationDataStore>
{
    private LatestVinLocationItem[] _points = Array.Empty<LatestVinLocationItem>();

    /// <summary>数据变更后触发。</summary>
    public event Action DataChanged;

    public int Count => _points.Length;

    /// <summary>清空并以接口响应全量替换本地缓存（旧数据在写入前删除）。</summary>
    public void ReplaceFromResponse(LatestVinLocationResponse response)
    {
        _points = Array.Empty<LatestVinLocationItem>();

        if (response?.data != null && response.data.Length > 0)
        {
            LatestVinLocationItem[] copy = new LatestVinLocationItem[response.data.Length];
            Array.Copy(response.data, copy, response.data.Length);
            _points = copy;
        }

        DataChanged?.Invoke();
    }

    /// <summary>获取当前缓存点位快照。</summary>
    public LatestVinLocationItem[] GetAllPoints()
    {
        if (_points.Length == 0)
        {
            return Array.Empty<LatestVinLocationItem>();
        }

        LatestVinLocationItem[] copy = new LatestVinLocationItem[_points.Length];
        Array.Copy(_points, copy, _points.Length);
        return copy;
    }

    public HttpVehicleLocationRecord[] GetAllRecords()
    {
        if (_points.Length == 0)
        {
            return Array.Empty<HttpVehicleLocationRecord>();
        }

        HttpVehicleLocationRecord[] records = new HttpVehicleLocationRecord[_points.Length];
        for (int i = 0; i < _points.Length; i++)
        {
            records[i] = HttpVehicleLocationRecord.FromApiItem(_points[i]);
        }

        return records;
    }

    public void Clear()
    {
        if (_points.Length == 0)
        {
            return;
        }

        _points = Array.Empty<LatestVinLocationItem>();
        DataChanged?.Invoke();
    }
}
