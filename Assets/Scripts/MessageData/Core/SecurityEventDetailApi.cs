using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 告警事件详情接口（HTTPS 测试环境 getSecurityEventDetail）。
/// </summary>
public static class SecurityEventDetailApi
{
    public const string SatokenHeaderKey = "Satoken";

    /// <summary>告警事件接口专用 Satoken（UseCustomSatoken=true 时生效）。</summary>
    public const string CustomSatoken =
        "9vOD5QtxpKlDbxZfKhRJnOqir2Tg4zxMCErT3MqcsLFagd6JP5V19BpWdR9VneTl";

    /// <summary>
    /// true：使用 <see cref="CustomSatoken"/>；
    /// false：使用 HttpBackendConfig 中的通用 Satoken。
    /// </summary>
    public static bool UseCustomSatoken = true;

    /// <summary>请求完成（成功或失败均触发）。</summary>
    public static event Action<HttpRequestResult, SecurityEventDetailResponse> RequestCompleted;

    /// <summary>构建告警事件详情接口完整 URL。</summary>
    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildHttpsTestApiUrl(HttpProjectConfig.SecurityEventDetailPath);
    }

    /// <summary>
    /// POST 查询告警事件详情；eventId / 时间参数为空时使用 <see cref="SecurityEventDetailRequest"/> 默认值。
    /// </summary>
    public static void Request(
        string eventId = null,
        string processStartTime = null,
        string processEndTime = null,
        bool passwd = false,
        Dictionary<string, string> additionalHeaders = null)
    {
        SecurityEventDetailRequest requestBody = SecurityEventDetailRequest.Create(
            eventId,
            processStartTime,
            processEndTime,
            passwd);
        Request(requestBody, additionalHeaders);
    }

    /// <summary>POST 查询告警事件详情（完整请求体）。</summary>
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

        string url = BuildRequestUrl();
        HttpService.Instance.PostJson<SecurityEventDetailRequest, SecurityEventDetailResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson(result);
                RaiseRequestCompleted(result, response);
            },
            BuildRequestHeaders(additionalHeaders));
    }

    /// <summary>解析 JSON 为告警详情响应（不发起 HTTP 请求）。</summary>
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

    /// <summary>解析并附加 data.record_data 结构化数据。</summary>
    public static bool ApplyRecordData(SecurityEventDetailData data)
    {
        if (data == null)
        {
            return false;
        }

        if (!data.TryApplyRecordData(out string errorMessage))
        {
            Debug.LogWarning($"[SecurityEventDetailApi] record_data 解析失败：{errorMessage}");
            return false;
        }

        if (data.TryGetRecordLongitudeLatitude(out double longitude, out double latitude))
        {
            Debug.Log($"[SecurityEventDetailApi] record_data 经纬度：longitude={longitude}, latitude={latitude}");
        }
        else
        {
            Debug.LogWarning("[SecurityEventDetailApi] record_data 中未包含有效经纬度。");
        }

        return true;
    }

    private static void RaiseRequestCompleted(HttpRequestResult result, SecurityEventDetailResponse response)
    {
        if (result != null && result.IsSuccess && response != null && response.IsSuccess && response.data != null)
        {
            ApplyRecordData(response.data);
        }

        RequestCompleted?.Invoke(result, response);
    }

    private static Dictionary<string, string> BuildRequestHeaders(Dictionary<string, string> additionalHeaders)
    {
        Dictionary<string, string> headers = HttpProjectConfig.MergeDefaultHeaders(additionalHeaders);
        if (UseCustomSatoken && !string.IsNullOrEmpty(CustomSatoken))
        {
            headers[SatokenHeaderKey] = CustomSatoken;
        }

        return headers;
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
