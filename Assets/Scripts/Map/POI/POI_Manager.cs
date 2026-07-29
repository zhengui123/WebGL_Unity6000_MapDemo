using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI POI 管理：在主相机 Canvas 下的 POIList 中生成固定屏幕尺寸的 POI。
/// </summary>
public class POI_Manager : UnitySingle<POI_Manager>
{
    private const string PoiListRootName = "POIList";

    [System.Serializable]
    private struct PoiTypeConfig
    {
        public POIType type;
        public GameObject prefab;
        public bool usePrefabScale;
        public Vector3 customScale;
    }

    [Header("类型配置")]
    [Tooltip("按 POIType 配置不同 UI 预制体（需带 RectTransform）。")]
    [SerializeField] private List<PoiTypeConfig> _poiTypeConfigs = new List<PoiTypeConfig>();

    [Header("生成")]
    [Tooltip("延迟结束后若地理转换未就绪，则每帧等待直至可转换")]
    [SerializeField] private bool _waitForGeoConverterReady = true;

    [Tooltip("新建 POI 时是否默认开启三维跟随")]
    [SerializeField] private bool _defaultFollowWorldPosition = true;

    public List<POIData> poiList = new List<POIData>();

    private Canvas _poiCanvas;
    private RectTransform _poiListRoot;

    /// <summary>在指定省级 code 对应板块上生成指定类型 UI POI。</summary>
    public void SpawnPoi(string provinceCode, POIType type, double longitude, double latitude)
    {
        if (!TryResolvePoiPrefab(type, out GameObject prefab, out Vector3 targetScale))
        {
            Debug.LogWarning($"[POI_Manager] 未找到类型 {type} 对应 POI 预制体。");
            return;
        }

        if (!TryGetMapLocalPosition(provinceCode, longitude, latitude, out Vector3 localPos))
        {
            return;
        }

        if (!PlateMapAPI.Instance.TryResolvePlateMapName(provinceCode, out string plateMapName))
        {
            return;
        }

        if (!TryEnsurePoiListRoot(out RectTransform poiListRoot))
        {
            return;
        }

        Transform plateRoot = ResolvePlateRootTransform(plateMapName);
        Vector3 worldPos = plateRoot.TransformPoint(localPos);

        GameObject obj = Instantiate(prefab, poiListRoot, false);
        RectTransform rect = obj.transform as RectTransform;
        if (rect == null)
        {
            Debug.LogWarning($"[POI_Manager] Prefab「{prefab.name}」不是 UI（缺少 RectTransform），已销毁。");
            Destroy(obj);
            return;
        }

        rect.localScale = targetScale;

        POIItem item = obj.GetComponent<POIItem>();
        if (item == null)
        {
            item = obj.AddComponent<POIItem>();
        }

        item.BindWorldPosition(worldPos, updateNow: false);
        item.BindPlate(plateRoot);
        item.SetFollowWorldPosition(_defaultFollowWorldPosition);

        AddPOI(new POIData(type, obj, longitude, latitude, localPos));
    }

    /// <summary>延迟生成指定类型 POI。</summary>
    public void SpawnPoiDelayed(string provinceCode, POIType type, double longitude, double latitude)
    {
        StartCoroutine(SpawnPoiDelayedRoutine(provinceCode, type, longitude, latitude));
    }

    private IEnumerator SpawnPoiDelayedRoutine(string provinceCode, POIType type, double longitude, double latitude)
    {
        if (_waitForGeoConverterReady)
        {
            yield return WaitUntilGeoConverterReady(provinceCode);
        }

        SpawnPoi(provinceCode, type, longitude, latitude);
    }

    private IEnumerator WaitUntilGeoConverterReady(string provinceCode)
    {
        while (!PlateMapAPI.Instance.IsGeoConverterReady(provinceCode))
        {
            yield return null;
        }
    }

    public void AddPOI(POIData data)
    {
        poiList.Add(data);
    }

    /// <summary>删除指定类型的全部 POI。</summary>
    public void RemoveAllPoiByType(POIType type)
    {
        for (int i = poiList.Count - 1; i >= 0; i--)
        {
            POIData data = poiList[i];
            if (data == null || data.type != type)
            {
                continue;
            }

            if (data.obj != null)
            {
                Destroy(data.obj);
            }

            poiList.RemoveAt(i);
        }
    }

