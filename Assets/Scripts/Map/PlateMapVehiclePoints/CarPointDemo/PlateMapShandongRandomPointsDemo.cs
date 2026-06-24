using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 山东省内测试点位随机生成（Demo）。经 <see cref="PlateMapVehiclePointEvents"/> 按板块名推送。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapShandongRandomPointsDemo : MonoBehaviour
{
    [Header("目标板块")]
    [SerializeField] private string _plateMapName = "sd_map (1)";

    [Header("省界与采样")]
    [SerializeField] private PlateMapShandongProvincePointFilter _provinceFilter = new PlateMapShandongProvincePointFilter();

    [Header("随机生成")]
    [Tooltip("勾选后在省界多边形内采样；取消则仅在 Province Filter 的 fallback 经纬度矩形内均匀随机")]
    [SerializeField] private bool _useProvinceBoundarySampling = true;
    [SerializeField] private int pointCount = 100;
    [SerializeField] private int _randomGenerateCount = 100;
    [SerializeField] private int _randomSeed;


    [Header("事件")]
    [SerializeField] private bool _logRebuildCompletedAction;
    [SerializeField] private bool _pushViaJsonApi = true;

    private PlateMapVehiclePointEvents Hub => PlateMapVehiclePointEvents.Instance;

    private void OnEnable()
    {
        if (_logRebuildCompletedAction)
        {
            Hub.VehiclePointsChangedAction += OnVehiclePointsChangedForLog;
        }

        if (ShouldSkipProvinceFilterRegistration())
        {
            return;
        }

        RegisterProvinceFilter();
    }

    public InputField inputText;
    public void Update()
    {
        if(Input.GetKeyDown(KeyCode.M))
        {
          pointCount = int.Parse(inputText.text);
            GenerateRandomVehiclePointsInShandongMenu();
        }
        
    }

    private void OnDisable()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        if (!ShouldSkipProvinceFilterRegistration())
        {
            hub.UnregisterShouldIncludePointAction(_plateMapName);
        }

        hub.VehiclePointsChangedAction -= OnVehiclePointsChangedForLog;
    }

    /// <summary>同物体已有 GeoConverter 时，省界过滤统一由其 _useProvinceBoundary 控制。</summary>
    private bool ShouldSkipProvinceFilterRegistration()
    {
        return GetComponent<PlateMapGeoConverter>() != null;
    }

    private void RegisterProvinceFilter()
    {
        Hub.RegisterShouldIncludePointAction(_plateMapName, ShouldIncludePointAction);
    }

    private void OnVehiclePointsChangedForLog(string plateMapName, VehicleMapPointData[] points)
    {
        if (plateMapName != _plateMapName)
        {
            return;
        }

        int count = points != null ? points.Length : 0;
        Debug.Log($"[PlateMapShandongRandomPointsDemo] 板块「{plateMapName}」点位已更新，共 {count} 个。");
    }


    private bool ShouldIncludePointAction(VehicleMapPointData data)
    {
        if (!_provinceFilter.StrictProvinceBoundary)
        {
            return true;
        }

        return _provinceFilter.ContainsInProvince(data.longitude, data.latitude);
    }


    /// <summary>从 UI 输入文本解析点位数量并重新生成热力图数据。</summary>
    public void UpdateVehicleHeatmapFromInput(string text)
    {
        pointCount = int.Parse(text);
        GenerateRandomVehiclePointsInShandongMenu();
    }

    [ContextMenu("随机生成100个山东省内点位")]
    public void GenerateRandomVehiclePointsInShandongMenu()
    {
        GenerateRandomVehiclePointsInShandong(pointCount);
    }

    public void GenerateRandomVehiclePointsInShandong(int count = -1)
    {
        if (count <= 0)
        {
            count = _randomGenerateCount;
        }

        VehicleMapPointData[] points = PlateMapShandongTestPointGenerator.GenerateFiltered(
            _plateMapName,
            _provinceFilter,
            count,
            _randomSeed,
            _useProvinceBoundarySampling);

        if (points.Length == 0)
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 未生成任何点位。");
            return;
        }

        bool ok = _pushViaJsonApi
            ? PlateMapAPI.Instance.UpdateVehiclePointsFromJson(_plateMapName, VehicleMapPointJson.ToJson(points))
            : Hub.PublishSetVehiclePoints(_plateMapName, points);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        Debug.Log(ok
            ? $"[PlateMapShandongRandomPointsDemo] 已推送到 {_plateMapName}，{points.Length} 个点（省界采样={_useProvinceBoundarySampling}）。"
            : "[PlateMapShandongRandomPointsDemo] 推送失败。");
    }
}
