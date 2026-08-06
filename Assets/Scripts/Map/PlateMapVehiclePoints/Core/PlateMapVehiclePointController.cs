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
    [Tooltip("勾选：簇位置取平均值（并查集传递合并）；不勾选：以判定点为圆心，位置取圆心坐标")]
    [SerializeField] private bool _mergeUseAveragePosition;
    [SerializeField] private float _mergeScalePerExtraVehicle = 0.35f;
    [SerializeField] private float _mergeScaleMaxMultiplier = 3f;

    [Header("点位外观")]
    [SerializeField] private float _pointHeightOffset = 0.002f;
    [SerializeField] private Vector3 _pointLocalScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("颜色标定")]
    [SerializeField] private Color _colorAtDataMin = new Color(1f, 0.9843137f, 0.627451f, 1f);
    [SerializeField] private Color _colorAtDataMax = new Color(0.9607843f, 1f, 0f, 1f);
    [SerializeField] private float _dataValueMin;
    [SerializeField] private float _dataValueMax = 1f;
    [SerializeField] private float _glowIntensityAtDataMin = 0.6f;
    [SerializeField] private float _glowIntensityAtDataMax = 2.8f;

    [Header("中心亮度")]
    [Range(0f, 5f)]
    [SerializeField] private float _centerBrightness = 1f;

    [Header("运行")]
    [SerializeField] private bool _rebuildOnStart = true;

    [Header("调试映射 Cube")]
    [Tooltip("首次经纬度→局部坐标映射成功后，在 VehiclePoints 下创建 Cube（仅一次，与 GPU 并存）")]
    [SerializeField] private bool _spawnMappedPointCubesOnce = true;

    private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>(128);
    private readonly List<CarPointGpuInstanceData> _gpuInstanceData = new List<CarPointGpuInstanceData>(128);
    private readonly List<PlateMapVehiclePointMerger.InputPoint> _mergeInputs = new List<PlateMapVehiclePointMerger.InputPoint>(128);
    public readonly List<PlateMapVehiclePointMerger.MergedPoint> _mergedPoints = new List<PlateMapVehiclePointMerger.MergedPoint>(128);
    private int _cachedMergeSourceHash;
    private int _cachedMergeSettingsHash;
    private bool _mergeCacheValid;
    private bool _initialized;
    private bool _wasDisplayActiveBeforeDisable;
    private float _lastAppliedCenterBrightness = float.NaN;
    private bool _mappedPointCubesSpawned;

    public VehicleMapPointData[] VehiclePoints => _vehiclePoints;
    public bool IsDisplayReady => _initialized;
    public string EventPlateMapKey => gameObject.name;

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
    private string PlateMapKey => EventPlateMapKey;

    private void OnEnable()
    {
        RegisterToEventHub();

        if (!Application.isPlaying)
        {
            return;
        }

        SyncVehiclePointsFromHubCache();

        // Start 仅在首次激活执行；AllPlateMap 隐藏后再显示需从 Hub 缓存重载热力图
        if (_wasDisplayActiveBeforeDisable || Hub.ConsumePendingDisplayRefresh(PlateMapKey))
        {
            RefreshDisplayFromVehiclePoints();
        }
    }

    private void OnDisable()
    {
        _wasDisplayActiveBeforeDisable = _initialized;
        _initialized = false;
        InvalidateMergeCache();
        SuspendGpuDisplay();
        UnregisterFromEventHub();
    }

    private void RegisterToEventHub()
    {
        Hub.SeedCachedVehiclePointsIfEmpty(PlateMapKey, _vehiclePoints);
        Hub.RegisterSetVehiclePointsAction(PlateMapKey, ApplySetVehiclePoints);
        Hub.RegisterGetCurrentVehiclePointsAction(PlateMapKey, () => _vehiclePoints);
        Hub.RegisterRefreshVehiclePointsDisplayAction(PlateMapKey, RefreshDisplayFromVehiclePoints);
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
        hub.UnregisterRefreshVehiclePointsDisplayAction(PlateMapKey);
    }

    private void OnValidate()
    {
        _centerBrightness = Mathf.Clamp(_centerBrightness, 0f, 5f);
        if (_instancedRenderer == null)
        {
            _instancedRenderer = GetComponent<PlateMapVehiclePointInstancedRenderer>();
        }

        ApplyCenterBrightness();

        if (Application.isPlaying && _initialized)
        {
            InvalidateMergeCache();
            RebuildGpuInstances();
        }
    }

    private void Start()
    {


        PlateMapVehiclePointEvents.Instance.SetPlateMapName(PlateMapKey);
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

    private void LateUpdate()
    {
        if (_initialized)
        {
            ApplyCenterBrightnessIfDirty();
        }
    }

    private void ApplySetVehiclePoints(VehicleMapPointData[] points, bool syncNow)
    {
        _vehiclePoints = PlateMapVehiclePointEvents.CloneVehiclePointArray(points);
        Hub.RaiseVehiclePointsChanged(PlateMapKey, _vehiclePoints);
        InvalidateMergeCache();

        if (!syncNow)
        {
            return;
        }

        if (IsVehiclePointsDisplayActive())
        {
            RefreshDisplayFromVehiclePoints();
        }
        else
        {
            SuspendGpuDisplay();
            Hub.MarkPendingDisplayRefresh(PlateMapKey);
        }
    }

    /// <summary>板块可见且组件启用时才绘制 GPU 热力点。</summary>
    private bool IsVehiclePointsDisplayActive()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    /// <summary>隐藏热力图绘制，保留 _vehiclePoints 数据源。</summary>
    private void SuspendGpuDisplay()
    {
        _instancedRenderer?.ClearInstances();
    }

    /// <summary>从 Hub 缓存同步数据源（AllPlateMap 隐藏期间 API 仍可能写入）。</summary>
    private void SyncVehiclePointsFromHubCache()
    {
        if (Hub.TryGetCachedVehiclePoints(PlateMapKey, out VehicleMapPointData[] cached))
        {
            _vehiclePoints = PlateMapVehiclePointEvents.CloneVehiclePointArray(cached);
            InvalidateMergeCache();
        }
    }

    /// <summary>根据当前 _vehiclePoints 刷新 GPU 显示（内部流程，无对外菜单）。</summary>
    private void RefreshDisplayFromVehiclePoints()
    {
        if (!IsVehiclePointsDisplayActive())
        {
            Hub.MarkPendingDisplayRefresh(PlateMapKey);
            return;
        }

        ResolveReferences();
        Hub.RaiseRebuildStarted(PlateMapKey);

        if (!Hub.InvokeIsGeoConverterReady(PlateMapKey))
        {
            // Debug.LogWarning($"[PlateMapVehiclePointController] 地理转换未就绪：{PlateMapKey}");
            _initialized = false;
            _instancedRenderer?.ClearInstances();
            return;
        }

        InvalidateMergeCache();
        CleanupLegacyPointObjects();
        RebuildGpuInstances();
        _initialized = true;
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

    private void RebuildGpuInstances()
    {
        _matrices.Clear();
        _gpuInstanceData.Clear();

        VehicleMapPointData[] vehicleSource = GetPointsForDisplay();
        if (vehicleSource == null)
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

        if (!TryGetMergedPointsForDisplay(vehicleSource, out IReadOnlyList<PlateMapVehiclePointMerger.MergedPoint> mergedDisplay))
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

    private bool TryGetMergedPointsForDisplay(
        VehicleMapPointData[] vehicleSource,
        out IReadOnlyList<PlateMapVehiclePointMerger.MergedPoint> mergedPoints)
    {
        mergedPoints = _mergedPoints;
        int sourceHash = ComputeVehiclePointsSourceHash(vehicleSource);
        int settingsHash = ComputeMergeSettingsHash();

        if (_mergeCacheValid && _cachedMergeSourceHash == sourceHash && _cachedMergeSettingsHash == settingsHash)
        {
            return _mergedPoints.Count > 0;
        }

        _mergeInputs.Clear();
        _mergedPoints.Clear();

        int rawCount = CollectMergeInputs(vehicleSource, _mergeInputs);

        if (rawCount == 0)
        {
            InvalidateMergeCache();
            return false;
        }

        // 合并前原始点：首次映射成功后创建调试 Cube（不影响 GPU Instancing）
        TrySpawnMappedPointCubesOnce(_mergeInputs);

        if (_enableProximityMerge)
        {
            PlateMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints, _mergeUseAveragePosition);
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

    private int ComputeVehiclePointsSourceHash(VehicleMapPointData[] source)
    {
        unchecked
        {
            int hash = 17;
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
            hash = hash * 31 + (_mergeUseAveragePosition ? 1 : 0);
            hash = hash * 31 + _mergeDistanceLocal.GetHashCode();
            hash = hash * 31 + _mergeScalePerExtraVehicle.GetHashCode();
            hash = hash * 31 + _mergeScaleMaxMultiplier.GetHashCode();
            hash = hash * 31 + _dataValueMin.GetHashCode();
            hash = hash * 31 + _dataValueMax.GetHashCode();
            hash = hash * 31 + PlateMapKey.GetHashCode();
            hash = hash * 31 + (hub.HasShouldIncludePointAction(PlateMapKey) ? 1 : 0);
            hash = hash * 31 + (hub.HasTransformPointsBeforeDisplayAction(PlateMapKey) ? 1 : 0);

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
        // 已创建过调试 Cube 则不再清空，避免后续刷新删掉「只建一次」的点位
        if (_mappedPointCubesSpawned || _mapRoot == null)
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

    /// <summary>
    /// 首次将车辆点映射为局部坐标后，在 _mapRoot/VehiclePoints 下按合并前点位生成 Cube。
    /// 原理：Cube 用 localPosition 挂到地图根下，与 InstancedRenderer 的 TransformPoint 路径一致，便于目视校验映射。
    /// </summary>
    private void TrySpawnMappedPointCubesOnce(List<PlateMapVehiclePointMerger.InputPoint> mappedInputs)
    {
        if (!_spawnMappedPointCubesOnce || _mappedPointCubesSpawned || _mapRoot == null)
        {
            return;
        }

        if (mappedInputs == null || mappedInputs.Count == 0)
        {
            return;
        }

        Transform pointsRoot = _mapRoot.Find("VehiclePoints");
        if (pointsRoot == null)
        {
            GameObject rootGo = new GameObject("VehiclePoints");
            rootGo.transform.SetParent(_mapRoot, false);
            pointsRoot = rootGo.transform;
        }
        else
        {
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

        for (int i = 0; i < mappedInputs.Count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"MappedPoint_{i}";
            cube.transform.SetParent(pointsRoot, false);

            Vector3 localPos = mappedInputs[i].LocalPosition;
            localPos.y = _pointHeightOffset;
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = _pointLocalScale;

            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }

        _mappedPointCubesSpawned = true;
        Debug.Log(
            $"[PlateMapVehiclePointController] 已一次性创建 {mappedInputs.Count} 个映射调试 Cube → {_mapRoot.name}/VehiclePoints");
    }

    private void ResolveReferences()
    {
        if (_mapRoot == null)
        {
            _mapRoot = transform;
        }

        if (!Hub.InvokeIsGeoConverterReady(PlateMapKey))
        {
            Hub.PublishGeoConverterRebuild(PlateMapKey);
        }

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
