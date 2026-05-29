using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 山东省地图打点控制器：默认 GPU Instancing 单 DrawCall 绘制全部点位。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SdMapVehiclePointInstancedRenderer))]
public class SdMapVehiclePointController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Transform _mapRoot;
    [SerializeField] private SdMapGeoConverter _geoConverter;
    [SerializeField] private SdMapVehiclePointInstancedRenderer _instancedRenderer;

    [Header("车辆点位数据")]
    [SerializeField] private VehicleMapPointData[] _vehiclePoints =
    {
        new VehicleMapPointData { vehicleId = "SD-001", longitude = 117.12, latitude = 36.65, alertValue = 0.1f },
        new VehicleMapPointData { vehicleId = "SD-002", longitude = 120.38, latitude = 35.42, alertValue = 0.85f },
        new VehicleMapPointData { vehicleId = "SD-003", longitude = 118.35, latitude = 37.44, alertValue = 0.3f }
    };

    [Header("随机生成（山东省内）")]
    [SerializeField] private int _randomGenerateCount = 100;
    [SerializeField] private bool _useGeoConverterBounds = true;
    [SerializeField] private double _fallbackWestLongitude = 114.819;
    [SerializeField] private double _fallbackEastLongitude = 122.714;
    [SerializeField] private double _fallbackSouthLatitude = 34.377;
    [SerializeField] private double _fallbackNorthLatitude = 38.401;
    [Range(0f, 0.2f)]
    [SerializeField] private float _randomBoundsInset = 0.02f;
    [SerializeField] private int _randomSeed;

    [Header("省界严格检测")]
    [SerializeField] private TextAsset _shandongBoundaryJson;
    [SerializeField] private bool _strictProvinceBoundary = true;
    [SerializeField] private int _randomMaxAttemptsPerPoint = 512;

    [Header("近距离合并")]
    [SerializeField] private bool _enableProximityMerge = true;
    [Tooltip("地图局部 XZ 平面距离小于该值（与 SdMapGeoConverter 局部坐标一致）则合并")]
    [SerializeField] private float _mergeDistanceLocal = 0.002f;
    [Tooltip("每多合并 1 辆车，在基础缩放上乘以 (1 + 此值)")]
    [SerializeField] private float _mergeScalePerExtraVehicle = 0.35f;
    [Tooltip("合并缩放上限倍数")]
    [SerializeField] private float _mergeScaleMaxMultiplier = 3f;

    [Header("点位外观")]
    [SerializeField] private float _pointHeightOffset = 0.002f;
    [SerializeField] private Vector3 _pointLocalScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("颜色标定（VehicleMapPointData.alertValue 在最小/最大之间插值）")]
    [Tooltip("数据源等于下限时使用的颜色")]
    [SerializeField] private Color _colorAtDataMin = new Color(0.2f, 0.88f, 1f, 1f);
    [Tooltip("数据源等于上限时使用的颜色")]
    [SerializeField] private Color _colorAtDataMax = new Color(1f, 0.28f, 0.12f, 1f);
    [Tooltip("数据源映射下限（对应 colorAtDataMin）")]
    [SerializeField] private float _dataValueMin;
    [Tooltip("数据源映射上限（对应 colorAtDataMax）")]
    [SerializeField] private float _dataValueMax = 1f;
    [Tooltip("数据源等于下限时的中心亮度")]
    [SerializeField] private float _glowIntensityAtDataMin = 0.6f;
    [Tooltip("数据源等于上限时的中心亮度")]
    [SerializeField] private float _glowIntensityAtDataMax = 2.8f;

    [Header("中心亮度（运行时统一调节）")]
    [Tooltip("全局倍数，同步到 M_CarPointGlowInstanced._CenterBrightness，改变时无需重建点位")]
    [Range(0f, 5f)]
    [SerializeField] private float _centerBrightness = 1f;

    [Header("运行")]
    [SerializeField] private bool _rebuildOnStart = true;
    [SerializeField] private bool _enableRealtimeUpdate = true;

    private ShandongProvinceBoundary _provinceBoundary;
    private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>(128);
    private readonly List<CarPointGpuInstanceData> _gpuInstanceData = new List<CarPointGpuInstanceData>(128);
    private readonly List<SdMapVehiclePointMerger.InputPoint> _mergeInputs = new List<SdMapVehiclePointMerger.InputPoint>(128);
    private readonly List<SdMapVehiclePointMerger.MergedPoint> _mergedPoints = new List<SdMapVehiclePointMerger.MergedPoint>(128);
    private int _cachedMergeSourceHash;
    private int _cachedMergeSettingsHash;
    private bool _mergeCacheValid;
    private bool _initialized;
    private int _lastRawPointCount;
    private int _lastMergedPointCount;
    private int _lastMaxClusterSize;
    private float _lastAppliedCenterBrightness = float.NaN;

    /// <summary>全局中心亮度（运行时可动态修改，立即生效）。</summary>
    public float CenterBrightness
    {
        get => _centerBrightness;
        set
        {
            _centerBrightness = Mathf.Clamp(value, 0f, 5f);
            ApplyCenterBrightness();
        }
    }

    private void OnValidate()
    {
        _centerBrightness = Mathf.Clamp(_centerBrightness, 0f, 5f);
        if (_instancedRenderer == null)
        {
            _instancedRenderer = GetComponent<SdMapVehiclePointInstancedRenderer>();
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
            _initialized = _geoConverter != null && _geoConverter.IsReady;
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

    [ContextMenu("随机生成100个山东省内点位")]
    public void GenerateRandomVehiclePointsInShandongMenu()
    {
        GenerateRandomVehiclePointsInShandong(100);
    }

    public void GenerateRandomVehiclePointsInShandong(int count = -1)
    {
        if (count <= 0)
        {
            count = _randomGenerateCount;
        }

        ResolveReferences();

        if (_strictProvinceBoundary && !EnsureProvinceBoundaryLoaded())
        {
            Debug.LogError("[SdMapVehiclePointController] 省界数据未加载，无法严格生成省内点位。");
            return;
        }

        System.Random rng = _randomSeed != 0 ? new System.Random(_randomSeed) : new System.Random();
        var list = new List<VehicleMapPointData>(count);
        int failed = 0;

        for (int i = 0; i < count; i++)
        {
            if (!TrySampleRandomLongitudeLatitude(rng, out double lon, out double lat))
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

        _vehiclePoints = list.ToArray();
        InvalidateMergeCache();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        Debug.Log(
            $"[SdMapVehiclePointController] 已生成 {_vehiclePoints.Length}/{count} 个省内点位（失败 {failed} 次）。");

        if (Application.isPlaying || !Application.isPlaying)
        {
            RebuildPoints();
        }
    }

    [ContextMenu("校验车辆点位是否在山东省内")]
    public void ValidateVehiclePointsInsideProvince()
    {
        if (!EnsureProvinceBoundaryLoaded())
        {
            return;
        }

        int outside = 0;
        if (_vehiclePoints != null)
        {
            for (int i = 0; i < _vehiclePoints.Length; i++)
            {
                VehicleMapPointData p = _vehiclePoints[i];
                if (!_provinceBoundary.Contains(p.longitude, p.latitude))
                {
                    outside++;
                }
            }
        }

        Debug.Log(outside == 0
            ? "[SdMapVehiclePointController] 全部点位均在山东省界内。"
            : $"[SdMapVehiclePointController] 共 {outside} 个点位在省界外。");
    }

    public void SetVehiclePoints(VehicleMapPointData[] points, bool syncNow = true)
    {
        _vehiclePoints = points;
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

        if (_geoConverter == null || !_geoConverter.IsReady)
        {
            Debug.LogWarning("[SdMapVehiclePointController] 地理转换未就绪。");
            _initialized = false;
            _instancedRenderer?.ClearInstances();
            return;
        }

        InvalidateMergeCache();
        CleanupLegacyPointObjects();
        RebuildGpuInstances();
        _initialized = true;
        string mergeInfo = _enableProximityMerge
            ? $"，合并 {_lastRawPointCount}→{_lastMergedPointCount}（最大簇 {_lastMaxClusterSize}）"
            : string.Empty;
        Debug.Log(
            $"[SdMapVehiclePointController] GPU 实例化重建完成，{_instancedRenderer.InstanceCount} 个点，DrawCall≈{GetDrawCallCount(_instancedRenderer.InstanceCount)}{mergeInfo}");
    }

    [ContextMenu("验证近距离合并（幂等）")]
    public void VerifyProximityMergeIdempotent()
    {
        ResolveReferences();
        if (_geoConverter == null || !_geoConverter.IsReady)
        {
            Debug.LogWarning("[SdMapVehiclePointController] 地理转换未就绪，无法验证合并。");
            return;
        }

        InvalidateMergeCache();
        int firstRaw = CollectMergeInputs(_mergeInputs);
        SdMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        int firstMerged = _mergedPoints.Count;
        int firstMax = GetMaxClusterSize(_mergedPoints);

        InvalidateMergeCache();
        int secondRaw = CollectMergeInputs(_mergeInputs);
        SdMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        int secondMerged = _mergedPoints.Count;
        int secondMax = GetMaxClusterSize(_mergedPoints);

        bool idempotent = firstRaw == secondRaw && firstMerged == secondMerged && firstMax == secondMax;
        Debug.Log(
            idempotent
                ? $"[SdMapVehiclePointController] 合并验证通过：原始 {firstRaw} → 显示 {firstMerged}，最大簇 {firstMax}（两次结果一致）"
                : $"[SdMapVehiclePointController] 合并验证失败：{firstRaw}/{firstMerged}/{firstMax} vs {secondRaw}/{secondMerged}/{secondMax}");
    }

    [ContextMenu("清空车辆点位")]
    public void ClearSpawnedPoints()
    {
        _vehiclePoints = Array.Empty<VehicleMapPointData>();
        InvalidateMergeCache();
        _instancedRenderer?.ClearInstances();
        _matrices.Clear();
        _gpuInstanceData.Clear();
        _initialized = false;
    }

    /// <summary>将数据源数值归一化到 [0,1]，用于颜色/亮度插值。</summary>
    private float NormalizeDataValue(float dataValue)
    {
        if (_dataValueMax <= _dataValueMin)
        {
            return dataValue >= _dataValueMax ? 1f : 0f;
        }

        return Mathf.InverseLerp(_dataValueMin, _dataValueMax, dataValue);
    }

    /// <summary>按标定颜色在最小/最大数据源之间插值，生成 GPU 实例数据。</summary>
    private CarPointGpuInstanceData BuildGpuInstanceData(float dataValue)
    {
        // 合并后业务值为累加，映射到标定区间（超出上限则饱和为最深色）
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
        if (instanceCount <= 0)
        {
            return 0;
        }

        return (instanceCount + 1022) / 1023;
    }

    private void RebuildGpuInstances()
    {
        _matrices.Clear();
        _gpuInstanceData.Clear();
        _lastRawPointCount = 0;
        _lastMergedPointCount = 0;
        _lastMaxClusterSize = 0;

        if (_vehiclePoints == null || _vehiclePoints.Length == 0)
        {
            _instancedRenderer.ClearInstances();
            InvalidateMergeCache();
            return;
        }

        if (_geoConverter == null || !_geoConverter.IsReady || _instancedRenderer == null)
        {
            return;
        }

        _instancedRenderer.SyncTransformSettings(_pointHeightOffset, _pointLocalScale);

        if (!TryGetMergedPointsForDisplay(out IReadOnlyList<SdMapVehiclePointMerger.MergedPoint> displayPoints))
        {
            _instancedRenderer.ClearInstances();
            return;
        }

        for (int i = 0; i < displayPoints.Count; i++)
        {
            SdMapVehiclePointMerger.MergedPoint merged = displayPoints[i];
            float scaleMul = GetMergeScaleMultiplier(merged.SourceCount);
            _matrices.Add(_instancedRenderer.BuildInstanceMatrix(merged.LocalPosition, scaleMul));
            _gpuInstanceData.Add(BuildGpuInstanceData(merged.SummedAlertValue));
        }

        _instancedRenderer.SetInstances(_matrices, _gpuInstanceData);
    }

    /// <summary>从原始 _vehiclePoints 构建/读取合并结果（不修改源数据，同输入哈希只算一次）。</summary>
    private bool TryGetMergedPointsForDisplay(out IReadOnlyList<SdMapVehiclePointMerger.MergedPoint> mergedPoints)
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

        int rawCount = CollectMergeInputs(_mergeInputs);
        _lastRawPointCount = rawCount;

        if (rawCount == 0)
        {
            InvalidateMergeCache();
            return false;
        }

        if (_enableProximityMerge)
        {
            SdMapVehiclePointMerger.Merge(_mergeInputs, _mergeDistanceLocal, _mergedPoints);
        }
        else
        {
            for (int i = 0; i < _mergeInputs.Count; i++)
            {
                SdMapVehiclePointMerger.InputPoint input = _mergeInputs[i];
                _mergedPoints.Add(new SdMapVehiclePointMerger.MergedPoint
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

    private int CollectMergeInputs(List<SdMapVehiclePointMerger.InputPoint> output)
    {
        output.Clear();
        if (_vehiclePoints == null)
        {
            return 0;
        }

        for (int i = 0; i < _vehiclePoints.Length; i++)
        {
            VehicleMapPointData data = _vehiclePoints[i];
            if (string.IsNullOrWhiteSpace(data.vehicleId))
            {
                continue;
            }

            if (_strictProvinceBoundary && _provinceBoundary != null &&
                !_provinceBoundary.Contains(data.longitude, data.latitude))
            {
                continue;
            }

            if (!_geoConverter.TryLongitudeLatitudeToLocal(data.longitude, data.latitude, out Vector3 localPos))
            {
                continue;
            }

            output.Add(new SdMapVehiclePointMerger.InputPoint
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

    private static int GetMaxClusterSize(IReadOnlyList<SdMapVehiclePointMerger.MergedPoint> points)
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

    private int ComputeVehiclePointsSourceHash()
    {
        unchecked
        {
            int hash = 17;
            if (_vehiclePoints == null)
            {
                return hash;
            }

            for (int i = 0; i < _vehiclePoints.Length; i++)
            {
                VehicleMapPointData p = _vehiclePoints[i];
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
            int hash = 17;
            hash = hash * 31 + (_enableProximityMerge ? 1 : 0);
            hash = hash * 31 + _mergeDistanceLocal.GetHashCode();
            hash = hash * 31 + _mergeScalePerExtraVehicle.GetHashCode();
            hash = hash * 31 + _mergeScaleMaxMultiplier.GetHashCode();
            hash = hash * 31 + _strictProvinceBoundary.GetHashCode();
            hash = hash * 31 + _dataValueMin.GetHashCode();
            hash = hash * 31 + _dataValueMax.GetHashCode();

            if (_geoConverter != null && _geoConverter.IsReady)
            {
                _geoConverter.GetProvinceLongitudeLatitudeBounds(
                    out double west, out double east, out double south, out double north);
                hash = hash * 31 + west.GetHashCode();
                hash = hash * 31 + east.GetHashCode();
                hash = hash * 31 + south.GetHashCode();
                hash = hash * 31 + north.GetHashCode();
            }

            return hash;
        }
    }

    private bool TrySampleRandomLongitudeLatitude(System.Random rng, out double longitude, out double latitude)
    {
        if (_strictProvinceBoundary && _provinceBoundary != null)
        {
            return _provinceBoundary.TryGetRandomLongitudeLatitude(rng, out longitude, out latitude, _randomMaxAttemptsPerPoint);
        }

        TryGetShandongLongitudeLatitudeBounds(out double westLon, out double eastLon, out double southLat, out double northLat);
        ApplyBoundsInset(ref westLon, ref eastLon, ref southLat, ref northLat, _randomBoundsInset);
        longitude = westLon + rng.NextDouble() * (eastLon - westLon);
        latitude = southLat + rng.NextDouble() * (northLat - southLat);
        return true;
    }

    private bool EnsureProvinceBoundaryLoaded()
    {
        if (_provinceBoundary != null)
        {
            return true;
        }

        if (_shandongBoundaryJson != null && ShandongProvinceBoundary.TryLoad(_shandongBoundaryJson, out _provinceBoundary))
        {
            return true;
        }

        TextAsset fallback = Resources.Load<TextAsset>("ShandongBoundary");
        return fallback != null && ShandongProvinceBoundary.TryLoad(fallback, out _provinceBoundary);
    }

    private void TryGetShandongLongitudeLatitudeBounds(
        out double westLon,
        out double eastLon,
        out double southLat,
        out double northLat)
    {
        westLon = _fallbackWestLongitude;
        eastLon = _fallbackEastLongitude;
        southLat = _fallbackSouthLatitude;
        northLat = _fallbackNorthLatitude;

        if (!_useGeoConverterBounds || _geoConverter == null)
        {
            return;
        }

        _geoConverter.Rebuild();
        _geoConverter.GetProvinceLongitudeLatitudeBounds(out westLon, out eastLon, out southLat, out northLat);
    }

    private static void ApplyBoundsInset(
        ref double westLon,
        ref double eastLon,
        ref double southLat,
        ref double northLat,
        float inset)
    {
        if (inset <= 0f)
        {
            return;
        }

        double lonSpan = (eastLon - westLon) * inset;
        double latSpan = (northLat - southLat) * inset;
        westLon += lonSpan;
        eastLon -= lonSpan;
        southLat += latSpan;
        northLat -= latSpan;
    }

    /// <summary>清理旧版逐 GameObject 打点，避免与 GPU 绘制重复。</summary>
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

        if (_geoConverter == null)
        {
            _geoConverter = GetComponent<SdMapGeoConverter>();
            if (_geoConverter == null)
            {
                _geoConverter = _mapRoot.GetComponent<SdMapGeoConverter>();
            }
        }

        if (_geoConverter != null)
        {
            _geoConverter.Rebuild();
        }

        if (_strictProvinceBoundary)
        {
            EnsureProvinceBoundaryLoaded();
        }

        if (_instancedRenderer == null)
        {
            _instancedRenderer = GetComponent<SdMapVehiclePointInstancedRenderer>();
            if (_instancedRenderer == null)
            {
                _instancedRenderer = gameObject.AddComponent<SdMapVehiclePointInstancedRenderer>();
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
