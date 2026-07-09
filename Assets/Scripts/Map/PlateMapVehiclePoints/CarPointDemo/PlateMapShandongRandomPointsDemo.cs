using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 山东省内测试点位随机生成（Demo）。经 <see cref="PlateMapVehiclePointEvents"/> 按板块名推送。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapShandongRandomPointsDemo : MonoBehaviour
{
    [Header("目标板块")]
    [SerializeField] private string _provinceCode = "370000";
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

    [Header("大量数据接入测试")]
    [SerializeField] private bool _runBulkDataRoundTripTestOnUpdate = true;
    [Tooltip("清空点位后，等待多少秒再通过 JSON 字符串回灌")]
    [SerializeField] private float _bulkDataReloadDelayAfterClearSeconds = 2f;

    private Coroutine _bulkDataRoundTripCoroutine;

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
        // if(Input.GetKeyDown(KeyCode.M))
        // {
        //   pointCount = int.Parse(inputText.text);
        //     GenerateRandomVehiclePointsInShandongMenu();
        // }
        
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

        if (_bulkDataRoundTripCoroutine != null)
        {
            StopCoroutine(_bulkDataRoundTripCoroutine);
            _bulkDataRoundTripCoroutine = null;
        }
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
        if (!int.TryParse(text, out int count) || count <= 0)
        {
            Debug.LogWarning($"[PlateMapShandongRandomPointsDemo] 无效的点位数量：{text}");
            return;
        }

        pointCount = count;

        if (_bulkDataRoundTripCoroutine != null)
        {
            StopCoroutine(_bulkDataRoundTripCoroutine);
        }

        if (_runBulkDataRoundTripTestOnUpdate && Application.isPlaying)
        {
            _bulkDataRoundTripCoroutine = StartCoroutine(BulkDataRoundTripTestCoroutine(count));
            return;
        }

        GenerateRandomVehiclePointsInShandongMenu();
    }

    private IEnumerator BulkDataRoundTripTestCoroutine(int count)
    {
        if (!TryGenerateVehiclePoints(count, out VehicleMapPointData[] points, out string pointsJson))
        {
            _bulkDataRoundTripCoroutine = null;
            yield break;
        }

        LogPointsJsonOutput(pointsJson, points.Length);

        if (!PushVehiclePointsJson(pointsJson))
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 首次推送点位失败。");
            _bulkDataRoundTripCoroutine = null;
            yield break;
        }

        Debug.Log($"[PlateMapShandongRandomPointsDemo] 已推送 {points.Length} 个点位，即将清空。");

        if (!ClearVehiclePoints())
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 清空点位失败。");
            _bulkDataRoundTripCoroutine = null;
            yield break;
        }

        Debug.Log("[PlateMapShandongRandomPointsDemo] 点位已清空。");

        float delay = Mathf.Max(0f, _bulkDataReloadDelayAfterClearSeconds);
        if (delay > 0f)
        {
            Debug.Log(
                $"[PlateMapShandongRandomPointsDemo] 清空后等待 {delay:F1}s，再通过 JSON 字符串回灌。");
            yield return new WaitForSeconds(delay);
        }

        if (!ImportVehiclePointsFromJson(pointsJson))
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] JSON 字符串回灌失败。");
            _bulkDataRoundTripCoroutine = null;
            yield break;
        }

        Debug.Log(
            $"[PlateMapShandongRandomPointsDemo] 大量数据接入测试完成：清空后延时 {delay:F1}s，已用字符串回灌 {points.Length} 个点位。");
        _bulkDataRoundTripCoroutine = null;
    }

    private void LogPointsJsonOutput(string pointsJson, int pointCount)
    {
        int length = pointsJson != null ? pointsJson.Length : 0;
        Debug.Log(
            $"[PlateMapShandongRandomPointsDemo] 点位 JSON 字符串（{pointCount} 个，{length} 字符）：{pointsJson}");
    }

    /// <summary>生成点位并序列化为 JSON 字符串。</summary>
    public bool TryGenerateVehiclePoints(int count, out VehicleMapPointData[] points, out string pointsJson)
    {
        points = PlateMapShandongTestPointGenerator.GenerateFiltered(
            _plateMapName,
            _provinceFilter,
            count,
            _randomSeed,
            _useProvinceBoundarySampling);
        pointsJson = null;

        if (points == null || points.Length == 0)
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 未生成任何点位。");
            points = Array.Empty<VehicleMapPointData>();
            return false;
        }

        pointsJson = VehicleMapPointJson.ToJson(points);
        return true;
    }

    /// <summary>清空当前板块车辆点位。</summary>
    public bool ClearVehiclePoints()
    {
        return PushVehiclePointsJson(VehicleMapPointJson.ToJson(Array.Empty<VehicleMapPointData>()));
    }

    /// <summary>从 JSON 字符串回灌车辆点位。</summary>
    public bool ImportVehiclePointsFromJson(string pointsJson)
    {
        return PushVehiclePointsJson(pointsJson);
    }

    private bool PushVehiclePointsJson(string pointsJson)
    {
        if (string.IsNullOrWhiteSpace(pointsJson))
        {
            Debug.LogWarning("[PlateMapShandongRandomPointsDemo] 点位 JSON 为空。");
            return false;
        }

        if (_pushViaJsonApi)
        {
            return PlateMapAPI.Instance.UpdateVehiclePointsFromJson(_provinceCode, pointsJson);
        }

        if (!VehicleMapPointJson.TryParse(pointsJson, out VehicleMapPointData[] points, out string error))
        {
            Debug.LogError($"[PlateMapShandongRandomPointsDemo] JSON 解析失败：{error}");
            return false;
        }

        return Hub.PublishSetVehiclePoints(_plateMapName, points);
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

        if (!TryGenerateVehiclePoints(count, out VehicleMapPointData[] points, out string pointsJson))
        {
            return;
        }

        bool ok = PushVehiclePointsJson(pointsJson);

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
