using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 告警事件分页接口（HTTPS 测试环境 getBasicEventPage）。
/// </summary>
public static class BasicEventPageApi
{
    public const string SatokenHeaderKey = "Satoken";

    /// <summary>告警事件接口专用 Satoken（UseCustomSatoken=true 时生效）。</summary>
    public const string CustomSatoken =
        "9vOD5QtxpKlDbxZfKhRJnOqir2Tg4zxMCErT3MqcsLFagd6JP5V19BpWdR9VneTl";

    /// <summary>
    /// true：POST/GET 使用 <see cref="CustomSatoken"/>；
    /// false：使用 HttpBackendConfig 中的通用 Satoken。
    /// </summary>
    public static bool UseCustomSatoken = true;

    /// <summary>构建告警事件接口完整 URL。</summary>
    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildHttpsTestApiUrl(HttpProjectConfig.EventQueryPath);
    }

    /// <summary>
    /// POST 分页查询告警事件。
    /// </summary>
    public static void Request(
        BasicEventPageRequest requestBody,
        Action<HttpRequestResult, BasicEventPageResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (requestBody == null)
        {
            onCompleted?.Invoke(HttpRequestResult.Failure("请求体为空。"), null);
            return;
        }

        string url = BuildRequestUrl();
        HttpService.Instance.PostJson<BasicEventPageRequest, BasicEventPageResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson("POST", result);
                onCompleted?.Invoke(result, response);
            },
            BuildRequestHeaders(additionalHeaders));
    }

    /// <summary>POST 分页查询告警事件（参数形式）。</summary>
    public static void Request(
        int pageNo,
        int pageSize,
        string startTime,
        string endTime,
        Action<HttpRequestResult, BasicEventPageResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(
            BasicEventPageRequest.Create(pageNo, pageSize, startTime, endTime),
            onCompleted,
            additionalHeaders);
    }

    /// <summary>
    /// 请求告警列表并回调首条事件；无数据时 item 为 null。
    /// </summary>
    public static void RequestFirstEvent(
        int pageNo,
        int pageSize,
        string startTime,
        string endTime,
        Action<HttpRequestResult, BasicEventItem> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(pageNo, pageSize, startTime, endTime, (result, response) =>
        {
            if (onCompleted == null)
            {
                return;
            }

            if (result == null || !result.IsSuccess || response == null || !response.IsSuccess)
            {
                onCompleted(result, null);
                return;
            }

            onCompleted(result, response.GetFirstEvent());
        }, additionalHeaders);
    }

    /// <summary>请求第一页并返回首条告警事件（默认 pageSize=10）。</summary>
    public static void RequestFirstEvent(
        Action<HttpRequestResult, BasicEventItem> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        RequestFirstEvent(1, 10, null, null, onCompleted, additionalHeaders);
    }

    /// <summary>GET 查询告警事件（用于联调或后端支持 GET 的场景）。</summary>
    public static void RequestGet(
        Action<HttpRequestResult, BasicEventPageResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        string url = BuildRequestUrl();
        HttpService.Instance.Get<BasicEventPageResponse>(
            url,
            (result, response) =>
            {
                LogResponseJson("GET", result);
                onCompleted?.Invoke(result, response);
            },
            BuildRequestHeaders(additionalHeaders));
    }

    /// <summary>解析 JSON 为告警事件响应（不发起 HTTP 请求）。</summary>
    public static bool TryParseResponse(string json, out BasicEventPageResponse response, out string errorMessage)
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

        return true;
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

    private static void LogResponseJson(string method, HttpRequestResult result)
    {
        if (result == null)
        {
            Debug.LogWarning($"[BasicEventPageApi] {method} 结果为空。");
            return;
        }

        string body = string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody;
        if (result.IsCancelled)
        {
            Debug.Log($"[BasicEventPageApi] {method} 已取消。");
            return;
        }

        if (result.IsSuccess)
        {
            Debug.Log($"[BasicEventPageApi] {method} 成功，状态码={result.StatusCode}，响应 JSON：\n{body}");
            return;
        }

        Debug.LogWarning(
            $"[BasicEventPageApi] {method} 失败，状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{body}");
    }
}
