using System;
using System.Collections.Generic;

/// <summary>
/// 高危安全事件消费标记上传接口（highRiskEventConsumedMark）。
/// 每省威胁下钻播放结束后调用：上报该省当前缓存中的全部 eventId。
/// </summary>
public static class HighRiskEventConsumedMarkApi
{
    /// <summary>请求完成（成功或失败均触发）。</summary>
    public static event Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> RequestCompleted;

    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildApiUrl(HttpProjectConfig.HighRiskEventConsumedMarkPath);
    }

    /// <summary>
    /// 上传指定省当前缓存中的全部 eventId（取最新数据）；无有效 id 时跳过。
    /// endTime 固定为调用时当前时间。须在移除/排除该省缓存之前调用。
    /// </summary>
    public static void RequestFromProvinceCache(
        string provinceCode,
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        string[] eventIds = CollectEventIdsByProvince(provinceCode);
        if (eventIds == null || eventIds.Length == 0)
        {
            LogManager.LogBackend(
                $"[HighRiskEventConsumedMarkApi] 省 {provinceCode} 无有效 eventId，跳过上传。");
            return;
        }

        LogManager.LogBackend(
            $"[HighRiskEventConsumedMarkApi] 省播放结束上传 | province={provinceCode} | eventIds={eventIds.Length}");
        Request(eventIds, endTime: null, onCompleted, additionalHeaders);
    }

    /// <summary>
    /// 从威胁缓存收集达标省事件并上传；无达标省或无有效 eventId 时跳过。
    /// endTime 固定为调用时当前时间。
    /// </summary>
    public static void RequestFromCacheMeetingThreshold(
        int threshold = ThreatAlertSettings.EventsPerProvinceThreshold,
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        string[] eventIds = CollectQualifiedEventIds(threshold);
        if (eventIds == null || eventIds.Length == 0)
        {
            LogManager.LogBackend(
                $"[HighRiskEventConsumedMarkApi] 无达标省事件（阈值≥{threshold}），跳过上传。");
            return;
        }

        Request(eventIds, endTime: null, onCompleted, additionalHeaders);
    }

    /// <summary>POST 上传消费标记；endTime 为空则使用当前时间。</summary>
    public static void Request(
        string[] eventIds,
        string endTime = null,
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (eventIds == null || eventIds.Length == 0)
        {
            LogManager.LogBackend("[HighRiskEventConsumedMarkApi] eventIds 为空，跳过上传。");
            return;
        }

        Request(
            HighRiskEventConsumedMarkRequest.Create(eventIds, endTime),
            onCompleted,
            additionalHeaders);
    }

    /// <summary>POST 上传消费标记（完整请求体）。</summary>
    public static void Request(
        HighRiskEventConsumedMarkRequest requestBody,
        Action<HttpRequestResult, HighRiskEventConsumedMarkResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (requestBody == null)
        {
            HttpRequestResult failure = HttpRequestResult.Failure("请求体为空。");
            LogResponseResult(failure, null);
            RaiseRequestCompleted(failure, null, onCompleted);
            return;
        }

        if (requestBody.eventIds == null || requestBody.eventIds.Length == 0)
        {
            LogManager.LogBackend("[HighRiskEventConsumedMarkApi] eventIds 为空，跳过上传。");
            return;
        }

        string url = BuildRequestUrl();
        string requestJson = requestBody.ToCompactJson();
        LogManager.LogBackend(
            $"[HighRiskEventConsumedMarkApi] 调用参数 | URL={url} | " +
            $"eventIds数量={requestBody.eventIds.Length} | endTime={requestBody.endTime}\n" +
            $"请求 JSON：\n{requestJson}");

        HttpService.Instance.PostJson<HighRiskEventConsumedMarkRequest, HighRiskEventConsumedMarkResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseResult(result, response);
                RaiseRequestCompleted(result, response, onCompleted);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>收集指定省当前缓存中的全部非空 eventId（去重保序）。</summary>
    public static string[] CollectEventIdsByProvince(string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return Array.Empty<string>();
        }

        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        if (store == null)
        {
            return Array.Empty<string>();
        }

        IReadOnlyList<HighRiskSecurityEventItem> events = store.GetEventsByProvince(provinceCode);
        if (events == null || events.Count == 0)
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>(events.Count);
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < events.Count; i++)
        {
            HighRiskSecurityEventItem item = events[i];
            if (item == null || string.IsNullOrWhiteSpace(item.eventId))
            {
                continue;
            }

            string id = item.eventId.Trim();
            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        return ids.ToArray();
    }

    /// <summary>收集缓存中达标省的全部非空 eventId（去重保序）。</summary>
    public static string[] CollectQualifiedEventIds(int threshold)
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        if (store == null)
        {
            return Array.Empty<string>();
        }

        IReadOnlyList<string> provinces = store.GetProvincesMeetingThreshold(threshold);
        if (provinces == null || provinces.Count == 0)
        {
            return Array.Empty<string>();
        }

        List<string> ids = new List<string>(64);
        HashSet<string> seen = new HashSet<string>();

        for (int i = 0; i < provinces.Count; i++)
        {
            IReadOnlyList<HighRiskSecurityEventItem> events = store.GetEventsByProvince(provinces[i]);
            if (events == null || events.Count == 0)
            {
                continue;
            }

            for (int j = 0; j < events.Count; j++)
            {
                HighRiskSecurityEventItem item = events[j];
                if (item == null || string.IsNullOrWhiteSpace(item.eventId))
                {
                    continue;
                }

                string id = item.eventId.Trim();
                if (seen.Add(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids.ToArray();
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
            LogManager.LogBackendWarning("[HighRiskEventConsumedMarkApi] 请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            LogManager.LogBackend("[HighRiskEventConsumedMarkApi] 请求已取消。");
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
                $"[HighRiskEventConsumedMarkApi] 响应结果 | HTTP失败，状态码={result.StatusCode}，错误={result.Error} | {bizHint}\n" +
                $"响应 JSON：\n{body}");
            return;
        }

        if (response != null && response.IsSuccess)
        {
            LogManager.LogBackend(
                $"[HighRiskEventConsumedMarkApi] 响应结果 | HTTP成功，状态码={result.StatusCode} | {bizHint}\n" +
                $"响应 JSON：\n{body}");
            return;
        }

        LogManager.LogBackendWarning(
            $"[HighRiskEventConsumedMarkApi] 响应结果 | HTTP成功但业务失败，状态码={result.StatusCode} | {bizHint}\n" +
            $"响应 JSON：\n{body}");
    }
}
