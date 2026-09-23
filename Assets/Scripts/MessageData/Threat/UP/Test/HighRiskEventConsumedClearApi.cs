using System;
using System.Collections.Generic;

/// <summary>
/// 高危事件消费标记清除测试接口（highRiskEventConsumedClear）。
/// 删除当前租户已提交的消费标记；无请求参数。
/// </summary>
public static class HighRiskEventConsumedClearApi
{
    /// <summary>请求完成（成功或失败均触发）。</summary>
    public static event Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> RequestCompleted;

    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildApiUrl(HttpProjectConfig.HighRiskEventConsumedClearPath);
    }

    /// <summary>POST 清除当前租户消费标记；无业务参数。</summary>
    public static void Request(
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        string url = BuildRequestUrl();
        HighRiskEventConsumedClearRequest requestBody = new HighRiskEventConsumedClearRequest();
        LogManager.LogBackend(
            $"[HighRiskEventConsumedClearApi] 调用参数 | URL={url} | 无业务参数\n请求 JSON：\n{{}}");

        HttpService.Instance.PostJson<HighRiskEventConsumedClearRequest, HighRiskEventConsumedMarkResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseResult(result, response);
                RaiseRequestCompleted(result, response, onCompleted);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    private static void RaiseRequestCompleted(
        HttpRequestResult result,
        HighRiskEventConsumedMarkResponse response,
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted)
    {
        RequestCompleted?.Invoke(result, response);
        onCompleted?.Invoke(result, response);
    }

    private static void LogResponseResult(
        HttpRequestResult result,
        HighRiskEventConsumedMarkResponse response)
    {
        if (result == null)
        {
            LogManager.LogBackendWarning("[HighRiskEventConsumedClearApi] 请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            LogManager.LogBackend("[HighRiskEventConsumedClearApi] 请求已取消。");
            return;
        }

        string body = !string.IsNullOrEmpty(result.RawBody)
            ? result.RawBody
            : (response != null ? HttpJsonParser.ToJson(response) : "(空)");

        string bizHint = response != null
            ? $"code={response.code}，msg={response.msg}，data={response.data}"
            : "响应对象为空";

        if (!result.IsSuccess)
        {
            LogManager.LogBackendWarning(
                $"[HighRiskEventConsumedClearApi] 响应结果 | HTTP失败，状态码={result.StatusCode}，错误={result.Error} | {bizHint}\n" +
                $"响应 JSON：\n{body}");
            return;
        }

        if (response != null && response.IsSuccess)
        {
            LogManager.LogBackend(
                $"[HighRiskEventConsumedClearApi] 响应结果 | HTTP成功，状态码={result.StatusCode} | {bizHint}\n" +
                $"响应 JSON：\n{body}");
            return;
        }

        LogManager.LogBackendWarning(
            $"[HighRiskEventConsumedClearApi] 响应结果 | HTTP成功但业务失败，状态码={result.StatusCode} | {bizHint}\n" +
            $"响应 JSON：\n{body}");
    }
}
