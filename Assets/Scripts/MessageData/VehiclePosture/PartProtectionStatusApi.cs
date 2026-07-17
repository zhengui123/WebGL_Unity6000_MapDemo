using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆态势：目标车辆各零部件防护状态接口（partProtectionStatus）。
/// </summary>
public static class PartProtectionStatusApi
{
    /// <summary>请求完成（成功或失败均触发）。</summary>
    public static event Action<HttpRequestResult, PartProtectionStatusResponse> RequestCompleted;

    /// <summary>构建接口完整 URL。</summary>
    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildApiUrl(HttpProjectConfig.PartProtectionStatusPath);
    }

    /// <summary>
    /// POST 查询零部件防护状态；参数为空时使用 <see cref="PartProtectionStatusRequest.CreateDefaultTest"/>。
    /// </summary>
    public static void Request(
        string encryptVin = null,
        string startTime = null,
        string endTime = null,
        Action<HttpRequestResult, PartProtectionStatusResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        PartProtectionStatusRequest requestBody = string.IsNullOrWhiteSpace(encryptVin)
            ? PartProtectionStatusRequest.CreateDefaultTest()
            : PartProtectionStatusRequest.Create(encryptVin, startTime, endTime);
        Request(requestBody, onCompleted, additionalHeaders);
    }

    /// <summary>POST 查询零部件防护状态（完整请求体）。</summary>
    public static void Request(
        PartProtectionStatusRequest requestBody,
        Action<HttpRequestResult, PartProtectionStatusResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (requestBody == null)
        {
            HttpRequestResult failure = HttpRequestResult.Failure("请求体为空。");
            LogResponseJson(failure);
            RaiseRequestCompleted(failure, null, onCompleted);
            return;
        }

        string url = BuildRequestUrl();
        Debug.Log(
            $"[PartProtectionStatusApi] POST {url} | encryptVin={requestBody.encryptVin} | " +
            $"startTime={requestBody.startTime} | endTime={requestBody.endTime}");

        HttpService.Instance.PostJson<PartProtectionStatusRequest, PartProtectionStatusResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson(result);
                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    LogDataSummary(response);
                }

                RaiseRequestCompleted(result, response, onCompleted);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>解析 JSON 为响应（不发起 HTTP 请求）。</summary>
    public static bool TryParseResponse(
        string json,
        out PartProtectionStatusResponse response,
        out string errorMessage)
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

    private static void RaiseRequestCompleted(
        HttpRequestResult result,
        PartProtectionStatusResponse response,
        Action<HttpRequestResult, PartProtectionStatusResponse> onCompleted)
    {
        RequestCompleted?.Invoke(result, response);
        onCompleted?.Invoke(result, response);
    }

    private static void LogDataSummary(PartProtectionStatusResponse response)
    {
        if (response?.data == null)
        {
            Debug.Log("[PartProtectionStatusApi] 业务成功，data 为空。");
            return;
        }

        Debug.Log(
            $"[PartProtectionStatusApi] 业务成功 | unprotected={response.data.UnprotectedCount} | " +
            $"protected={response.data.ProtectedCount}");
    }

    private static void LogResponseJson(HttpRequestResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[PartProtectionStatusApi] 请求结果为空。");
            return;
        }

        string body = string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody;
        if (result.IsCancelled)
        {
            Debug.Log("[PartProtectionStatusApi] 请求已取消。");
            return;
        }

        if (result.IsSuccess)
        {
            Debug.Log($"[PartProtectionStatusApi] 请求成功，状态码={result.StatusCode}，响应 JSON：\n{body}");
            return;
        }

        Debug.LogWarning(
            $"[PartProtectionStatusApi] 请求失败，状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{body}");
    }
}
