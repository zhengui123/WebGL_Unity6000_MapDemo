using UnityEngine;

/// <summary>
/// 场景中 GaodeMap（Online Maps + 高德瓦片）的控制脚本。
/// </summary>
[DisallowMultipleComponent]
public class GaodeMapController : MonoBehaviour
{
    [Header("引用（留空则取同物体 OnlineMaps）")]
    [SerializeField] private OnlineMaps _onlineMaps;

    private static GaodeMapController _instance;

    /// <summary>场景中的 GaodeMap 控制器。</summary>
    public static GaodeMapController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GaodeMapController>();
            }

            return _instance;
        }
    }

    public OnlineMaps OnlineMaps => _onlineMaps;

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 将地图中心定位到指定 WGS84 经纬度；zoom 不传则保持当前缩放级别。
    /// </summary>
    /// <param name="longitude">经度（-180 ~ 180）</param>
    /// <param name="latitude">纬度（-90 ~ 90）</param>
    /// <param name="zoom">可选缩放级别；null 表示不改变 zoom</param>
    public void LocateTo(double longitude, double latitude, int? zoom = null)
    {
        if (!TryGetMap(out OnlineMaps map))
        {
            return;
        }

        if (zoom.HasValue)
        {
            map.SetPositionAndZoom(longitude, latitude, zoom.Value);
        }
        else
        {
            map.SetPosition(longitude, latitude);
        }
    }

    /// <summary>定位到指定经纬度，并设置缩放级别。</summary>
    public void LocateTo(double longitude, double latitude, int zoom)
    {
        LocateTo(longitude, latitude, (int?)zoom);
    }

    /// <summary>读取当前地图中心经纬度。</summary>
    public bool TryGetCenter(out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;

        if (!TryGetMap(out OnlineMaps map))
        {
            return false;
        }

        map.GetPosition(out longitude, out latitude);
        return true;
    }

    /// <summary>读取当前缩放级别。</summary>
    public int GetCurrentZoom()
    {
        return TryGetMap(out OnlineMaps map) ? map.zoom : 0;
    }

    private void ResolveReferences()
    {
        if (_onlineMaps == null)
        {
            _onlineMaps = GetComponent<OnlineMaps>();
        }
    }

    private bool TryGetMap(out OnlineMaps map)
    {
        ResolveReferences();
        map = _onlineMaps;
        if (map != null)
        {
            return true;
        }

        Debug.LogWarning("[GaodeMapController] 未找到 OnlineMaps 组件。");
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("测试：定位到北京天安门")]
    private void EditorTestLocateBeijing()
    {
        LocateTo(116.397128, 39.916527, 15);
    }
#endif
}
