using UnityEngine;

/// <summary>
/// 高德地图省级聚焦：按省名称查 <see cref="ChinaProvinceMapDatabase"/> 并调用 <see cref="GaodeMapController"/>。
/// </summary>
[DisallowMultipleComponent]
public class GaodeMapProvinceFocusController : MonoBehaviour
{
    [Header("引用（留空则自动查找）")]
    [SerializeField] private GaodeMapController _gaodeMapController;

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
    /// 聚焦到指定省份（使用数据表中的中心经纬度与 zoom）。
    /// </summary>
    /// <param name="provinceName">如「山东」「山东省」「北京」</param>
    /// <returns>是否找到省份数据并成功发起定位。</returns>
    public bool FocusProvince(string provinceName)
    {
        if (!ChinaProvinceMapDatabase.TryGet(provinceName, out ChinaProvinceMapFocusData data))
        {
            Debug.LogWarning($"[GaodeMapProvinceFocusController] 未找到省份数据：{provinceName}");
            return false;
        }

        return FocusProvince(data);
    }

    /// <summary>按省级 adcode 聚焦（先转省名再查 ChinaProvinceMapDatabase）。</summary>
    public bool FocusProvinceByCode(string provinceCode)
    {
        if (!PlateProvinceFocusResolver.TryProvinceCodeToFocusName(provinceCode, out string provinceName))
        {
            Debug.LogWarning($"[GaodeMapProvinceFocusController] 无法从 code={provinceCode} 解析省名。");
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

        controller.LocateTo(data.Longitude, data.Latitude, data.Zoom);
        Debug.Log($"[GaodeMapProvinceFocusController] 聚焦省份：{data.ProvinceName}（{data.Longitude:F4}, {data.Latitude:F4}, zoom={data.Zoom}）");
        return true;
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
