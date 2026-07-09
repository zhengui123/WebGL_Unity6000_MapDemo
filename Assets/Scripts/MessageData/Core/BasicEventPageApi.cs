using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 告警事件分页接口（HTTPS 测试环境 getBasicEventPage）。
/// </summary>
public static class BasicEventPageApi
{
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
            onCompleted,
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    /// <summary>POST 分页查询告警事件（参数形式）。</summary>
    public static void Request(
        int pageNum,
        int pageSize,
        string startTime,
        string endTime,
        Action<HttpRequestResult, BasicEventPageResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(
            BasicEventPageRequest.Create(pageNum, pageSize, startTime, endTime),
            onCompleted,
            additionalHeaders);
    }

    /// <summary>GET 查询告警事件（用于联调或后端支持 GET 的场景）。</summary>
    public static void RequestGet(
        Action<HttpRequestResult, BasicEventPageResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        string url = BuildRequestUrl();
        HttpService.Instance.Get<BasicEventPageResponse>(
            url,
            onCompleted,
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
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
}
