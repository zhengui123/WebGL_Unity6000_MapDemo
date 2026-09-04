using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 高危安全事件接口（综合态势 highRiskSecurityEvent）。
/// 国内：单次全国 POST（firstClassCode/secondClassCode 均为空），入库后按 data[].province 分省存储。
/// </summary>
public static class HighRiskSecurityEventApi
{
    private sealed class RegionBatchState
    {
        public ThreatQueryScope Scope;
        public string StartTime;
        public string EndTime;
        public IReadOnlyList<ThreatRegionRequestCodes> Regions;
        public Dictionary<string, string> AdditionalHeaders;
        public Action<HttpRequestResult, HighRiskSecurityEventBatchResult> OnCompleted;
        public int NextIndex;
        public int SuccessRegionCount;
        public int FailedRegionCount;
    }

    private static RegionBatchState _activeBatch;
    private static bool _isDomesticRequestInProgress;

    /// <summary>单次区域请求完成（成功或失败均触发）。</summary>
    public static event Action<HttpRequestResult, HighRiskSecurityEventResponse, ThreatRegionRequestCodes> RequestCompleted;

    /// <summary>国内全国请求或国外分批全部结束。</summary>
    public static event Action<HighRiskSecurityEventBatchResult> BatchCompleted;

    public static bool IsBatchRequesting => _activeBatch != null || _isDomesticRequestInProgress;

    public static string BuildRequestUrl()
    {
        return HttpProjectConfig.BuildApiUrl(HttpProjectConfig.HighRiskSecurityEventPath);
    }

    /// <summary>国内请求为单次全国查询，固定返回 1。</summary>
    public static int GetDomesticProvinceCount() => 1;

