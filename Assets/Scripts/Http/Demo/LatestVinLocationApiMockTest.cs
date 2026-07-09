using UnityEngine;

/// <summary>
/// 不发起 HTTP 请求，使用固定 JSON 模拟 <see cref="VehicleHeatmapApi"/> 成功返回并同步地图车辆点位。
/// </summary>
[DisallowMultipleComponent]
public class LatestVinLocationApiMockTest : MonoBehaviour
{
    /// <summary>Demo 车辆位置接口成功响应样本（与接口测试返回一致）。</summary>
    public const string SampleResponseJson =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"longitude\":\"120.219191\",\"latitude\":\"30.215549\",\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330108\",\"region\":null,\"country\":null,\"vin\":\"111\",\"vinEncrypt\":\"f0e00a29357e02f427e79ba1efae576c\"}," +
        "{\"longitude\":\"120.219191\",\"latitude\":\"30.215549\",\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330108\",\"region\":null,\"country\":null,\"vin\":\"12\",\"vinEncrypt\":\"d1df719fe0aa3f61062c910c081b1cfe2\"}," +
        "{\"longitude\":\"120.219191\",\"latitude\":\"30.215549\",\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330108\",\"region\":null,\"country\":null,\"vin\":\"123***\",\"vinEncrypt\":\"ec1651ed0706e3a59de73e4c3a92f05b\"}," +
        "{\"longitude\":\"120.219191\",\"latitude\":\"30.215549\",\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330108\",\"region\":null,\"country\":null,\"vin\":\"1\",\"vinEncrypt\":\"040c75501cbb9c00da5e158122214704\"}," +
        "{\"longitude\":\"120.219191\",\"latitude\":\"30.215549\",\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330108\",\"region\":null,\"country\":null,\"vin\":\"123******\",\"vinEncrypt\":\"420a510e316e8956d98bee4764a5e00a\"}" +
        "]}";

    [Header("模拟响应")]
    [TextArea(6, 14)]
    [SerializeField] private string _responseJson = SampleResponseJson;

    [Header("触发")]
    [SerializeField] private bool _runOnStart;
    [SerializeField] private KeyCode _runKey = KeyCode.V;

    private void Start()
    {
        if (_runOnStart)
        {
            ApplyMockResponse();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_runKey))
        {
            ApplyMockResponse();
        }
    }

    [ContextMenu("模拟车辆位置接口成功响应")]
    public void ApplyMockResponse()
    {
        if (!VehicleHeatmapApi.TryApplySuccessfulResponseFromJson(_responseJson, out string error))
        {
            Debug.LogError($"[LatestVinLocationApiMockTest] 模拟失败：{error}");
            return;
        }

        Debug.Log("[LatestVinLocationApiMockTest] 已应用模拟车辆位置响应并同步地图点位。");
    }

    [ContextMenu("恢复为内置样本 JSON")]
    private void ResetToSampleJson()
    {
        _responseJson = SampleResponseJson;
    }
}
