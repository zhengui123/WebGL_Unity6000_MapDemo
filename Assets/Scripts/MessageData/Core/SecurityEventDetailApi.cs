using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件溯源详情接口（getSourceEventDetail）：与其它业务接口共用主机与默认请求头。
/// 成功解析后缓存数据、刷新 GJ_Panel，并按经纬度生成 POI。
/// </summary>
public static class SecurityEventDetailApi
{
    /// <summary>请求完成（成功或失败均触发；成功时已先执行 <see cref="ApplySuccessfulResponse"/>）。</summary>
    public static event Action<HttpRequestResult, SecurityEventDetailResponse> RequestCompleted;

    /// <summary>最近一次成功应用的完整响应。</summary>
    public static SecurityEventDetailResponse LastResponse { get; private set; }

    /// <summary>最近一次成功应用的 data。</summary>
    public static SecurityEventDetailData LastData => LastResponse != null ? LastResponse.data : null;

    /// <summary>构建事件溯源详情接口完整 URL。</summary>
    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildApiUrl(HttpProjectConfig.SecurityEventDetailPath);
    }

    /// <summary>
    /// POST 查询事件溯源详情；参数为空时使用 <see cref="SecurityEventDetailRequest"/> 默认值。
    /// </summary>
    public static void Request(
        string eventId = null,
        string processStartTime = null,
        string processEndTime = null,
        string[] columns = null,
        int? tenantId = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        SecurityEventDetailRequest requestBody = SecurityEventDetailRequest.Create(
            eventId,
            processStartTime,
            processEndTime,
            columns,
            tenantId);
        Request(requestBody, additionalHeaders);
    }

    /// <summary>POST 查询事件溯源详情（完整请求体）。</summary>
    public static void Request(
        SecurityEventDetailRequest requestBody,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (requestBody == null)
        {
            HttpRequestResult failure = HttpRequestResult.Failure("请求体为空。");
            LogResponseJson(failure);
            RaiseRequestCompleted(failure, null);
            return;
        }

        if (requestBody.columns == null)
        {
            requestBody.columns = Array.Empty<string>();
        }

        string url = BuildRequestUrl();
        HttpService.Instance.PostJson<SecurityEventDetailRequest, SecurityEventDetailResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson(result);
                RaiseRequestCompleted(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>解析 JSON 为事件溯源详情响应（不发起 HTTP、不刷新面板/POI）。</summary>
    public static bool TryParseResponse(string json, out SecurityEventDetailResponse response, out string errorMessage)
    {
        response = null;
        errorMessage = null;
        if (!HttpJsonParser.TryParse(json, out response, out string parseError))
        {
            errorMessage = parseError;
            return false;
        }

        if (response == null)
        {
            errorMessage = "响应为空。";
            return false;
        }

        if (!response.IsSuccess)
        {
            errorMessage = $"业务 code={response.code}，msg={response.msg}";
            return false;
        }

        if (response.data != null)
        {
            ApplyRecordData(response.data);
        }

        return true;
    }

    /// <summary>解析本地/宿主 JSON 并执行成功后的缓存、GJ_Panel、POI（不发起 HTTP）。</summary>
    public static bool TryApplySuccessfulResponseFromJson(string json, out string errorMessage)
    {
        if (!TryParseResponse(json, out SecurityEventDetailResponse response, out errorMessage))
        {
            return false;
        }

        return ApplySuccessfulResponse(response, showPanel: true);
    }

    /// <summary>
    /// 接口成功后：解析 record_data、缓存、刷新 GJ_Panel、按经纬度生成 POI。
    /// </summary>
    public static bool ApplySuccessfulResponse(SecurityEventDetailResponse response, bool showPanel = true)
    {
        if (response == null || !response.IsSuccess || response.data == null)
        {
            Debug.LogWarning("[SecurityEventDetailApi] ApplySuccessfulResponse 跳过：响应为空或业务未成功。");
            return false;
        }

        ApplyRecordData(response.data);
        LastResponse = response;

        GJPanel panel = GJPanel.Instance;
        if (panel != null)
        {
            panel.ApplyDetailData(response.data, showPanel);
            Debug.Log(
                $"[SecurityEventDetailApi] 已缓存并刷新 GJ_Panel：{response.data.event_name} / {response.data.vin}");
        }
        else
        {
            Debug.LogWarning("[SecurityEventDetailApi] 未找到 GJPanel，已缓存数据但未刷新面板。");
        }

        TrySpawnEventPoi(response.data);
        return true;
    }

    /// <summary>解析并附加 data.record_data / originalMap 经纬度。</summary>
    public static bool ApplyRecordData(SecurityEventDetailData data)
    {
        if (data == null)
        {
            return false;
        }

        bool hasRecord = data.TryApplyRecordData(out string errorMessage);
        if (!hasRecord && !string.IsNullOrWhiteSpace(data.record_data))
        {
            Debug.LogWarning($"[SecurityEventDetailApi] record_data 解析失败：{errorMessage}");
        }

        if (data.TryGetRecordLongitudeLatitude(out double longitude, out double latitude))
        {
            Debug.Log($"[SecurityEventDetailApi] 经纬度：longitude={longitude}, latitude={latitude}");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(data.record_data) || data.originalMap != null)
        {
            Debug.LogWarning("[SecurityEventDetailApi] record_data / originalMap 中未包含有效经纬度。");
        }

        return hasRecord;
    }

    private static void TrySpawnEventPoi(SecurityEventDetailData data)
    {
        if (data == null)
        {
            return;
        }

        if (!data.TryGetRecordLongitudeLatitude(out double longitude, out double latitude))
        {
            Debug.LogWarning("[SecurityEventDetailApi] 无有效经纬度，跳过 POI 生成。");
            return;
        }

        string provinceCode = ResolveProvinceCode(data);
        if (string.IsNullOrWhiteSpace(provinceCode) || provinceCode == "0")
        {
            Debug.LogWarning(
                "[SecurityEventDetailApi] originalMap.province 无效，无法生成 POI（需要省级 adcode）。");
            return;
        }

        POI_Manager poiManager = POI_Manager.Instance;
        if (poiManager == null)
        {
            Debug.LogWarning("[SecurityEventDetailApi] 未找到 POI_Manager，跳过 POI 生成。");
            return;
        }

        poiManager.SpawnPoiDelayed(provinceCode, POIType.yellow, longitude, latitude);
        Debug.Log(
            $"[SecurityEventDetailApi] 已请求生成 POI：province={provinceCode}, lon={longitude}, lat={latitude}");
    }

    private static string ResolveProvinceCode(SecurityEventDetailData data)
    {
        string raw = data.originalMap != null ? data.originalMap.province : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(raw, out string normalized) &&
            !string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return raw;
    }

    private static void RaiseRequestCompleted(HttpRequestResult result, SecurityEventDetailResponse response)
    {
        if (result != null && result.IsSuccess && response != null && response.IsSuccess && response.data != null)
        {
            ApplySuccessfulResponse(response, showPanel: true);
        }

        RequestCompleted?.Invoke(result, response);
    }

    private static void LogResponseJson(HttpRequestResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[SecurityEventDetailApi] 请求结果为空。");
            return;
        }

        string body = string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody;
        if (result.IsCancelled)
        {
            Debug.Log("[SecurityEventDetailApi] 请求已取消。");
            return;
        }

        if (result.IsSuccess)
        {
            Debug.Log($"[SecurityEventDetailApi] 请求成功，状态码={result.StatusCode}，响应 JSON：\n{body}");
            return;
        }

        Debug.LogWarning(
            $"[SecurityEventDetailApi] 请求失败，状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{body}");
    }
}
