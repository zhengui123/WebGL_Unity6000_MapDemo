using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆热力图接口：综合区域态势 - 事件范围内热力点（latestVinLocation）。
/// 请求含 isReplay；响应 data 为 {x,y,c}，按 c（省级 adcode）分组后同步到各省板块。
/// </summary>
public static class VehicleHeatmapApi
{
    /// <summary>
    /// 请求热力点；成功且 code=10000 时全量覆盖缓存，并按 data[].c 分省刷新地图点位。
    /// </summary>
    public static void Request(
        string provinceCode,
        string region,
        string country,
        string startTime,
        string endTime,
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null,
        bool isReplay = false)
    {
        ComprehensiveRegionRequest requestBody = ComprehensiveRegionRequest.Create(
            provinceCode,
            region,
            country,
            startTime,
            endTime,
            isReplay);
        string url = HttpProjectConfig.BuildApiUrl(HttpProjectConfig.LatestVinLocationPath);

        HttpService.Instance.PostJson<ComprehensiveRegionRequest, LatestVinLocationResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson(result, response);

                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    ApplySuccessfulResponse(response);
                }

                onCompleted?.Invoke(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>使用默认时间范围请求全国热力点（isReplay=false）。</summary>
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
            additionalHeaders,
            isReplay: false);
    }

    /// <summary>
    /// 解析模拟 JSON 并执行接口成功后的车辆点位同步（不发起 HTTP 请求）。
    /// provinceCode 参数保留兼容，实际分组以 data[].c 为准。
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

        ApplySuccessfulResponse(response);
        return true;
    }

    /// <summary>接口成功后：全量替换缓存，并按 data[].c 分组同步到各省板块。</summary>
    public static void ApplySuccessfulResponse(LatestVinLocationResponse response, string provinceCode = null)
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

        Dictionary<string, List<LatestVinLocationItem>> groups = GroupByProvinceCode(response?.data);
        if (groups.Count == 0)
        {
            Debug.LogWarning("[VehicleHeatmapApi] 响应无有效省级分组（data[].c），未刷新地图点位。");
            return;
        }

        int provinceCount = 0;
        int pointCount = 0;
        foreach (KeyValuePair<string, List<LatestVinLocationItem>> pair in groups)
        {
            VehicleMapPointData[] points = HttpVehicleLocationRecord.ToVehicleMapPointArray(
                pair.Value.ToArray(),
                alertValue: 1f);
            bool controllerUpdated = PlateMapAPI.Instance.UpdateVehiclePoints(pair.Key, points, syncNow: true);
            provinceCount++;
            pointCount += points.Length;

            if (!controllerUpdated)
            {
                PlateMapAPI.Instance.TryResolvePlateMapName(pair.Key, out string plateMapName);
                Debug.LogWarning(
                    $"[VehicleHeatmapApi] 点位已按 c={pair.Key} 分组（{plateMapName}），" +
                    "但 Controller 未注册或未启用；启用后将从 Hub 缓存同步。");
            }
        }

        Debug.Log(
            $"[VehicleHeatmapApi] 已按 c 同步 {pointCount} 个热力点到 {provinceCount} 个省级板块。");
    }

    /// <summary>按 data[].c 做省级分组（c 必须有值）。</summary>
    private static Dictionary<string, List<LatestVinLocationItem>> GroupByProvinceCode(
        LatestVinLocationItem[] items)
    {
        Dictionary<string, List<LatestVinLocationItem>> groups =
            new Dictionary<string, List<LatestVinLocationItem>>();
        if (items == null || items.Length == 0)
        {
            return groups;
        }

        for (int i = 0; i < items.Length; i++)
        {
            LatestVinLocationItem item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.c))
            {
                Debug.LogWarning($"[VehicleHeatmapApi] 跳过无省级 code(c) 的点位 index={i}。");
                continue;
            }

            string code = item.c.Trim();
            if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(code, out string normalized) &&
                !string.IsNullOrWhiteSpace(normalized))
            {
                code = normalized;
            }

            if (!groups.TryGetValue(code, out List<LatestVinLocationItem> list))
            {
                list = new List<LatestVinLocationItem>();
                groups[code] = list;
            }

            list.Add(item);
        }

        return groups;
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
