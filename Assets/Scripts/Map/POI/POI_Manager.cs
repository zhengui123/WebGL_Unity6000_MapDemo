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

        GameObject obj = Instantiate(poiData.obj);

        obj.transform.parent = transform;
        obj.transform.localPosition = GetWorldPosition(x, y);
        obj.transform.localScale = scale;

        AddPOI(new POIData(poiData.type, obj, x, y, obj.transform.localPosition));
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
        PlateMapVehiclePointEvents.Instance.InvokeTryLongitudeLatitudeToLocal(
            PlateMapVehiclePointEvents.Instance.plateMapName, x, y, out Vector3 localPosition);
        return localPosition;
    }
}
