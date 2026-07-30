using UnityEngine;

/// <summary>
/// 不发起 HTTP 请求，使用固定 JSON 模拟 <see cref="VehicleHeatmapApi"/> 成功返回并同步地图车辆点位。
/// 按 V 键应用；内置样本仅含山东(370000)与黑龙江(230000)范围测试点。
/// </summary>
[DisallowMultipleComponent]
public class LatestVinLocationApiMockTest : MonoBehaviour
{
    /// <summary>全国视图用：山东 + 黑龙江各若干测试点（x=经度，y=纬度，c=省级 code）。</summary>
    public const string SampleResponseJson =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        // 山东
        "{\"x\":117.120000,\"y\":36.650000,\"c\":\"370000\"}," +
        "{\"x\":120.380000,\"y\":36.070000,\"c\":\"370000\"}," +
        "{\"x\":118.790000,\"y\":36.880000,\"c\":\"370000\"}," +
        "{\"x\":119.460000,\"y\":35.420000,\"c\":\"370000\"}," +
        // 黑龙江
        "{\"x\":126.530000,\"y\":45.800000,\"c\":\"230000\"}," +
        "{\"x\":123.950000,\"y\":47.350000,\"c\":\"230000\"}," +
        "{\"x\":130.350000,\"y\":46.800000,\"c\":\"230000\"}," +
        "{\"x\":128.810000,\"y\":47.720000,\"c\":\"230000\"}" +
        "]}";

    /// <summary>仅山东省境测试点。</summary>
    public const string SampleShandongJson =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"x\":117.120000,\"y\":36.650000,\"c\":\"370000\"}," +
        "{\"x\":120.380000,\"y\":36.070000,\"c\":\"370000\"}," +
        "{\"x\":118.790000,\"y\":36.880000,\"c\":\"370000\"}," +
        "{\"x\":119.460000,\"y\":35.420000,\"c\":\"370000\"}," +
        "{\"x\":116.580000,\"y\":35.070000,\"c\":\"370000\"}" +
        "]}";

    /// <summary>仅黑龙江省境测试点。</summary>
    public const string SampleHeilongjiangJson =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"x\":126.530000,\"y\":45.800000,\"c\":\"230000\"}," +
        "{\"x\":123.950000,\"y\":47.350000,\"c\":\"230000\"}," +
        "{\"x\":130.350000,\"y\":46.800000,\"c\":\"230000\"}," +
        "{\"x\":128.810000,\"y\":47.720000,\"c\":\"230000\"}," +
        "{\"x\":131.150000,\"y\":45.750000,\"c\":\"230000\"}" +
        "]}";

    private const string ShandongCode = "370000";
    private const string HeilongjiangCode = "230000";

    [Header("模拟响应")]
    [TextArea(6, 14)]
    [SerializeField] private string _responseJson = SampleResponseJson;

    [Tooltip("热力图绘制目标：0=全国；370000=山东；230000=黑龙江")]
    [SerializeField] private string _provinceCode = "0";

    [Header("触发")]
    [SerializeField] private bool _runOnStart;
    [SerializeField] private KeyCode _runKey = KeyCode.V;

    [Tooltip("按 V 时是否在 全国→山东→黑龙江 间循环切换样本并应用")]
    [SerializeField] private bool _cycleTargetOnKey = true;

    private int _cycleIndex;

    private void Start()
    {
        if (_runOnStart)
        {
            ApplyMockResponse();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_runKey))
        {
            return;
        }

        if (_cycleTargetOnKey)
        {
            CycleTargetAndApply();
            return;
        }

        ApplyMockResponse();
    }

    /// <summary>按 V：全国(0) → 山东(370000) → 黑龙江(230000) → 循环。</summary>
    private void CycleTargetAndApply()
    {
        _cycleIndex = (_cycleIndex + 1) % 3;
        switch (_cycleIndex)
        {
            case 1:
                _provinceCode = ShandongCode;
                _responseJson = SampleShandongJson;
                break;
            case 2:
                _provinceCode = HeilongjiangCode;
                _responseJson = SampleHeilongjiangJson;
                break;
            default:
                _provinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;
                _responseJson = SampleResponseJson;
                break;
        }

        ApplyMockResponse();
    }

    [ContextMenu("模拟车辆位置接口成功响应")]
    public void ApplyMockResponse()
    {
        if (!VehicleHeatmapApi.TryApplySuccessfulResponseFromJson(_responseJson, _provinceCode, out string error))
        {
            Debug.LogError($"[LatestVinLocationApiMockTest] 模拟失败：{error}");
            return;
        }

        Debug.Log(
            $"[LatestVinLocationApiMockTest] 已应用模拟热力点（按 data[].c 分省）");
    }

    [ContextMenu("加载全国样本(山东+黑龙江)")]
    private void LoadNationalSample()
    {
        _provinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;
        _responseJson = SampleResponseJson;
        _cycleIndex = 0;
    }

    [ContextMenu("加载山东样本")]
    private void LoadShandongSample()
    {
        _provinceCode = ShandongCode;
        _responseJson = SampleShandongJson;
        _cycleIndex = 1;
    }

    [ContextMenu("加载黑龙江样本")]
    private void LoadHeilongjiangSample()
    {
        _provinceCode = HeilongjiangCode;
        _responseJson = SampleHeilongjiangJson;
        _cycleIndex = 2;
    }

    [ContextMenu("恢复为全国内置样本 JSON")]
    private void ResetToSampleJson()
    {
        LoadNationalSample();
    }
}
