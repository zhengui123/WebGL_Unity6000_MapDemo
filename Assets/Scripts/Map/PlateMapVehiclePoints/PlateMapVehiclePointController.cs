using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块地图车辆点位显示：GPU Instancing、合并与颜色标定。对外指令与通知均经 <see cref="PlateMapVehiclePointEvents"/> 单例。
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
        PlateMapVehiclePointEvents hub = Hub;
        hub.RequestSetVehiclePoints += ApplySetVehiclePoints;
        hub.RequestRebuildPoints += RebuildPoints;
        hub.RequestClearVehiclePoints += ClearSpawnedPoints;
        hub.GetCurrentVehiclePoints = () => _vehiclePoints;
    }

    private void UnregisterFromEventHub()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }
        hub.RequestSetVehiclePoints -= ApplySetVehiclePoints;
        hub.RequestRebuildPoints -= RebuildPoints;
        hub.RequestClearVehiclePoints -= ClearSpawnedPoints;
        hub.GetCurrentVehiclePoints = null;
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
            RebuildPoints();
        }
        else
        {
            _initialized = Hub.InvokeIsGeoConverterReady();
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
        Hub.RaiseVehiclePointsWillChange(points);
        _vehiclePoints = points;
        Hub.RaiseVehiclePointsChanged(_vehiclePoints);
        InvalidateMergeCache();
        if (syncNow)
        {
            RebuildPoints();
        }
    }

    [ContextMenu("重建车辆点位")]
    public void RebuildPoints()
    {
        ResolveReferences();
        Hub.RaiseRebuildStarted();

        if (!Hub.InvokeIsGeoConverterReady())
        {
            Debug.LogWarning("[PlateMapVehiclePointController] 地理转换未就绪。");
            _initialized = false;
            _instancedRenderer?.ClearInstances();
            Hub.RaiseRebuildCompleted(new PlateMapVehiclePointRebuildInfo(0, 0, 0, 0, 0, false));
            return;
        }

        InvalidateMergeCache();
        CleanupLegacyPointObjects();
        RebuildGpuInstances();
        _initialized = true;

        int instanceCount = _instancedRenderer != null ? _instancedRenderer.InstanceCount : 0;
        var rebuildInfo = new PlateMapVehiclePointRebuildInfo(
            _lastRawPointCount,
            _lastMergedPointCount,
            instanceCount,
            GetDrawCallCount(instanceCount),
            _lastMaxClusterSize,
            instanceCount > 0);
        Hub.RaiseRebuildCompleted(rebuildInfo);

        string mergeInfo = _enableProximityMerge
            ? $"，合并 {_lastRawPointCount}→{_lastMergedPointCount}（最大簇 {_lastMaxClusterSize}）"
            : string.Empty;
        Debug.Log(
            $"[PlateMapVehiclePointController] GPU 实例化重建完成，{instanceCount} 个点，DrawCall≈{rebuildInfo.DrawCallCount}{mergeInfo}");
    }

    [ContextMenu("验证近距离合并（幂等）")]
    public void VerifyProximityMergeIdempotent()
    {
        ResolveReferences();
        if (!Hub.InvokeIsGeoConverterReady())
        {
            Debug.LogWarning("[PlateMapVehiclePointController] 地理转换未就绪，无法验证合并。");
            return;
        }

        VehicleMapPointData[] displaySource = GetPointsForDisplay();
        InvalidateMergeCache();
        int firstRaw = CollectMergeInputs(displaySource, _mergeInputs);
        PlateMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        int firstMerged = _mergedPoints.Count;
        int firstMax = GetMaxClusterSize(_mergedPoints);

        InvalidateMergeCache();
        int secondRaw = CollectMergeInputs(displaySource, _mergeInputs);
        PlateMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        int secondMerged = _mergedPoints.Count;
        int secondMax = GetMaxClusterSize(_mergedPoints);

        bool idempotent = firstRaw == secondRaw && firstMerged == secondMerged && firstMax == secondMax;
        Debug.Log(
            idempotent
                ? $"[PlateMapVehiclePointController] 合并验证通过：原始 {firstRaw} → 显示 {firstMerged}，最大簇 {firstMax}"
                : $"[PlateMapVehiclePointController] 合并验证失败：{firstRaw}/{firstMerged}/{firstMax} vs {secondRaw}/{secondMerged}/{secondMax}");
    }

    [ContextMenu("清空车辆点位")]
    public void ClearSpawnedPoints()
    {
        Hub.RaiseVehiclePointsWillChange(Array.Empty<VehicleMapPointData>());
        _vehiclePoints = Array.Empty<VehicleMapPointData>();
        Hub.RaiseVehiclePointsChanged(_vehiclePoints);
        InvalidateMergeCache();
        _instancedRenderer?.ClearInstances();
        _matrices.Clear();
        _gpuInstanceData.Clear();
        _initialized = false;
        Hub.RaiseCleared();
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

        if (!Hub.InvokeIsGeoConverterReady() || _instancedRenderer == null)
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

            if (!hub.InvokeShouldIncludePoint(data))
            {
                continue;
            }

            if (!hub.InvokeTryLongitudeLatitudeToLocal(data.longitude, data.latitude, out Vector3 localPos))
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

        return Hub.InvokeTransformPointsBeforeDisplay(_vehiclePoints);
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
            hash = hash * 31 + (hub.ShouldIncludePoint != null ? 1 : 0);
            hash = hash * 31 + (hub.TransformPointsBeforeDisplay != null ? 1 : 0);

            if (hub.InvokeIsGeoConverterReady() &&
                hub.InvokeGetProvinceLongitudeLatitudeBounds(
                    out double west, out double east, out double south, out double north))
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

        Hub.PublishGeoConverterRebuild();

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
