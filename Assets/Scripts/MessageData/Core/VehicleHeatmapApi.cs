using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆热力图接口：综合区域态势 - 事件范围内车辆最新位置（latestVinLocation）。
/// province 为空表示全国；每个 vin 取 process_time 最新。
/// </summary>
public static class VehicleHeatmapApi
{
    /// <summary>
    /// 请求车辆最新位置；成功且 code=10000 时自动写入 <see cref="HttpVehicleLocationDataStore"/> 并刷新地图热力点。
    /// </summary>
    public static void Request(
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

    /// <summary>使用默认时间范围请求全国车辆热力点。</summary>
    public static void RequestDefault(
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(
            province: string.Empty,
            region: string.Empty,
            country: string.Empty,
            startTime: null,
            endTime: null,
            onCompleted,
            additionalHeaders);
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

    /// <summary>接口成功（code=10000）后的统一处理：写入缓存并刷新地图车辆点位。</summary>
    public static void ApplySuccessfulResponse(LatestVinLocationResponse response)
    {
        if (response == null || !response.IsSuccess)
        {
            Debug.LogWarning("[VehicleHeatmapApi] ApplySuccessfulResponse 跳过：响应为空或业务未成功。");
            return;
        }

        ApplyResponseToVehicleMap(response);
    }

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
                $"[VehicleHeatmapApi] 车辆点位已缓存到「{plateMapName}」，但 Controller 未注册；板块启用后将自动同步并刷新。");
            return;
        }

        Debug.Log($"[VehicleHeatmapApi] 已同步 {points.Length} 个车辆点位到「{plateMapName}」并刷新显示。");
    }
}
