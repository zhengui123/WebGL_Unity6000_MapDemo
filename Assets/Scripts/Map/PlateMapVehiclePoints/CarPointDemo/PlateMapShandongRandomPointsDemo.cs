using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 山东省内测试点位随机生成（Demo）。仅通过 <see cref="PlateMapVehiclePointEvents"/> 单例与显示管线通信。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapShandongRandomPointsDemo : MonoBehaviour
{
    [Header("省界与采样")]
    [SerializeField] private PlateMapShandongProvincePointFilter _provinceFilter = new PlateMapShandongProvincePointFilter();

    [Header("随机生成")]
    [SerializeField] private int _randomGenerateCount = 100;
    [SerializeField] private int _randomSeed;

    [Header("事件")]
    [Tooltip("订阅 RebuildCompleted 并在控制台输出统计")]
    [SerializeField] private bool _logRebuildCompleted;

    private PlateMapVehiclePointEvents Hub => PlateMapVehiclePointEvents.Instance;

    private void OnEnable()
    {
        BindEventHandlers();
    }

    private void OnDisable()
    {
        UnbindEventHandlers();
    }

    private void BindEventHandlers()
    {
        PlateMapVehiclePointEvents hub = Hub;
        hub.ShouldIncludePoint = ShouldIncludePointForDisplay;
        if (_logRebuildCompleted)
        {
            hub.RebuildCompleted += OnRebuildCompleted;
        }
    }

    private void UnbindEventHandlers()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }
        hub.ShouldIncludePoint = null;
        hub.RebuildCompleted -= OnRebuildCompleted;
    }

    private bool ShouldIncludePointForDisplay(VehicleMapPointData data)
    {
        if (!_provinceFilter.StrictProvinceBoundary)
        {
            return true;
        }

        return _provinceFilter.Contains(data.longitude, data.latitude);
    }

    private void OnRebuildCompleted(PlateMapVehiclePointRebuildInfo info)
    {
        Debug.Log(
            $"[PlateMapShandongRandomPointsDemo] 重建完成 | 原始 {info.RawPointCount} → 合并 {info.MergedPointCount} | " +
            $"实例 {info.InstanceCount} DrawCall≈{info.DrawCallCount} | 成功={info.Success}");
    }

    [ContextMenu("随机生成100个山东省内点位")]
    public void GenerateRandomVehiclePointsInShandongMenu()
    {
        GenerateRandomVehiclePointsInShandong(100);
    }

    /// <summary>在山东省范围内随机生成车辆点位，经事件总线写入并重建显示。</summary>
    public void GenerateRandomVehiclePointsInShandong(int count = -1)
    {
        if (count <= 0)
        {
            count = _randomGenerateCount;
        }

        if (_provinceFilter.StrictProvinceBoundary && !_provinceFilter.EnsureProvinceBoundaryLoaded())
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 省界数据未加载，无法严格生成省内点位。");
            return;
        }

        Hub.PublishGeoConverterRebuild();

        System.Random rng = _randomSeed != 0 ? new System.Random(_randomSeed) : new System.Random();
        var list = new List<VehicleMapPointData>(count);
        int failed = 0;

        for (int i = 0; i < count; i++)
        {
            if (!_provinceFilter.TrySampleRandomLongitudeLatitude(rng, out double lon, out double lat))
            {
                failed++;
                continue;
            }

            list.Add(new VehicleMapPointData
            {
                vehicleId = $"SD-{list.Count + 1:D3}",
                longitude = lon,
                latitude = lat,
                alertValue = (float)rng.NextDouble()
            });
        }

        VehicleMapPointData[] points = list.ToArray();
        Hub.PublishSetVehiclePoints(points, syncNow: true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        Debug.Log(
            $"[PlateMapShandongRandomPointsDemo] 已生成 {points.Length}/{count} 个省内点位（失败 {failed} 次）。");
    }

    [ContextMenu("按省界过滤当前点位")]
    public void FilterControllerPointsByProvinceBoundary()
    {
        Hub.PublishRebuildPoints();
        Debug.Log("[PlateMapShandongRandomPointsDemo] 已按 ShouldIncludePoint 省界规则重建显示（源数据未改写）。");
    }
}
