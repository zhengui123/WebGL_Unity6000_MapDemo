using System;
using System.Collections.Generic;
using UnityEngine;

public delegate void PlateMapSetVehiclePointsAction(VehicleMapPointData[] points, bool syncNow);
public delegate VehicleMapPointData[] PlateMapGetCurrentVehiclePointsAction();
public delegate bool PlateMapShouldIncludePointAction(VehicleMapPointData data);
public delegate VehicleMapPointData[] PlateMapTransformPointsBeforeDisplayAction(VehicleMapPointData[] source);

public delegate void PlateMapGeoConverterRebuildAction();
public delegate bool PlateMapIsGeoConverterReadyAction();
public delegate bool PlateMapTryLonLatToLocalAction(double longitude, double latitude, out Vector3 localPosition);
public delegate void PlateMapGetProvinceBoundsAction(
    out double westLongitude,
    out double eastLongitude,
    out double southLatitude,
    out double northLatitude);

/// <summary>
/// 板块地图车辆点位事件总线（单例）。按板块 GameObject 名称注册/分发，key 为 <c>gameObject.name</c>。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapVehiclePointEvents : UnitySingle<PlateMapVehiclePointEvents>
{
    /// <summary>单板块全部回调，合并原 7 个独立字典。</summary>
    private sealed class PlateHandlers
    {
        public PlateMapSetVehiclePointsAction SetVehiclePoints;
        public PlateMapGetCurrentVehiclePointsAction GetCurrentVehiclePoints;
        public PlateMapShouldIncludePointAction ShouldIncludePoint;
        public PlateMapTransformPointsBeforeDisplayAction TransformBeforeDisplay;
        public PlateMapGeoConverterRebuildAction GeoConverterRebuild;
        public PlateMapIsGeoConverterReadyAction IsGeoConverterReady;
        public PlateMapTryLonLatToLocalAction TryLonLatToLocal;
        public PlateMapGetProvinceBoundsAction GetProvinceBounds;

        public bool IsEmpty =>
            SetVehiclePoints == null &&
            GetCurrentVehiclePoints == null &&
            ShouldIncludePoint == null &&
            TransformBeforeDisplay == null &&
            GeoConverterRebuild == null &&
            IsGeoConverterReady == null &&
            TryLonLatToLocal == null &&
            GetProvinceBounds == null;
    }

    private readonly Dictionary<string, PlateHandlers> _plates = new();

    public event Action<string, VehicleMapPointData[]> VehiclePointsWillChangeAction;
    public event Action<string, VehicleMapPointData[]> VehiclePointsChangedAction;
    public event Action<string> RebuildStartedAction;

    #region 注册 / 注销（板块名为 gameObject.name）

    public void RegisterSetVehiclePointsAction(string plateMapName, PlateMapSetVehiclePointsAction action)
    {
        GetOrCreateHandlers(plateMapName).SetVehiclePoints = action;
    }

    public void UnregisterSetVehiclePointsAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.SetVehiclePoints = null);
    }

    public void RegisterGetCurrentVehiclePointsAction(string plateMapName, PlateMapGetCurrentVehiclePointsAction action)
    {
        GetOrCreateHandlers(plateMapName).GetCurrentVehiclePoints = action;
    }

    public void UnregisterGetCurrentVehiclePointsAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.GetCurrentVehiclePoints = null);
    }

    public void RegisterShouldIncludePointAction(string plateMapName, PlateMapShouldIncludePointAction action)
    {
        GetOrCreateHandlers(plateMapName).ShouldIncludePoint = action;
    }

    public void UnregisterShouldIncludePointAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.ShouldIncludePoint = null);
    }

    public void RegisterTransformPointsBeforeDisplayAction(
        string plateMapName,
        PlateMapTransformPointsBeforeDisplayAction action)
    {
        GetOrCreateHandlers(plateMapName).TransformBeforeDisplay = action;
    }

    public void UnregisterTransformPointsBeforeDisplayAction(string plateMapName)
    {
        ClearHandler(plateMapName, h => h.TransformBeforeDisplay = null);
    }

    public void RegisterGeoConverterActions(
        string plateMapName,
        PlateMapGeoConverterRebuildAction rebuildAction,
        PlateMapIsGeoConverterReadyAction readyAction,
        PlateMapTryLonLatToLocalAction lonLatToLocalAction,
        PlateMapGetProvinceBoundsAction boundsAction)
    {
        PlateHandlers handlers = GetOrCreateHandlers(plateMapName);
        handlers.GeoConverterRebuild = rebuildAction;
        handlers.IsGeoConverterReady = readyAction;
        handlers.TryLonLatToLocal = lonLatToLocalAction;
        handlers.GetProvinceBounds = boundsAction;
    }

    public void UnregisterGeoConverterActions(string plateMapName)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            return;
        }

        handlers.GeoConverterRebuild = null;
        handlers.IsGeoConverterReady = null;
        handlers.TryLonLatToLocal = null;
        handlers.GetProvinceBounds = null;
        RemoveIfEmpty(plateMapName, handlers);
    }

    #endregion

    #region 对外发布

    public bool PublishSetVehiclePoints(string plateMapName, VehicleMapPointData[] points, bool syncNow = true)
    {
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.SetVehiclePoints == null)
        {
            Debug.LogWarning($"[PlateMapVehiclePointEvents] 未注册板块「{plateMapName}」的 SetVehiclePointsAction。");
            return false;
        }

        handlers.SetVehiclePoints.Invoke(points, syncNow);
        return true;
    }

    public void PublishGeoConverterRebuild(string plateMapName)
    {
        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.GeoConverterRebuild != null)
        {
            handlers.GeoConverterRebuild.Invoke();
        }
    }

    #endregion

    #region 触发与查询

    public void RaiseVehiclePointsWillChange(string plateMapName, VehicleMapPointData[] points)
    {
        VehiclePointsWillChangeAction?.Invoke(plateMapName, points);
    }

    public void RaiseVehiclePointsChanged(string plateMapName, VehicleMapPointData[] points)
    {
        VehiclePointsChangedAction?.Invoke(plateMapName, points);
    }

    public void RaiseRebuildStarted(string plateMapName)
    {
        RebuildStartedAction?.Invoke(plateMapName);
    }

    public bool InvokeShouldIncludePoint(string plateMapName, VehicleMapPointData data)
    {
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.ShouldIncludePoint == null)
        {
            return true;
        }

        return handlers.ShouldIncludePoint.Invoke(data);
    }

    public VehicleMapPointData[] InvokeTransformPointsBeforeDisplay(string plateMapName, VehicleMapPointData[] source)
    {
        if (source == null)
        {
            return null;
        }

        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.TransformBeforeDisplay != null)
        {
            return handlers.TransformBeforeDisplay.Invoke(source);
        }

        return source;
    }

    public bool InvokeTryLongitudeLatitudeToLocal(
        string plateMapName,
        double longitude,
        double latitude,
        out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.TryLonLatToLocal == null)
        {
            return false;
        }

        return handlers.TryLonLatToLocal.Invoke(longitude, latitude, out localPosition);
    }

    public bool InvokeIsGeoConverterReady(string plateMapName)
    {
        return TryGetHandlers(plateMapName, out PlateHandlers handlers) &&
               handlers.IsGeoConverterReady != null &&
               handlers.IsGeoConverterReady.Invoke();
    }

    public bool InvokeGetProvinceLongitudeLatitudeBounds(
        string plateMapName,
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = eastLongitude = southLatitude = northLatitude = 0;
        if (!TryGetHandlers(plateMapName, out PlateHandlers handlers) || handlers.GetProvinceBounds == null)
        {
            return false;
        }

        handlers.GetProvinceBounds.Invoke(out westLongitude, out eastLongitude, out southLatitude, out northLatitude);
        return true;
    }

    public VehicleMapPointData[] InvokeGetCurrentVehiclePoints(string plateMapName)
    {
        if (TryGetHandlers(plateMapName, out PlateHandlers handlers) && handlers.GetCurrentVehiclePoints != null)
        {
            return handlers.GetCurrentVehiclePoints.Invoke();
        }

        return null;
    }

    #endregion

    private PlateHandlers GetOrCreateHandlers(string plateMapName)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            handlers = new PlateHandlers();
            _plates[plateMapName] = handlers;
        }

        return handlers;
    }

    private bool TryGetHandlers(string plateMapName, out PlateHandlers handlers)
    {
        return _plates.TryGetValue(plateMapName, out handlers);
    }

    private void ClearHandler(string plateMapName, Action<PlateHandlers> clear)
    {
        if (!_plates.TryGetValue(plateMapName, out PlateHandlers handlers))
        {
            return;
        }

        clear(handlers);
        RemoveIfEmpty(plateMapName, handlers);
    }

    private void RemoveIfEmpty(string plateMapName, PlateHandlers handlers)
    {
        if (handlers.IsEmpty)
        {
            _plates.Remove(plateMapName);
        }
    }
}
