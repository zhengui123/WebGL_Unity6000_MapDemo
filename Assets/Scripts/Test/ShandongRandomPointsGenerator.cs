using UnityEngine;

/// <summary>
/// WGS84 经纬度点位（度）。
/// </summary>
[System.Serializable]
public struct LongitudeLatitudePoint
{
    public double longitude;
    public double latitude;
}

/// <summary>
/// 在山东省外包矩形内均匀随机生成点位（默认 100 个）。
/// </summary>
public class ShandongRandomPointsGenerator : MonoBehaviour
{
    private const int DefaultPointCount = 100;

    // 山东省近似范围（WGS84，度）：114°19′E–122°43′E，34°22′N–38°24′N
    private const double ShandongLongitudeMin = 114.82;
    private const double ShandongLongitudeMax = 122.72;
    private const double ShandongLatitudeMin = 34.37;
    private const double ShandongLatitudeMax = 38.40;

    [SerializeField] private int _pointCount = DefaultPointCount;
    [SerializeField] private LongitudeLatitudePoint[] _points;

    /// <summary>生成后的点位数组（只读引用）。</summary>
    public LongitudeLatitudePoint[] Points => _points;

    private void Awake()
    {
        GeneratePoints();
    }

    [ContextMenu("生成山东省内随机点位")]
    public void GeneratePoints()
    {
        int count = Mathf.Max(1, _pointCount);
        _points = ShandongGeoRandom.CreatePoints(count);
        Debug.Log($"[ShandongRandomPoints] 已生成 {_points.Length} 个点位。");
    }
}

/// <summary>
/// 山东省范围随机经纬度工具。
/// </summary>
public static class ShandongGeoRandom
{
    private const double LonMin = 114.82;
    private const double LonMax = 122.72;
    private const double LatMin = 34.37;
    private const double LatMax = 38.40;

    public static LongitudeLatitudePoint[] CreatePoints(int count)
    {
        var points = new LongitudeLatitudePoint[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = CreateOne();
        }

        return points;
    }

    public static LongitudeLatitudePoint CreateOne()
    {
        return new LongitudeLatitudePoint
        {
            longitude = LonMin + Random.value * (LonMax - LonMin),
            latitude = LatMin + Random.value * (LatMax - LatMin)
        };
    }
}
