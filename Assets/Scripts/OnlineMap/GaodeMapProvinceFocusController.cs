using UnityEngine;

/// <summary>
/// 高德地图区域聚焦：
/// 国内按省名称/省 code 查 <see cref="ChinaProvinceMapDatabase"/>，
/// 国外按国家名称/国家 code 查 <see cref="WorldCountryMapDatabase"/>，
/// 最终统一调用 <see cref="GaodeMapController"/>。
/// </summary>
[DisallowMultipleComponent]
public class GaodeMapProvinceFocusController : MonoBehaviour
{
    [Header("引用（留空则自动查找）")]
    [SerializeField] private GaodeMapController _gaodeMapController;

    [Header("国外聚焦")]
    [Tooltip("国外国家二维地图起始 zoom（省→车辆阶段一 LocateTo）；国内仍用数据表 Zoom")]
    [SerializeField] private float _foreignFocusZoom = 5f;

    private static GaodeMapProvinceFocusController _instance;

    /// <summary>场景中的省级聚焦控制器。</summary>
    public static GaodeMapProvinceFocusController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GaodeMapProvinceFocusController>();
            }

            return _instance;
        }
    }

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
    /// 聚焦到指定区域（国内省份 / 国外国家，使用数据表中的中心经纬度与 zoom）。
    /// </summary>
    /// <param name="provinceName">如「山东」「山东省」「北京」或「日本」「德国」</param>
    /// <returns>是否找到区域数据并成功发起定位。</returns>
    public bool FocusProvince(string provinceName)
    {
        if (!TryResolveFocusDataByName(provinceName, out ChinaProvinceMapFocusData data))
        {
            Debug.LogWarning($"[GaodeMapProvinceFocusController] 未找到区域数据：{provinceName}");
            return false;
        }

        return FocusProvince(data);
    }

    /// <summary>按区域 code 聚焦（国内=省级 adcode，国外=国家 secondClassCode）。</summary>
    public bool FocusProvinceByCode(string provinceCode)
    {
        if (WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign &&
            WorldCountryMapDatabase.TryGetByCode(provinceCode, out ChinaProvinceMapFocusData foreignData))
        {
            return FocusProvince(foreignData);
        }

        if (!PlateProvinceFocusResolver.TryProvinceCodeToFocusName(provinceCode, out string provinceName))
        {
            Debug.LogWarning($"[GaodeMapProvinceFocusController] 无法从 code={provinceCode} 解析区域名。");
            return false;
        }

        return FocusProvince(provinceName);
    }

    /// <summary>使用已有省级数据聚焦。</summary>
    public bool FocusProvince(ChinaProvinceMapFocusData data)
    {
        if (data == null)
        {
            return false;
        }

        GaodeMapController controller = ResolveReferences();
        if (controller == null)
        {
            Debug.LogWarning("[GaodeMapProvinceFocusController] 未找到 GaodeMapController。");
            return false;
        }

        float zoom = ResolveFocusZoom(data);
        controller.LocateTo(data.Longitude, data.Latitude, Mathf.RoundToInt(zoom));
        Debug.Log(
            $"[GaodeMapProvinceFocusController] 聚焦区域：{data.ProvinceName}（{data.Longitude:F4}, {data.Latitude:F4}, zoom={zoom}）");
        return true;
    }

    /// <summary>国内用数据表 Zoom；国外统一用 Inspector 起始 zoom。</summary>
    private float ResolveFocusZoom(ChinaProvinceMapFocusData data)
    {
        if (WorldMapRegionContext.IsInitialized &&
            WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign)
        {
            return _foreignFocusZoom;
        }

        return data != null ? data.Zoom : _foreignFocusZoom;
    }

    private static bool TryResolveFocusDataByName(string regionName, out ChinaProvinceMapFocusData data)
    {
        data = null;
        if (ChinaProvinceMapDatabase.TryGet(regionName, out data))
        {
            return true;
        }

        return WorldCountryMapDatabase.TryGetByName(regionName, out data);
    }

    private GaodeMapController ResolveReferences()
    {
        if (_gaodeMapController == null)
        {
            _gaodeMapController = GetComponent<GaodeMapController>();
        }

        if (_gaodeMapController == null)
        {
            _gaodeMapController = GaodeMapController.Instance;
        }

        return _gaodeMapController;
    }

#if UNITY_EDITOR
    [ContextMenu("测试：聚焦山东省")]
    private void EditorTestFocusShandong()
    {
        FocusProvince("山东");
    }

    [ContextMenu("测试：聚焦广东省")]
    private void EditorTestFocusGuangdong()
    {
        FocusProvince("广东");
    }
#endif
}
