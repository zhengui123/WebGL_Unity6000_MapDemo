using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块地图车辆点位显示：GPU Instancing、合并与颜色标定。对外指令与通知均经 <see cref="PlateMapVehiclePointEvents"/> 单例。
/// 合并可根据中国地图与省级地图相应调整合并距离。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlateMapVehiclePointInstancedRenderer))]
public class PlateMapVehiclePointController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Transform _mapRoot;
    [SerializeField] private PlateMapVehiclePointInstancedRenderer _instancedRenderer;

    [Header("车辆点位数据")]
    [SerializeField] private VehicleMapPointData[] _vehiclePoints =
    {
        new VehicleMapPointData { vehicleId = "SD-001", longitude = 117.12, latitude = 36.65, alertValue = 0.1f },
        new VehicleMapPointData { vehicleId = "SD-002", longitude = 120.38, latitude = 35.42, alertValue = 0.85f },
        new VehicleMapPointData { vehicleId = "SD-003", longitude = 118.35, latitude = 37.44, alertValue = 0.3f }
    };

    [Header("近距离合并")]
    [SerializeField] private bool _enableProximityMerge = true;
    [Tooltip("地图局部 XZ 平面距离小于该值则合并")]
    [SerializeField] private float _mergeDistanceLocal = 0.002f;
    [SerializeField] private float _mergeScalePerExtraVehicle = 0.35f;
    [SerializeField] private float _mergeScaleMaxMultiplier = 3f;

    [Header("点位外观")]
    [SerializeField] private float _pointHeightOffset = 0.002f;
    [SerializeField] private Vector3 _pointLocalScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("颜色标定")]
    [SerializeField] private Color _colorAtDataMin = new Color(0.2f, 0.88f, 1f, 1f);
    [SerializeField] private Color _colorAtDataMax = new Color(1f, 0.28f, 0.12f, 1f);
    [SerializeField] private float _dataValueMin;
    [SerializeField] private float _dataValueMax = 1f;
    [SerializeField] private float _glowIntensityAtDataMin = 0.6f;
    [SerializeField] private float _glowIntensityAtDataMax = 2.8f;

    [Header("中心亮度")]
    [Range(0f, 5f)]
    [SerializeField] private float _centerBrightness = 1f;

    [Header("运行")]
    [SerializeField] private bool _rebuildOnStart = true;
    [SerializeField] private bool _enableRealtimeUpdate = true;

    private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>(128);
    private readonly List<CarPointGpuInstanceData> _gpuInstanceData = new List<CarPointGpuInstanceData>(128);
    private readonly List<PlateMapVehiclePointMerger.InputPoint> _mergeInputs = new List<PlateMapVehiclePointMerger.InputPoint>(128);
    private readonly List<PlateMapVehiclePointMerger.MergedPoint> _mergedPoints = new List<PlateMapVehiclePointMerger.MergedPoint>(128);
    private int _cachedMergeSourceHash;
    private int _cachedMergeSettingsHash;
    private bool _mergeCacheValid;
    private bool _initialized;
    private int _lastRawPointCount;
    private int _lastMergedPointCount;
    private int _lastMaxClusterSize;
    private float _lastAppliedCenterBrightness = float.NaN;

    public VehicleMapPointData[] VehiclePoints => _vehiclePoints;
    public bool IsDisplayReady => _initialized;

    public float CenterBrightness
    {
        get => _centerBrightness;
        set
        {
            _centerBrightness = Mathf.Clamp(value, 0f, 5f);
            ApplyCenterBrightness();
        }
    }

    private PlateMapVehiclePointEvents Hub => PlateMapVehiclePointEvents.Instance;

    /// <summary>板块地图 GameObject 名称，作为事件总线字典 key。</summary>
    private string PlateMapKey => gameObject.name;

    private void OnEnable()
    {
        RegisterToEventHub();
    }

    private void OnDisable()
    {
        UnregisterFromEventHub();
    }

    private void RegisterToEventHub()
    {
        Hub.RegisterSetVehiclePointsAction(PlateMapKey, ApplySetVehiclePoints);
        Hub.RegisterGetCurrentVehiclePointsAction(PlateMapKey, () => _vehiclePoints);
    }

    private void UnregisterFromEventHub()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.UnregisterSetVehiclePointsAction(PlateMapKey);
        hub.UnregisterGetCurrentVehiclePointsAction(PlateMapKey);
    }

    private void OnValidate()
    {
        _centerBrightness = Mathf.Clamp(_centerBrightness, 0f, 5f);
        if (_instancedRenderer == null)
        {
            _instancedRenderer = GetComponent<PlateMapVehiclePointInstancedRenderer>();
        }

        ApplyCenterBrightness();
    }

    private void Start()
    {
        ResolveReferences();
        if (_rebuildOnStart)
        {
            RefreshDisplayFromVehiclePoints();
        }
        else
        {
            _initialized = Hub.InvokeIsGeoConverterReady(PlateMapKey);
        }
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        ApplyCenterBrightnessIfDirty();

        if (_enableRealtimeUpdate)
        {
            RebuildGpuInstances();
        }
    }

    private void ApplySetVehiclePoints(VehicleMapPointData[] points, bool syncNow)
    {
        Hub.RaiseVehiclePointsWillChange(PlateMapKey, points);
        _vehiclePoints = points;
        Hub.RaiseVehiclePointsChanged(PlateMapKey, _vehiclePoints);
        InvalidateMergeCache();
        if (syncNow)
        {
            RefreshDisplayFromVehiclePoints();
        }
    }

    /// <summary>根据当前 _vehiclePoints 刷新 GPU 显示（内部流程，无对外菜单）。</summary>
    private void RefreshDisplayFromVehiclePoints()
    {
        ResolveReferences();
        Hub.RaiseRebuildStarted(PlateMapKey);

        if (!Hub.InvokeIsGeoConverterReady(PlateMapKey))
        {
            Debug.LogWarning($"[PlateMapVehiclePointController] 地理转换未就绪：{PlateMapKey}");
            _initialized = false;
            _instancedRenderer?.ClearInstances();
            return;
        }

        InvalidateMergeCache();
        CleanupLegacyPointObjects();
        RebuildGpuInstances();
        _initialized = true;

        int instanceCount = _instancedRenderer != null ? _instancedRenderer.InstanceCount : 0;

    }

    private float NormalizeDataValue(float dataValue)
    {
        if (_dataValueMax <= _dataValueMin)
        {
            return dataValue >= _dataValueMax ? 1f : 0f;
        }

        return Mathf.InverseLerp(_dataValueMin, _dataValueMax, dataValue);
    }

    private CarPointGpuInstanceData BuildGpuInstanceData(float dataValue)
    {
        float t = Mathf.Clamp01(NormalizeDataValue(dataValue));
        Color color = Color.Lerp(_colorAtDataMin, _colorAtDataMax, t);
        float glow = Mathf.Lerp(_glowIntensityAtDataMin, _glowIntensityAtDataMax, t);
        return new CarPointGpuInstanceData
        {
            Color = new Vector4(color.r, color.g, color.b, 1f),
            GlowIntensity = glow
        };
    }

    private static int GetDrawCallCount(int instanceCount)
    {
        return instanceCount <= 0 ? 0 : (instanceCount + 1022) / 1023;
    }

    private void RebuildGpuInstances()
    {
        _matrices.Clear();
        _gpuInstanceData.Clear();
        _lastRawPointCount = 0;
        _lastMergedPointCount = 0;
        _lastMaxClusterSize = 0;

        VehicleMapPointData[] vehicleSource = GetPointsForDisplay();
        if (vehicleSource == null || vehicleSource.Length == 0)
        {
            _instancedRenderer.ClearInstances();
            InvalidateMergeCache();
            return;
        }

        if (!Hub.InvokeIsGeoConverterReady(PlateMapKey) || _instancedRenderer == null)
        {
            return;
        }

        _instancedRenderer.SyncTransformSettings(_pointHeightOffset, _pointLocalScale);

        if (!TryGetMergedPointsForDisplay(out IReadOnlyList<PlateMapVehiclePointMerger.MergedPoint> mergedDisplay))
        {
            _instancedRenderer.ClearInstances();
            return;
        }

        for (int i = 0; i < mergedDisplay.Count; i++)
        {
            PlateMapVehiclePointMerger.MergedPoint merged = mergedDisplay[i];
            float scaleMul = GetMergeScaleMultiplier(merged.SourceCount);
            _matrices.Add(_instancedRenderer.BuildInstanceMatrix(merged.LocalPosition, scaleMul));
            _gpuInstanceData.Add(BuildGpuInstanceData(merged.SummedAlertValue));
        }

        _instancedRenderer.SetInstances(_matrices, _gpuInstanceData);
    }

    private bool TryGetMergedPointsForDisplay(out IReadOnlyList<PlateMapVehiclePointMerger.MergedPoint> mergedPoints)
    {
        mergedPoints = _mergedPoints;
        int sourceHash = ComputeVehiclePointsSourceHash();
        int settingsHash = ComputeMergeSettingsHash();

        if (_mergeCacheValid && _cachedMergeSourceHash == sourceHash && _cachedMergeSettingsHash == settingsHash)
        {
            _lastRawPointCount = _mergeInputs.Count;
            _lastMergedPointCount = _mergedPoints.Count;
            _lastMaxClusterSize = GetMaxClusterSize(_mergedPoints);
            return _mergedPoints.Count > 0;
        }

        _mergeInputs.Clear();
        _mergedPoints.Clear();

        int rawCount = CollectMergeInputs(GetPointsForDisplay(), _mergeInputs);
        _lastRawPointCount = rawCount;

        if (rawCount == 0)
        {
            InvalidateMergeCache();
            return false;
        }

        if (_enableProximityMerge)
        {
            PlateMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        }
        else
        {
            for (int i = 0; i < _mergeInputs.Count; i++)
            {
                PlateMapVehiclePointMerger.InputPoint input = _mergeInputs[i];
                _mergedPoints.Add(new PlateMapVehiclePointMerger.MergedPoint
                {
                    LocalPosition = input.LocalPosition,
                    SummedAlertValue = input.AlertValue,
                    SourceCount = 1
                });
            }
        }

        _cachedMergeSourceHash = sourceHash;
        _cachedMergeSettingsHash = settingsHash;
        _mergeCacheValid = true;
        _lastMergedPointCount = _mergedPoints.Count;
        _lastMaxClusterSize = GetMaxClusterSize(_mergedPoints);
        mergedPoints = _mergedPoints;
        return _mergedPoints.Count > 0;
    }

    private int CollectMergeInputs(VehicleMapPointData[] source, List<PlateMapVehiclePointMerger.InputPoint> output)
    {
        output.Clear();
        if (source == null)
        {
            return 0;
        }

        PlateMapVehiclePointEvents hub = Hub;
        for (int i = 0; i < source.Length; i++)
        {
            VehicleMapPointData data = source[i];
            if (string.IsNullOrWhiteSpace(data.vehicleId))
            {
                continue;
            }

            if (!hub.InvokeShouldIncludePoint(PlateMapKey, data))
            {
                continue;
            }

            if (!hub.InvokeTryLongitudeLatitudeToLocal(PlateMapKey, data.longitude, data.latitude, out Vector3 localPos))
            {
                continue;
            }

            output.Add(new PlateMapVehiclePointMerger.InputPoint
            {
                LocalPosition = localPos,
                AlertValue = data.alertValue
            });
        }

        return output.Count;
    }

    private float GetMergeScaleMultiplier(int sourceCount)
    {
        if (sourceCount <= 1 || !_enableProximityMerge)
        {
            return 1f;
        }

        float mul = 1f + (sourceCount - 1) * _mergeScalePerExtraVehicle;
        return Mathf.Min(mul, _mergeScaleMaxMultiplier);
    }

    private static int GetMaxClusterSize(IReadOnlyList<PlateMapVehiclePointMerger.MergedPoint> points)
    {
        int max = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].SourceCount > max)
            {
                max = points[i].SourceCount;
            }
        }

        return max;
    }

    private void InvalidateMergeCache()
    {
        _mergeCacheValid = false;
        _cachedMergeSourceHash = 0;
        _cachedMergeSettingsHash = 0;
    }

    private VehicleMapPointData[] GetPointsForDisplay()
    {
        if (_vehiclePoints == null)
        {
            return null;
        }

        return Hub.InvokeTransformPointsBeforeDisplay(PlateMapKey, _vehiclePoints);
    }

    private int ComputeVehiclePointsSourceHash()
    {
        unchecked
        {
            int hash = 17;
            VehicleMapPointData[] source = GetPointsForDisplay();
            if (source == null)
            {
                return hash;
            }

            for (int i = 0; i < source.Length; i++)
            {
                VehicleMapPointData p = source[i];
                hash = hash * 31 + (p.vehicleId?.GetHashCode() ?? 0);
                hash = hash * 31 + p.longitude.GetHashCode();
                hash = hash * 31 + p.latitude.GetHashCode();
                hash = hash * 31 + p.alertValue.GetHashCode();
            }

            return hash;
        }
    }

    private int ComputeMergeSettingsHash()
    {
        unchecked
        {
            PlateMapVehiclePointEvents hub = Hub;
            int hash = 17;
            hash = hash * 31 + (_enableProximityMerge ? 1 : 0);
            hash = hash * 31 + _mergeDistanceLocal.GetHashCode();
            hash = hash * 31 + _mergeScalePerExtraVehicle.GetHashCode();
            hash = hash * 31 + _mergeScaleMaxMultiplier.GetHashCode();
            hash = hash * 31 + _dataValueMin.GetHashCode();
            hash = hash * 31 + _dataValueMax.GetHashCode();
            hash = hash * 31 + PlateMapKey.GetHashCode();

            if (hub.InvokeIsGeoConverterReady(PlateMapKey) &&
                hub.InvokeGetProvinceLongitudeLatitudeBounds(
                    PlateMapKey, out double west, out double east, out double south, out double north))
            {
                hash = hash * 31 + west.GetHashCode();
                hash = hash * 31 + east.GetHashCode();
                hash = hash * 31 + south.GetHashCode();
                hash = hash * 31 + north.GetHashCode();
            }

            return hash;
        }
    }

    private void CleanupLegacyPointObjects()
    {
        if (_mapRoot == null)
        {
            return;
        }

        Transform pointsRoot = _mapRoot.Find("VehiclePoints");
        if (pointsRoot == null)
        {
            return;
        }

        for (int i = pointsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = pointsRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ResolveReferences()
    {
        if (_mapRoot == null)
        {
            _mapRoot = transform;
        }

        Hub.PublishGeoConverterRebuild(PlateMapKey);

        if (_instancedRenderer == null)
        {
            _instancedRenderer = GetComponent<PlateMapVehiclePointInstancedRenderer>();
            if (_instancedRenderer == null)
            {
                _instancedRenderer = gameObject.AddComponent<PlateMapVehiclePointInstancedRenderer>();
            }
        }

        _instancedRenderer.BindMapRoot(_mapRoot);
        _instancedRenderer.SyncTransformSettings(_pointHeightOffset, _pointLocalScale);
        ApplyCenterBrightness();
    }

    private void ApplyCenterBrightnessIfDirty()
    {
        if (float.IsNaN(_lastAppliedCenterBrightness) ||
            !Mathf.Approximately(_centerBrightness, _lastAppliedCenterBrightness))
        {
            ApplyCenterBrightness();
        }
    }

    private void ApplyCenterBrightness()
    {
        if (_instancedRenderer == null)
        {
            return;
        }

        _instancedRenderer.SetCenterBrightness(_centerBrightness);
        _lastAppliedCenterBrightness = _centerBrightness;
    }
}
