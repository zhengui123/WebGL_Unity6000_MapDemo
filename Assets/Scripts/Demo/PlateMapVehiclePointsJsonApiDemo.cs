using System.IO;
using UnityEngine;

/// <summary>
/// 演示 <see cref="PlateMapVehiclePointEvents.UpdateVehiclePointsFromJson"/> 按板块名推送 JSON。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapVehiclePointsJsonApiDemo : UnitySingle<PlateMapVehiclePointsJsonApiDemo>
{
    [Header("目标板块")]
    [SerializeField] private string _plateMapName = "sd_map (1)";

    [Header("测试数据生成")]
    [SerializeField] private PlateMapShandongProvincePointFilter _provinceFilter = new PlateMapShandongProvincePointFilter();
    [SerializeField] private int _randomGenerateCount = 20;
    [SerializeField] private int _randomSeed = 42;

    [Header("JSON 样本")]
    [SerializeField] private TextAsset _sampleJsonAsset;
    [SerializeField] private string _exportJsonPath = "Scripts/Map/PlateMapVehiclePoints/Data/SampleVehiclePoints.json";

    private PlateMapVehiclePointEvents Hub => PlateMapVehiclePointEvents.Instance;

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            GenerateTestJsonAndPushApi();
        }
    }

    [ContextMenu("生成测试JSON并推送API")]
    public void GenerateTestJsonAndPushApi()
    {
        VehicleMapPointData[] points = PlateMapShandongTestPointGenerator.Generate(
            _plateMapName, _provinceFilter, _randomGenerateCount, _randomSeed);

        if (points.Length == 0)
        {
            Debug.LogError("[PlateMapVehiclePointsJsonApiDemo] 未生成任何点位。");
            return;
        }

        string json = VehicleMapPointJson.ToJson(points);
        LogJsonPreview(json);

#if UNITY_EDITOR
        TryExportJsonToAssets(json);
#endif

        bool ok = PlateMapAPI.Instance.UpdateVehiclePointsFromJson(_plateMapName, json);
        Debug.Log(ok
            ? $"[PlateMapVehiclePointsJsonApiDemo] 已推送到 {_plateMapName}，{points.Length} 个点。"
            : "[PlateMapVehiclePointsJsonApiDemo] 推送失败。");
    }

    [ContextMenu("从 SampleJson 资源推送API")]
    public void PushSampleJsonAsset()
    {
        if (_sampleJsonAsset == null)
        {
            Debug.LogError("[PlateMapVehiclePointsJsonApiDemo] 未指定 Sample Json Asset。");
            return;
        }

        bool ok = PlateMapAPI.Instance.UpdateVehiclePointsFromJson(_plateMapName, _sampleJsonAsset.text);
        Debug.Log(ok ? $"[PlateMapVehiclePointsJsonApiDemo] 样本 JSON 已推送到 {_plateMapName}。" : "[PlateMapVehiclePointsJsonApiDemo] 推送失败。");
    }

    private static void LogJsonPreview(string json)
    {
        const int maxLen = 280;
        string preview = json.Length <= maxLen ? json : json.Substring(0, maxLen) + "...";
        Debug.Log($"[PlateMapVehiclePointsJsonApiDemo] JSON 预览：{preview}");
    }

#if UNITY_EDITOR
    /// <summary>将 JSON 写入 Assets 指定路径。</summary>
    private void TryExportJsonToAssets(string json)
    {
        if (string.IsNullOrWhiteSpace(_exportJsonPath))
        {
            return;
        }

        string fullPath = Path.Combine(Application.dataPath, _exportJsonPath.Replace('\\', '/'));
        string dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, json);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[PlateMapVehiclePointsJsonApiDemo] 已导出 JSON：Assets/{_exportJsonPath}");
    }
#endif
}
