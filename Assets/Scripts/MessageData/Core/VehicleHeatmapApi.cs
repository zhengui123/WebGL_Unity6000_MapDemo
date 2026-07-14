using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆热力图接口：综合区域态势 - 事件范围内热力点（latestVinLocation）。
/// province 为省级 adcode；空或 "0" 表示全国板块。响应 data 为 {x,y} 点列表。
/// </summary>
public static class VehicleHeatmapApi
{
    /// <summary>
    /// 请求热力点；成功且 code=10000 时全量覆盖缓存并刷新对应省份地图点位。
    /// </summary>
    public static void Request(
        string provinceCode,
        string region,
        string country,
        string startTime,
        string endTime,
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        ComprehensiveRegionRequest requestBody = ComprehensiveRegionRequest.Create(
            provinceCode,
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
                LogResponseJson(result, response);

                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    ApplySuccessfulResponse(response, provinceCode);
                }

                onCompleted?.Invoke(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>使用默认时间范围请求全国热力点。</summary>
    public static void RequestDefault(
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(
            provinceCode: string.Empty,
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
    public static bool TryApplySuccessfulResponseFromJson(string json, string provinceCode, out string errorMessage)
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

        ApplySuccessfulResponse(response, provinceCode);
        return true;
    }

    /// <summary>接口成功后：全量替换缓存并同步到指定 provinceCode 板块。</summary>
    public static void ApplySuccessfulResponse(LatestVinLocationResponse response, string provinceCode = null)
    {
        if (response == null || !response.IsSuccess)
        {
            Debug.LogWarning("[VehicleHeatmapApi] ApplySuccessfulResponse 跳过：响应为空或业务未成功。");
            return;
        }

        string resolvedCode = ResolveProvinceCodeForMap(provinceCode);
        ApplyResponseToVehicleMap(response, resolvedCode);
    }

    private static string ResolveProvinceCodeForMap(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return HttpProjectConfig.DefaultProvinceCode;
        }

        if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalized))
        {
            return normalized;
        }

        return HttpProjectConfig.DefaultProvinceCode;
    }

    private static void ApplyResponseToVehicleMap(LatestVinLocationResponse response, string provinceCode)
    {
        // 每次请求完成：清空旧缓存后写入新结果
        HttpVehicleLocationDataStore.Instance.ReplaceFromResponse(response);

        VehicleMapPointData[] points = HttpVehicleLocationRecord.ToVehicleMapPointArray(response?.data, alertValue: 1f);
        bool controllerUpdated = PlateMapAPI.Instance.UpdateVehiclePoints(provinceCode, points, syncNow: true);

        if (!controllerUpdated)
        {
            PlateMapAPI.Instance.TryResolvePlateMapName(provinceCode, out string plateMapName);
            Debug.LogWarning(
                $"[VehicleHeatmapApi] 点位已缓存到 provinceCode={provinceCode}（{plateMapName}），但 Controller 未注册或未启用；启用后将从 Hub 缓存同步。");
            return;
        }

        Debug.Log($"[VehicleHeatmapApi] 已同步 {points.Length} 个热力点到 provinceCode={provinceCode} 并刷新显示。");
    }

    private static void LogResponseJson(HttpRequestResult result, LatestVinLocationResponse response)
    {
        if (result == null)
        {
            Debug.LogWarning("[VehicleHeatmapApi] 请求结果为空。");
            return;
        }

        string body = string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody;
        if (result.IsCancelled)
        {
            Debug.Log("[VehicleHeatmapApi] 请求已取消。");
            return;
        }

        if (!result.IsSuccess)
        {
            Debug.LogWarning(
                $"[VehicleHeatmapApi] 请求失败，状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{body}");
            return;
        }

        int count = response?.data != null ? response.data.Length : 0;
        bool bizOk = response != null && response.IsSuccess;
        Debug.Log(
            $"[VehicleHeatmapApi] 请求成功，状态码={result.StatusCode}，业务成功={bizOk}，点数={count}\n响应 JSON：\n{body}");
    }
}
