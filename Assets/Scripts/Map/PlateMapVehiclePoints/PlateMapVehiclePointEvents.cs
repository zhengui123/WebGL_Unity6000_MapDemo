using System;
using UnityEngine;

/// <summary>点位重建完成后的统计信息。</summary>
public readonly struct PlateMapVehiclePointRebuildInfo
{
    public int RawPointCount { get; }
    public int MergedPointCount { get; }
    public int InstanceCount { get; }
    public int DrawCallCount { get; }
    public int MaxClusterSize { get; }
    public bool Success { get; }

    public PlateMapVehiclePointRebuildInfo(
        int rawPointCount,
        int mergedPointCount,
        int instanceCount,
        int drawCallCount,
        int maxClusterSize,
        bool success)
    {
        RawPointCount = rawPointCount;
        MergedPointCount = mergedPointCount;
        InstanceCount = instanceCount;
        DrawCallCount = drawCallCount;
        MaxClusterSize = maxClusterSize;
        Success = success;
    }
}

public delegate bool PlateMapTryLonLatToLocalHandler(double longitude, double latitude, out Vector3 localPosition);

public delegate void PlateMapGetProvinceBoundsHandler(
    out double westLongitude,
    out double eastLongitude,
    out double southLatitude,
    out double northLatitude);

/// <summary>
/// 板块地图车辆点位事件总线（单例）。PlateMap 模块间仅通过本类注册/触发 Action，不直接引用彼此。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapVehiclePointEvents : UnitySingle<PlateMapVehiclePointEvents>
{
    #region 显示指令（由 PlateMapVehiclePointController 订阅）

    /// <summary>请求替换车辆点位数据。</summary>
    public event Action<VehicleMapPointData[], bool> RequestSetVehiclePoints;

    /// <summary>请求完整重建 GPU 显示。</summary>
    public event Action RequestRebuildPoints;

    /// <summary>请求清空点位并停止绘制。</summary>
    public event Action RequestClearVehiclePoints;

    #endregion

    #region 数据与显示生命周期通知

    public event Action<VehicleMapPointData[]> VehiclePointsWillChange;
    public event Action<VehicleMapPointData[]> VehiclePointsChanged;
    public event Action RebuildStarted;
    public event Action<PlateMapVehiclePointRebuildInfo> RebuildCompleted;
    public event Action Cleared;

    /// <summary>返回 false 时该点不参与合并与 GPU 显示。</summary>
    public Func<VehicleMapPointData, bool> ShouldIncludePoint;

    /// <summary>合并显示前对数据源做变换。</summary>
    public Func<VehicleMapPointData[], VehicleMapPointData[]> TransformPointsBeforeDisplay;

    /// <summary>读取当前点位（由 Controller 注册）。</summary>
    public Func<VehicleMapPointData[]> GetCurrentVehiclePoints;

    #endregion

    #region 地理转换桥接（由 PlateMapGeoConverter 注册）

    public event Action RequestGeoConverterRebuild;

    public Func<bool> IsGeoConverterReady;
    public PlateMapTryLonLatToLocalHandler TryLongitudeLatitudeToLocal;
    public PlateMapGetProvinceBoundsHandler GetProvinceLongitudeLatitudeBounds;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlateMapVehiclePointEvents] 场景中存在多个实例，将销毁重复对象。");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region 发布接口（对外统一入口）

    public void PublishSetVehiclePoints(VehicleMapPointData[] points, bool syncNow = true)
    {
        RequestSetVehiclePoints?.Invoke(points, syncNow);
    }

    public void PublishRebuildPoints()
    {
        RequestRebuildPoints?.Invoke();
    }

    public void PublishClearVehiclePoints()
    {
        RequestClearVehiclePoints?.Invoke();
    }

    public void PublishGeoConverterRebuild()
    {
        RequestGeoConverterRebuild?.Invoke();
    }

    #endregion

    #region 触发与查询（供 Controller / 管线内部调用）

    public void RaiseVehiclePointsWillChange(VehicleMapPointData[] points)
    {
        VehiclePointsWillChange?.Invoke(points);
    }

    public void RaiseVehiclePointsChanged(VehicleMapPointData[] points)
    {
        VehiclePointsChanged?.Invoke(points);
    }

    public void RaiseRebuildStarted()
    {
        RebuildStarted?.Invoke();
    }

    public void RaiseRebuildCompleted(PlateMapVehiclePointRebuildInfo info)
    {
        RebuildCompleted?.Invoke(info);
    }

    public void RaiseCleared()
    {
        Cleared?.Invoke();
    }

    public bool InvokeShouldIncludePoint(VehicleMapPointData data)
    {
        return ShouldIncludePoint == null || ShouldIncludePoint.Invoke(data);
    }

    public VehicleMapPointData[] InvokeTransformPointsBeforeDisplay(VehicleMapPointData[] source)
    {
        if (source == null)
        {
            return null;
        }

        return TransformPointsBeforeDisplay != null
            ? TransformPointsBeforeDisplay.Invoke(source)
            : source;
    }

    public bool InvokeTryLongitudeLatitudeToLocal(double longitude, double latitude, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (TryLongitudeLatitudeToLocal == null)
        {
            return false;
        }

        return TryLongitudeLatitudeToLocal.Invoke(longitude, latitude, out localPosition);
    }

    public bool InvokeIsGeoConverterReady()
    {
        return IsGeoConverterReady != null && IsGeoConverterReady.Invoke();
    }

    public bool InvokeGetProvinceLongitudeLatitudeBounds(
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = eastLongitude = southLatitude = northLatitude = 0;
        if (GetProvinceLongitudeLatitudeBounds == null)
        {
            return false;
        }

        GetProvinceLongitudeLatitudeBounds.Invoke(
            out westLongitude, out eastLongitude, out southLatitude, out northLatitude);
        return true;
    }

    public VehicleMapPointData[] InvokeGetCurrentVehiclePoints()
    {
        return GetCurrentVehiclePoints != null ? GetCurrentVehiclePoints.Invoke() : null;
    }

    #endregion
}