    /// <summary>删除全部 POI。</summary>
    public void RemoveAllPoi()
    {
        for (int i = poiList.Count - 1; i >= 0; i--)
        {
            POIData data = poiList[i];
            if (data?.obj != null)
            {
                Destroy(data.obj);
            }
        }

        poiList.Clear();
    }

    public Vector3 GetWorldPosition(string provinceCode, double longitude, double latitude)
    {
        TryGetMapLocalPosition(provinceCode, longitude, latitude, out Vector3 localPosition);
        if (!PlateMapAPI.Instance.TryResolvePlateMapName(provinceCode, out string plateMapName))
        {
            return localPosition;
        }

        Transform plateRoot = ResolvePlateRootTransform(plateMapName);
        return plateRoot.TransformPoint(localPosition);
    }

    private bool TryGetMapLocalPosition(string provinceCode, double longitude, double latitude, out Vector3 localPosition)
    {
        return PlateMapAPI.Instance.TryLongitudeLatitudeToLocal(provinceCode, longitude, latitude, out localPosition);
    }

    private bool TryResolvePoiPrefab(POIType type, out GameObject prefab, out Vector3 targetScale)
    {
        for (int i = 0; i < _poiTypeConfigs.Count; i++)
        {
            PoiTypeConfig cfg = _poiTypeConfigs[i];
            if (cfg.type != type || cfg.prefab == null)
            {
                continue;
            }

            prefab = cfg.prefab;
            targetScale = cfg.usePrefabScale ? cfg.prefab.transform.localScale : cfg.customScale;
            return true;
        }

        prefab = null;
        targetScale = Vector3.one;
        return false;
    }

    private Transform ResolvePlateRootTransform(string plateMapName)
    {
        PlateMapGeoConverter[] converters = FindObjectsByType<PlateMapGeoConverter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < converters.Length; i++)
        {
            if (converters[i].gameObject.name == plateMapName ||
                converters[i].transform.name == plateMapName)
            {
                return converters[i].transform;
            }
        }

        return transform;
    }

    /// <summary>获取主摄像机下 Canvas，并确保存在 POIList 父节点。</summary>
    private bool TryEnsurePoiListRoot(out RectTransform poiListRoot)
    {
        if (_poiListRoot != null)
        {
            poiListRoot = _poiListRoot;
            return true;
        }

        if (!TryResolveMainCameraCanvas(out Canvas canvas))
        {
            poiListRoot = null;
            return false;
        }

        _poiCanvas = canvas;
        Transform existing = canvas.transform.Find(PoiListRootName);
        if (existing != null)
        {
            _poiListRoot = existing as RectTransform;
            if (_poiListRoot == null)
            {
                _poiListRoot = existing.gameObject.AddComponent<RectTransform>();
            }

            poiListRoot = _poiListRoot;
            return true;
        }

        GameObject listGo = new GameObject(PoiListRootName, typeof(RectTransform));
        listGo.transform.SetParent(canvas.transform, false);
        _poiListRoot = listGo.GetComponent<RectTransform>();
        _poiListRoot.anchorMin = Vector2.zero;
        _poiListRoot.anchorMax = Vector2.one;
        _poiListRoot.offsetMin = Vector2.zero;
        _poiListRoot.offsetMax = Vector2.zero;
        _poiListRoot.localScale = Vector3.one;
        _poiListRoot.localPosition = Vector3.zero;

        poiListRoot = _poiListRoot;
        return true;
    }

    /// <summary>从 Camera.main 子层级查找 Canvas。</summary>
    private static bool TryResolveMainCameraCanvas(out Canvas canvas)
    {
        canvas = null;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[POI_Manager] 未找到 Camera.main，无法挂载 UI POI。");
            return false;
        }

        canvas = mainCamera.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogWarning(
                $"[POI_Manager] 主摄像机「{mainCamera.name}」下未找到 Canvas，无法挂载 UI POI。");
            return false;
        }

        return true;
    }
}
