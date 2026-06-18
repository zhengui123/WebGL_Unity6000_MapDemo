using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class POI_Manager : UnitySingle<POI_Manager>
{
    [SerializeField] private POIData poiData;

    [Header("生成")]
    public Vector3 scale => poiData.obj.transform.localScale;

    [Tooltip("延迟结束后若地理转换未就绪，则每帧等待直至可转换")]
    [SerializeField] private bool _waitForGeoConverterReady = true;

    public List<POIData> poiList = new List<POIData>();

    private void Start()
    {
       
    }

    /// <summary>立即在经纬度处生成 POI。</summary>
    public void SpawnPoi(double x, double y)
    {
        if (poiData?.obj == null)
        {
            Debug.LogWarning("[POI_Manager] poiData 或预制体未配置。");
            return;
        }

        if (!TryGetMapLocalPosition(x, y, out Vector3 localPos))
        {
            return;
        }

        GameObject obj = Instantiate(poiData.obj);
        Quaternion desiredWorldRotation = obj.transform.rotation;
        //Transform mapRoot = ResolveMapRootTransform();
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;
        // sd_map 根节点常带 Z180°，若仅用预制体局部旋转会导致标牌上下倒置；保持生成前世界朝向
        obj.transform.rotation = desiredWorldRotation;

        AddPOI(new POIData(poiData.type, obj, x, y, localPos));
    }

    /// <summary>延迟若干秒后生成 POI；<paramref name="delaySeconds"/> 小于 0 时使用 Inspector 默认值。</summary>
    public void SpawnPoiDelayed(double x, double y)
    {
        StartCoroutine(SpawnPoiDelayedRoutine(x, y));
    }

    private IEnumerator SpawnPoiDelayedRoutine(double x, double y)
    {
        if (_waitForGeoConverterReady)
        {
            yield return WaitUntilGeoConverterReady();
        }

        SpawnPoi(x, y);
    }

    private IEnumerator WaitUntilGeoConverterReady()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            yield break;
        }

        string plateMapName = hub.plateMapName;
        while (!hub.InvokeIsGeoConverterReady(plateMapName))
        {
            yield return null;
        }
    }

    public void AddPOI(POIData data)
    {
        poiList.Add(data);
    }

    public Vector3 GetWorldPosition(double x, double y)
    {
        TryGetMapLocalPosition(x, y, out Vector3 localPosition);
        return localPosition;
    }

    private bool TryGetMapLocalPosition(double longitude, double latitude, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return false;
        }

        return hub.InvokeTryLongitudeLatitudeToLocal(hub.plateMapName, longitude, latitude, out localPosition);
    }

    /// <summary>车辆点位与 mesh 共用 sd_map 根节点局部坐标。</summary>
    private Transform ResolveMapRootTransform()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub != null && !string.IsNullOrEmpty(hub.plateMapName))
        {
            PlateMapGeoConverter[] converters = FindObjectsByType<PlateMapGeoConverter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < converters.Length; i++)
            {
                if (converters[i].gameObject.name == hub.plateMapName)
                {
                    return converters[i].transform;
                }
            }
        }

        return transform;
    }
}