    /// <summary>
    /// 请求国内全国数据（单次 POST）；成功后按 <see cref="HighRiskSecurityEventItem.province"/> 分省入库。
    /// </summary>
    public static void RequestAllDomesticProvinces(
        string startTime = null,
        string endTime = null,
        Action<HttpRequestResult, HighRiskSecurityEventBatchResult> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (_isDomesticRequestInProgress || _activeBatch != null)
        {
            Debug.LogWarning("[HighRiskSecurityEventApi] 已有请求进行中，忽略新的全国请求。");
            return;
        }

        string resolvedStartTime = ThreatQueryDefaults.ResolveStartTime(startTime);
        string resolvedEndTime = ThreatQueryDefaults.ResolveEndTime(endTime);
        ThreatRegionRequestCodes regionCodes = ThreatRegionCodeSettings.GetDomesticNationalRequestRegion();
        HighRiskSecurityEventRequest requestBody = HighRiskSecurityEventRequest.CreateForRegion(
            regionCodes,
            resolvedStartTime,
            resolvedEndTime);

        _isDomesticRequestInProgress = true;
        Debug.Log(
            $"[HighRiskSecurityEventApi] 开始全国请求，" +
            $"firstClassCode={regionCodes.FirstClassCode}，secondClassCode={regionCodes.SecondClassCode}，" +
            $"startTime={resolvedStartTime}，endTime={resolvedEndTime}");

        PostRequest(
            requestBody,
            regionCodes,
            (result, response) =>
            {
                _isDomesticRequestInProgress = false;

                int successCount = 0;
                int failedCount = 0;
                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    successCount = 1;
                    HighRiskSecurityEventDataStore.Instance.ReplaceFromResponse(response);
                    LogSuccessfulDataStoredIfNeeded(regionCodes, result, response, "全国入库");
                }
                else
                {
                    failedCount = 1;
                    string body = result != null && !string.IsNullOrWhiteSpace(result.RawBody)
                        ? result.RawBody
                        : BuildResponseJsonText(result, response);
                    Debug.LogWarning(
                        $"[HighRiskSecurityEventApi] 全国请求失败详情：" +
                        $"IsSuccess={result?.IsSuccess}，StatusCode={result?.StatusCode}，Error={result?.Error}\n" +
                        $"业务 code={response?.code}，msg={response?.msg}\n结果内容：\n{body}");
                }

                RequestCompleted?.Invoke(result, response, regionCodes);

                HighRiskSecurityEventBatchResult batchResult = new HighRiskSecurityEventBatchResult
                {
                    TotalRegionCount = 1,
                    SuccessRegionCount = successCount,
                    FailedRegionCount = failedCount,
                    TotalEventCount = HighRiskSecurityEventDataStore.Instance.Count,
                };

                ThreatProvinceAlertController.EvaluateAfterDataUpdated();

                Debug.Log(
                    $"[HighRiskSecurityEventApi] 全国请求完成，成功={batchResult.SuccessRegionCount}，" +
                    $"失败={batchResult.FailedRegionCount}，总事件数={batchResult.TotalEventCount}");

                BatchCompleted?.Invoke(batchResult);
                onCompleted?.Invoke(result, batchResult);
            },
            additionalHeaders);
    }

    /// <summary>
    /// 按查询范围请求：国内=全国单次；国外=预留分批。
    /// </summary>
    public static void Request(
        ThreatQueryScope scope = ThreatQueryScope.Domestic,
        string startTime = null,
        string endTime = null,
        Action<HttpRequestResult, HighRiskSecurityEventBatchResult> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        if (scope == ThreatQueryScope.Domestic)
        {
            RequestAllDomesticProvinces(startTime, endTime, onCompleted, additionalHeaders);
            return;
        }

        IReadOnlyList<ThreatRegionRequestCodes> regions = ThreatRegionCodeSettings.GetRequestRegions(scope);
        BeginRegionBatch(
            scope,
            ThreatQueryDefaults.ResolveStartTime(startTime),
            ThreatQueryDefaults.ResolveEndTime(endTime),
            regions,
            onCompleted,
            additionalHeaders);
    }

    private static void BeginRegionBatch(
        ThreatQueryScope scope,
        string startTime,
        string endTime,
        IReadOnlyList<ThreatRegionRequestCodes> regions,
        Action<HttpRequestResult, HighRiskSecurityEventBatchResult> onCompleted,
        Dictionary<string, string> additionalHeaders)
    {
        if (_activeBatch != null || _isDomesticRequestInProgress)
        {
            Debug.LogWarning("[HighRiskSecurityEventApi] 已有请求进行中，忽略新的分批请求。");
            return;
        }

        if (regions == null || regions.Count == 0)
        {
            Debug.LogWarning($"[HighRiskSecurityEventApi] 未找到可请求的区域编码，scope={scope}。");
            onCompleted?.Invoke(null, new HighRiskSecurityEventBatchResult());
            return;
        }

        _activeBatch = new RegionBatchState
        {
            Scope = scope,
            StartTime = startTime,
            EndTime = endTime,
            Regions = regions,
            AdditionalHeaders = additionalHeaders,
            OnCompleted = onCompleted,
        };

        HighRiskSecurityEventDataStore.Instance.BeginBatch();
        Debug.Log(
            $"[HighRiskSecurityEventApi] 开始分批请求，scope={scope}，区域数={regions.Count}，" +
            $"startTime={startTime}，endTime={endTime}");
        RequestNextRegionInBatch();
    }

    /// <summary>POST 查询单个区域（不参与分批汇总时使用）。</summary>
    public static void RequestSingleRegion(
        ThreatRegionRequestCodes regionCodes,
        string startTime = null,
        string endTime = null,
        Action<HttpRequestResult, HighRiskSecurityEventResponse> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null,
        bool evaluateAlerts = true)
    {
        HighRiskSecurityEventRequest requestBody = HighRiskSecurityEventRequest.CreateForRegion(
            regionCodes,
            startTime,
            endTime);
        PostRequest(
            requestBody,
            regionCodes,
            (result, response) =>
            {
                if (evaluateAlerts)
                {
                    HandleSingleRegionSuccess(result, response, regionCodes);
                }
                else
                {
                    HandleBatchRegionSuccess(result, response, regionCodes);
                }

                onCompleted?.Invoke(result, response);
            },
            additionalHeaders);
    }

    /// <summary>使用 <see cref="ThreatRegionCodeSettings.ActiveScope"/> 与默认时间请求。</summary>
    public static void RequestWithActiveScope(
        Action<HttpRequestResult, HighRiskSecurityEventBatchResult> onCompleted = null,
        Dictionary<string, string> additionalHeaders = null)
    {
        Request(
            ThreatRegionCodeSettings.ActiveScope,
            null,
            null,
            onCompleted,
            additionalHeaders);
    }

    public static bool TryParseAndStoreResponse(string json, out HighRiskSecurityEventResponse response, out string errorMessage)
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
            LogParsedResponse(json, response, "解析入库失败");
            return false;
        }

        HighRiskSecurityEventDataStore.Instance.ReplaceFromResponse(response);
        ThreatProvinceAlertController.EvaluateAfterDataUpdated();
        LogParsedResponse(json, response, "解析入库成功");
        return true;
    }

    private static void RequestNextRegionInBatch()
    {
        RegionBatchState batch = _activeBatch;
        if (batch == null)
        {
            return;
        }

        if (batch.NextIndex >= batch.Regions.Count)
        {
            CompleteBatch();
            return;
        }

        ThreatRegionRequestCodes regionCodes = batch.Regions[batch.NextIndex];
        batch.NextIndex++;

        HighRiskSecurityEventRequest requestBody = HighRiskSecurityEventRequest.CreateForRegion(
            regionCodes,
            batch.StartTime,
            batch.EndTime);

        Debug.Log(
            $"[HighRiskSecurityEventApi] 分批请求进度 {batch.NextIndex}/{batch.Regions.Count}，" +
            $"firstClassCode={regionCodes.FirstClassCode}，secondClassCode={regionCodes.SecondClassCode}");

        PostRequest(
            requestBody,
            regionCodes,
            (result, response) =>
            {
                HandleBatchRegionSuccess(result, response, regionCodes);
                RequestCompleted?.Invoke(result, response, regionCodes);
                RequestNextRegionInBatch();
            },
            batch.AdditionalHeaders);
    }

    private static void CompleteBatch()
    {
        RegionBatchState batch = _activeBatch;
        if (batch == null)
        {
            return;
        }

        _activeBatch = null;

        HighRiskSecurityEventBatchResult batchResult = new HighRiskSecurityEventBatchResult
        {
            TotalRegionCount = batch.Regions.Count,
            SuccessRegionCount = batch.SuccessRegionCount,
            FailedRegionCount = batch.FailedRegionCount,
            TotalEventCount = HighRiskSecurityEventDataStore.Instance.Count,
        };

        ThreatProvinceAlertController.EvaluateAfterDataUpdated();

        Debug.Log(
            $"[HighRiskSecurityEventApi] 分批请求完成，成功={batchResult.SuccessRegionCount}，" +
            $"失败={batchResult.FailedRegionCount}，总事件数={batchResult.TotalEventCount}");

        BatchCompleted?.Invoke(batchResult);
        batch.OnCompleted?.Invoke(null, batchResult);
    }

    private static void HandleBatchRegionSuccess(
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        ThreatRegionRequestCodes regionCodes)
    {
        RegionBatchState batch = _activeBatch;
        if (batch == null)
        {
            return;
        }

        if (result != null && result.IsSuccess && response != null && response.IsSuccess)
        {
            batch.SuccessRegionCount++;
            StoreRegionResponse(response, regionCodes);
            LogSuccessfulDataStoredIfNeeded(regionCodes, result, response, "分批入库");
            return;
        }

        batch.FailedRegionCount++;
    }

    private static void HandleSingleRegionSuccess(
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        ThreatRegionRequestCodes regionCodes)
    {
        if (result != null && result.IsSuccess && response != null && response.IsSuccess)
        {
            StoreRegionResponse(response, regionCodes);
            LogSuccessfulDataStoredIfNeeded(regionCodes, result, response, "单省入库");
            ThreatProvinceAlertController.EvaluateAfterDataUpdated();
        }
    }

    /// <summary>全国响应全量替换；带 secondClassCode 时仅合并该省。</summary>
    private static void StoreRegionResponse(
        HighRiskSecurityEventResponse response,
        ThreatRegionRequestCodes regionCodes)
    {
        if (!string.IsNullOrWhiteSpace(regionCodes.SecondClassCode))
        {
            HighRiskSecurityEventDataStore.Instance.MergeProvinceResponse(
                regionCodes.SecondClassCode,
                response);
            return;
        }

        HighRiskSecurityEventDataStore.Instance.ReplaceFromResponse(response);
    }

    private static string FormatRegionCodesForLog(ThreatRegionRequestCodes regionCodes)
    {
        return $"country={regionCodes.FirstClassCode}，province={regionCodes.SecondClassCode}";
    }

    private static void PostRequest(
        HighRiskSecurityEventRequest requestBody,
        ThreatRegionRequestCodes regionCodes,
        Action<HttpRequestResult, HighRiskSecurityEventResponse> onCompleted,
        Dictionary<string, string> additionalHeaders)
    {
        if (requestBody == null)
        {
            HttpRequestResult failure = HttpRequestResult.Failure("请求体为空。");
            LogResponseJson(failure, null, requestBody, regionCodes);
            onCompleted?.Invoke(failure, null);
            return;
        }

        string url = BuildRequestUrl();
        Debug.Log($"[HighRiskSecurityEventApi] 请求 URL：{url}");
        HttpService.Instance.PostJson<HighRiskSecurityEventRequest, HighRiskSecurityEventResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                LogResponseJson(result, response, requestBody, regionCodes);
                onCompleted?.Invoke(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }

    private static void LogResponseJson(
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        HighRiskSecurityEventRequest requestBody,
        ThreatRegionRequestCodes regionCodes)
    {
        if (result == null)
        {
            Debug.LogWarning("[HighRiskSecurityEventApi] 请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            Debug.Log("[HighRiskSecurityEventApi] 请求已取消。");
            return;
        }

        if (!result.IsSuccess)
        {
            Debug.LogWarning(
                $"[HighRiskSecurityEventApi] 请求失败，{FormatRegionCodesForLog(regionCodes)}，" +
                $"状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{BuildResponseJsonText(result, response)}");
            return;
        }

        LogSuccessfulJsonReceived(result, response, requestBody, regionCodes);
    }

    /// <summary>HTTP 成功且业务成功时输出 JSON；RawBody 为空时回退为序列化响应对象。</summary>
    private static void LogSuccessfulJsonReceived(
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        HighRiskSecurityEventRequest requestBody,
        ThreatRegionRequestCodes regionCodes)
    {
        int count = response?.data != null ? response.data.Length : 0;
        bool bizOk = response != null && response.IsSuccess;
        string json = BuildResponseJsonText(result, response);
        string requestHint = requestBody != null
            ? $"startTime={requestBody.startTime}，endTime={requestBody.endTime}，"
            : string.Empty;

        if (bizOk)
        {
            Debug.Log(
                $"[HighRiskSecurityEventApi] 成功接收 JSON，{FormatRegionCodesForLog(regionCodes)}，" +
                $"{requestHint}事件数={count}\n响应 JSON：\n{json}");
            return;
        }

        string bizMessage = response != null ? $"code={response.code}，msg={response.msg}" : "响应对象为空";
        Debug.LogWarning(
            $"[HighRiskSecurityEventApi] HTTP 成功但业务失败，{FormatRegionCodesForLog(regionCodes)}，" +
            $"{requestHint}{bizMessage}\n响应 JSON：\n{json}");
    }

    private static void LogSuccessfulDataStoredIfNeeded(
        ThreatRegionRequestCodes regionCodes,
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        string action)
    {
        if (response == null || !response.IsSuccess)
        {
            return;
        }

        if (result != null && !string.IsNullOrWhiteSpace(result.RawBody))
        {
            return;
        }

        int count = response.data != null ? response.data.Length : 0;
        string json = BuildResponseJsonText(result, response);
        Debug.Log(
            $"[HighRiskSecurityEventApi] {action}（补打日志）：{FormatRegionCodesForLog(regionCodes)}，" +
            $"事件数={count}\n响应 JSON：\n{json}");
    }

    private static void LogParsedResponse(string json, HighRiskSecurityEventResponse response, string action)
    {
        int count = response?.data != null ? response.data.Length : 0;
        bool bizOk = response != null && response.IsSuccess;
        string body = string.IsNullOrWhiteSpace(json) ? BuildResponseJsonText(null, response) : json;

        if (bizOk)
        {
            Debug.Log($"[HighRiskSecurityEventApi] {action}，事件数={count}\n响应 JSON：\n{body}");
            return;
        }

        string bizMessage = response != null ? $"code={response.code}，msg={response.msg}" : "响应为空";
        Debug.LogWarning($"[HighRiskSecurityEventApi] {action}，{bizMessage}\n响应 JSON：\n{body}");
    }

    private static string BuildResponseJsonText(HttpRequestResult result, HighRiskSecurityEventResponse response)
    {
        if (result != null && !string.IsNullOrWhiteSpace(result.RawBody))
        {
            return result.RawBody;
        }

        if (response != null)
        {
            return HttpJsonParser.ToJson(response);
        }

        return "(空)";
    }
}
