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
    private readonly Dictionary<string, PlateMapSetVehiclePointsAction> _setVehiclePointsActions = new();
    private readonly Dictionary<string, PlateMapGetCurrentVehiclePointsAction> _getCurrentVehiclePointsActions = new();

    private readonly Dictionary<string, PlateMapShouldIncludePointAction> _shouldIncludePointActions = new();
    private readonly Dictionary<string, PlateMapTransformPointsBeforeDisplayAction> _transformPointsBeforeDisplayActions =
        new();

    private readonly Dictionary<string, PlateMapGeoConverterRebuildAction> _geoConverterRebuildActions = new();
    private readonly Dictionary<string, PlateMapIsGeoConverterReadyAction> _isGeoConverterReadyActions = new();
    private readonly Dictionary<string, PlateMapTryLonLatToLocalAction> _tryLonLatToLocalActions = new();
    private readonly Dictionary<string, PlateMapGetProvinceBoundsAction> _getProvinceBoundsActions = new();

    public event Action<string, VehicleMapPointData[]> VehiclePointsWillChangeAction;
    public event Action<string, VehicleMapPointData[]> VehiclePointsChangedAction;
    public event Action<string> RebuildStartedAction;

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

    #region 注册 / 注销（板块名为 gameObject.name）

    public void RegisterSetVehiclePointsAction(string plateMapName, PlateMapSetVehiclePointsAction action)
    {
        _setVehiclePointsActions[plateMapName] = action;
    }

    public void UnregisterSetVehiclePointsAction(string plateMapName)
    {
        _setVehiclePointsActions.Remove(plateMapName);
    }

    public void RegisterGetCurrentVehiclePointsAction(string plateMapName, PlateMapGetCurrentVehiclePointsAction action)
    {
        _getCurrentVehiclePointsActions[plateMapName] = action;
    }

    public void UnregisterGetCurrentVehiclePointsAction(string plateMapName)
    {
        _getCurrentVehiclePointsActions.Remove(plateMapName);
    }

    public void RegisterShouldIncludePointAction(string plateMapName, PlateMapShouldIncludePointAction action)
    {
        _shouldIncludePointActions[plateMapName] = action;
    }

    public void UnregisterShouldIncludePointAction(string plateMapName)
    {
        _shouldIncludePointActions.Remove(plateMapName);
    }

    public void RegisterTransformPointsBeforeDisplayAction(
        string plateMapName,
        PlateMapTransformPointsBeforeDisplayAction action)
    {
        _transformPointsBeforeDisplayActions[plateMapName] = action;
    }

    public void UnregisterTransformPointsBeforeDisplayAction(string plateMapName)
    {
        _transformPointsBeforeDisplayActions.Remove(plateMapName);
    }

    public void RegisterGeoConverterActions(
        string plateMapName,
        PlateMapGeoConverterRebuildAction rebuildAction,
        PlateMapIsGeoConverterReadyAction readyAction,
        PlateMapTryLonLatToLocalAction lonLatToLocalAction,
        PlateMapGetProvinceBoundsAction boundsAction)
    {
        _geoConverterRebuildActions[plateMapName] = rebuildAction;
        _isGeoConverterReadyActions[plateMapName] = readyAction;
        _tryLonLatToLocalActions[plateMapName] = lonLatToLocalAction;
        _getProvinceBoundsActions[plateMapName] = boundsAction;
    }

    public void UnregisterGeoConverterActions(string plateMapName)
    {
        _geoConverterRebuildActions.Remove(plateMapName);
        _isGeoConverterReadyActions.Remove(plateMapName);
        _tryLonLatToLocalActions.Remove(plateMapName);
        _getProvinceBoundsActions.Remove(plateMapName);
    }

    #endregion

    #region 对外发布

    public bool PublishSetVehiclePoints(string plateMapName, VehicleMapPointData[] points, bool syncNow = true)
    {
        if (!_setVehiclePointsActions.TryGetValue(plateMapName, out PlateMapSetVehiclePointsAction action))
        {
            Debug.LogWarning($"[PlateMapVehiclePointEvents] 未注册板块「{plateMapName}」的 SetVehiclePointsAction。");
            return false;
        }

        action.Invoke(points, syncNow);
        return true;
    }

    public bool UpdateVehiclePointsFromJson(string plateMapName, string vehiclePointsJson, bool syncNow = true)
    {
        if (!VehicleMapPointJson.TryParse(vehiclePointsJson, out VehicleMapPointData[] points, out string error))
        {
            Debug.LogError($"[PlateMapVehiclePointEvents] UpdateVehiclePointsFromJson 失败：{error}");
            return false;
        }

        return PublishSetVehiclePoints(plateMapName, points, syncNow);
    }

    public void PublishGeoConverterRebuild(string plateMapName)
    {
        if (_geoConverterRebuildActions.TryGetValue(plateMapName, out PlateMapGeoConverterRebuildAction action))
        {
            action.Invoke();
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
        if (!_shouldIncludePointActions.TryGetValue(plateMapName, out PlateMapShouldIncludePointAction action))
        {
            return true;
        }

        return action.Invoke(data);
    }

    public VehicleMapPointData[] InvokeTransformPointsBeforeDisplay(string plateMapName, VehicleMapPointData[] source)
    {
        if (source == null)
        {
            return null;
        }

        return _transformPointsBeforeDisplayActions.TryGetValue(
            plateMapName,
            out PlateMapTransformPointsBeforeDisplayAction action)
            ? action.Invoke(source)
            : source;
    }

    public bool InvokeTryLongitudeLatitudeToLocal(
        string plateMapName,
        double longitude,
        double latitude,
        out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (!_tryLonLatToLocalActions.TryGetValue(plateMapName, out PlateMapTryLonLatToLocalAction action))
        {
            return false;
        }

        return action.Invoke(longitude, latitude, out localPosition);
    }

    public bool InvokeIsGeoConverterReady(string plateMapName)
    {
        return _isGeoConverterReadyActions.TryGetValue(plateMapName, out PlateMapIsGeoConverterReadyAction action) &&
               action.Invoke();
    }

    public bool InvokeGetProvinceLongitudeLatitudeBounds(
        string plateMapName,
        out double westLongitude,
        out double eastLongitude,
        out double southLatitude,
        out double northLatitude)
    {
        westLongitude = eastLongitude = southLatitude = northLatitude = 0;
        if (!_getProvinceBoundsActions.TryGetValue(plateMapName, out PlateMapGetProvinceBoundsAction action))
        {
            return false;
        }

        action.Invoke(out westLongitude, out eastLongitude, out southLatitude, out northLatitude);
        return true;
    }

    public VehicleMapPointData[] InvokeGetCurrentVehiclePoints(string plateMapName)
    {
        return _getCurrentVehiclePointsActions.TryGetValue(
            plateMapName,
            out PlateMapGetCurrentVehiclePointsAction action)
            ? action.Invoke()
            : null;
    }

    #endregion
}
