using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class POI_Manager : UnitySingle<POI_Manager>
{
    [System.Serializable]
    private struct PoiTypeConfig
    {
        public POIType type;
        public GameObject prefab;
        public bool usePrefabScale;
        public Vector3 customScale;
    }

    [Header("类型配置")]
    [Tooltip("按 POIType 配置不同预制体。")]
    [SerializeField] private List<PoiTypeConfig> _poiTypeConfigs = new List<PoiTypeConfig>();

    [Header("生成")]
    [Tooltip("延迟结束后若地理转换未就绪，则每帧等待直至可转换")]
    [SerializeField] private bool _waitForGeoConverterReady = true;

    public List<POIData> poiList = new List<POIData>();

    /// <summary>在指定省级 code 对应板块上生成指定类型 POI。</summary>
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

        Transform plateRoot = ResolvePlateRootTransform(plateMapName);
        GameObject obj = Instantiate(prefab);
        Quaternion desiredWorldRotation = obj.transform.rotation;
        obj.transform.SetParent(plateRoot, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = targetScale;
        obj.transform.rotation = desiredWorldRotation;
        // 统一挂到 POI_Manager 下，保留已计算好的世界位姿
        obj.transform.SetParent(transform, true);
        Vector3 managerLocalPos = obj.transform.localPosition;
        managerLocalPos.y = 0f;
        obj.transform.localPosition = managerLocalPos;

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
}
