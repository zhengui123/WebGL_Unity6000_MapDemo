using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 综合区域态势 - 事件范围内车辆最新位置接口。
/// province 为空表示全国，否则为指定省份 adcode；每个 vin 取 process_time 最新。
/// </summary>
public static class LatestVinLocationApi
{
    /// <summary>
    /// 请求车辆最新位置；成功且 code=10000 时自动写入 <see cref="HttpVehicleLocationDataStore"/>。
    /// </summary>
    /// <param name="province">省份 adcode，null/空表示全国。</param>
    /// <param name="region">区域，可空。</param>
    /// <param name="country">国家，可空。</param>
    /// <param name="startTime">查询开始时间，可空（使用项目默认）。</param>
    /// <param name="endTime">查询结束时间，可空（使用项目默认）。</param>
    /// <param name="onCompleted">请求完成回调（含 HTTP 与业务响应）。</param>
    /// <param name="additionalHeaders">额外请求头（与 <see cref="HttpProjectConfig"/> 默认头合并，同键覆盖）。</param>
    public static void RequestLatestVinLocations(
        string province,
        string region,
        string country,
        string startTime,
        string endTime,
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        ComprehensiveRegionRequest requestBody = ComprehensiveRegionRequest.Create(
            province,
            region,
            country,
            startTime,
            endTime);
        string url = HttpProjectConfig.BuildApiUrl(HttpProjectConfig.LatestVinLocationPath);

        HttpService.Instance.PostJson<ComprehensiveRegionRequest, LatestVinLocationResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    ApplySuccessfulResponse(response);
                }

                onCompleted?.Invoke(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>
    /// 解析模拟 JSON 并执行接口成功后的车辆点位同步（不发起 HTTP 请求）。
    /// </summary>
    public static bool TryApplySuccessfulResponseFromJson(string json, out string errorMessage)
    {
        errorMessage = null;
        if (!HttpJsonParser.TryParse(json, out LatestVinLocationResponse response, out string parseError))
        {
            errorMessage = parseError;
            return false;
        }

        if (!response.IsSuccess)
        {
            errorMessage = $"业务 code={response.code}，msg={response.msg}";
            return false;
        }

        ApplySuccessfulResponse(response);
        return true;
    }

    /// <summary>
    /// 接口成功（code=10000）后的统一处理：清空缓存/地图点位并刷新显示。
    /// </summary>
    public static void ApplySuccessfulResponse(LatestVinLocationResponse response)
    {
        if (response == null || !response.IsSuccess)
        {
            Debug.LogWarning("[LatestVinLocationApi] ApplySuccessfulResponse 跳过：响应为空或业务未成功。");
            return;
        }

        ApplyResponseToVehicleMap(response);
    }

    /// <summary>
    /// 清空并写入地图车辆点位：vehicleId=vinEncrypt，经纬度来自接口，alertValue=1，并刷新 GPU 显示。
    /// </summary>
    private static void ApplyResponseToVehicleMap(LatestVinLocationResponse response)
    {
        HttpVehicleLocationDataStore.Instance.ReplaceFromResponse(response);

        VehicleMapPointData[] points = HttpVehicleLocationRecord.ToVehicleMapPointArray(response?.data, alertValue: 1f);
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        string plateMapName = hub.ResolvePlateMapNameForVehiclePoints(HttpProjectConfig.DefaultPlateMapName);
        bool controllerUpdated = hub.PublishSetVehiclePoints(plateMapName, points, syncNow: true);

        if (!controllerUpdated)
        {
            Debug.LogWarning(
                $"[LatestVinLocationApi] 车辆点位已缓存到「{plateMapName}」，但 Controller 未注册；板块启用后将自动同步并刷新。");
            return;
        }

        Debug.Log($"[LatestVinLocationApi] 已同步 {points.Length} 个车辆点位到「{plateMapName}」并刷新显示。");
    }
}
