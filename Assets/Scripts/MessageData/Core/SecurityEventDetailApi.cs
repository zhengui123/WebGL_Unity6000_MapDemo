using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件溯源详情接口（getSourceEventDetail）：与其它业务接口共用主机与默认请求头。
/// 成功解析后缓存数据、刷新 GJ_Panel，并记录溯源会话；黄点仅在国家/对应省级显示。
/// </summary>
public static class SecurityEventDetailApi
{
    /// <summary>请求完成（成功或失败均触发；成功时已先执行 <see cref="ApplySuccessfulResponse"/>）。</summary>
    public static event Action<HttpRequestResult, SecurityEventDetailResponse> RequestCompleted;

    /// <summary>最近一次成功应用的完整响应。</summary>
    public static SecurityEventDetailResponse LastResponse { get; private set; }

    /// <summary>最近一次成功应用的 data。</summary>
    public static SecurityEventDetailData LastData => LastResponse != null ? LastResponse.data : null;

    /// <summary>事件溯源会话是否有效（未 CloseGJPanel；与 GJ 面板是否可见无关）。</summary>
    public static bool HasActiveTraceSession { get; private set; }

    /// <summary>会话中的溯源地图单元 code（国内省 adcode / 国外国家 SOC）。</summary>
    public static string TraceSessionProvinceCode { get; private set; }

    private static double _traceSessionLongitude;
    private static double _traceSessionLatitude;

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
            LogManager.LogBackendWarning("[SecurityEventDetailApi] ApplySuccessfulResponse 跳过：响应为空或业务未成功。");
            return false;
        }

        ApplyRecordData(response.data);
        LastResponse = response;

        // Android 仍缓存数据，但不显示 GJ_Panel
        bool shouldShowPanel = showPanel && GJPanel.IsDisplaySupportedOnCurrentPlatform;

        GJPanel panel = GJPanel.Instance;
        if (panel != null)
        {
            panel.ApplyDetailData(response.data, shouldShowPanel);
            LogManager.LogBackend(
                shouldShowPanel
                    ? $"[SecurityEventDetailApi] 已缓存并刷新 GJ_Panel：{response.data.event_name} / {response.data.vin}"
                    : $"[SecurityEventDetailApi] 已缓存事件溯源（当前平台不显示 GJ_Panel）：" +
                      $"{response.data.event_name} / {response.data.vin}");
        }
        else
        {
            LogManager.LogBackendWarning("[SecurityEventDetailApi] 未找到 GJPanel，已缓存数据但未刷新面板。");
        }

        // 开启/刷新溯源会话；国外可按 region 对齐大板后再刷黄点
        BeginTraceSessionFromData(response.data);
        TryAlignWorldRegionFromOriginalMap(response.data);
        RefreshTracePoiForCurrentControlState();
        return true;
    }

    /// <summary>关闭事件溯源会话（由 CloseGJPanel 调用）：清黄点并丢弃省份记录。</summary>
    public static void ClearTraceSession()
    {
        HasActiveTraceSession = false;
        TraceSessionProvinceCode = null;
        _traceSessionLongitude = 0d;
        _traceSessionLatitude = 0d;
        POI_Manager.Instance?.RemoveAllPoiByType(POIType.yellow);
        LogManager.LogBackend("[SecurityEventDetailApi] 已清除事件溯源会话与黄点 POI。");
    }

    /// <summary>
    /// 按操控级别刷新溯源黄点：仅国家/对应省级显示；其它级或其它省清掉黄点但保留会话。
    /// </summary>
    public static void RefreshTracePoiForControlState(
        GameManager.ControlState state,
        string currentProvinceCode = null)
    {
        POI_Manager poiManager = POI_Manager.Instance;
        poiManager?.RemoveAllPoiByType(POIType.yellow);

        if (!HasActiveTraceSession ||
            string.IsNullOrWhiteSpace(TraceSessionProvinceCode) ||
            !TryGetTraceSessionCoordinates(out double longitude, out double latitude))
        {
            return;
        }

        if (state == GameManager.ControlState.CountryLevel)
        {
            SpawnTracePoi(TraceSessionProvinceCode, longitude, latitude);
            LogManager.LogBackend(
                $"[SecurityEventDetailApi] 全国级恢复溯源黄点 | province={TraceSessionProvinceCode}");
            return;
        }

        if (state == GameManager.ControlState.ProvinceLevel)
        {
            string current = ResolveCurrentProvinceCode(currentProvinceCode);
            if (ProvinceCodesEqual(current, TraceSessionProvinceCode))
            {
                SpawnTracePoi(TraceSessionProvinceCode, longitude, latitude);
                LogManager.LogBackend(
                    $"[SecurityEventDetailApi] 省级恢复溯源黄点 | province={TraceSessionProvinceCode}");
            }
            else
            {
                LogManager.LogBackend(
                    $"[SecurityEventDetailApi] 省级非溯源省，已清黄点保留会话 | " +
                    $"current={current ?? "-"} | session={TraceSessionProvinceCode}");
            }

            return;
        }

        LogManager.LogBackend(
            $"[SecurityEventDetailApi] {state} 不显示溯源黄点，已清黄点保留会话 | " +
            $"session={TraceSessionProvinceCode}");
    }

    /// <summary>使用 GameManager 当前状态刷新溯源黄点。</summary>
    public static void RefreshTracePoiForCurrentControlState()
    {
        GameManager manager = GameManager.Instance;
        GameManager.ControlState state = manager != null
            ? manager.CurrentState
            : GameManager.ControlState.EarthLevel;
        string currentProvince = null;
        PlateProvinceFocusResolver.TryGetCachedProvinceCode(out currentProvince);
        RefreshTracePoiForControlState(state, currentProvince);
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
            LogManager.LogBackendWarning($"[SecurityEventDetailApi] record_data 解析失败：{errorMessage}");
        }

        if (data.TryGetRecordLongitudeLatitude(out double longitude, out double latitude))
        {
            LogManager.LogBackend($"[SecurityEventDetailApi] 经纬度：longitude={longitude}, latitude={latitude}");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(data.record_data) || data.originalMap != null)
        {
            LogManager.LogBackendWarning("[SecurityEventDetailApi] record_data / originalMap 中未包含有效经纬度。");
        }

        return hasRecord;
    }

    private static void BeginTraceSessionFromData(SecurityEventDetailData data)
    {
        if (data == null ||
            !data.TryGetRecordLongitudeLatitude(out double longitude, out double latitude))
        {
            LogManager.LogBackendWarning("[SecurityEventDetailApi] 无有效经纬度，未开启溯源会话。");
            return;
        }

        string unitCode = ResolveMapUnitCode(data);
        if (string.IsNullOrWhiteSpace(unitCode) || unitCode == "0")
        {
            LogManager.LogBackendWarning(
                "[SecurityEventDetailApi] originalMap 无有效 province/country，未开启溯源会话" +
                "（国内需省级 adcode，国外需国家 SOC）。");
            return;
        }

        HasActiveTraceSession = true;
        TraceSessionProvinceCode = unitCode;
        _traceSessionLongitude = longitude;
        _traceSessionLatitude = latitude;
        LogManager.LogBackend(
            $"[SecurityEventDetailApi] 已记录溯源会话 | unitCode={unitCode} | " +
            $"lon={longitude} | lat={latitude}");
    }

    /// <summary>
    /// 国外事件：若 originalMap.region 与当前大板不一致，切到对应国外板（国内有 province 时不切）。
    /// </summary>
    private static void TryAlignWorldRegionFromOriginalMap(SecurityEventDetailData data)
    {
        if (data?.originalMap == null)
        {
            return;
        }

        // 国内省事件：不按 region 切国外板
        if (!string.IsNullOrWhiteSpace(data.originalMap.province))
        {
            return;
        }

        string regionCode = data.originalMap.region != null ? data.originalMap.region.Trim() : null;
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            // 无 region 时尝试用国家 SOC 反查大板
            string countryCode = data.originalMap.country != null ? data.originalMap.country.Trim() : null;
            if (WorldMapRegionCodeTable.TryGetCountryByCode(countryCode, out WorldMapCountryCodeEntry country) &&
                !string.IsNullOrWhiteSpace(country.plateCode))
            {
                regionCode = country.plateCode.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return;
        }

        if (WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign &&
            string.Equals(
                WorldMapRegionContext.PlateCode,
                regionCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorldMapRegionController regionController = WorldMapRegionController.Instance;
        if (regionController == null)
        {
            LogManager.LogBackendWarning(
                "[SecurityEventDetailApi] 未找到 WorldMapRegionController，无法按 region 切板。");
            return;
        }

        if (regionController.SwitchToForeignPlate(regionCode))
        {
            LogManager.LogBackend(
                $"[SecurityEventDetailApi] 已按溯源 region 切换国外大板 | region={regionCode}");
        }
        else
        {
            LogManager.LogBackendWarning(
                $"[SecurityEventDetailApi] 按 region 切板失败 | region={regionCode}");
        }
    }

    private static bool TryGetTraceSessionCoordinates(out double longitude, out double latitude)
    {
        longitude = _traceSessionLongitude;
        latitude = _traceSessionLatitude;
        return HasActiveTraceSession;
    }

    private static void SpawnTracePoi(string provinceCode, double longitude, double latitude)
    {
        POI_Manager poiManager = POI_Manager.Instance;
        if (poiManager == null)
        {
            LogManager.LogBackendWarning("[SecurityEventDetailApi] 未找到 POI_Manager，跳过溯源黄点。");
            return;
        }

        poiManager.SpawnPoiDelayed(provinceCode, POIType.yellow, longitude, latitude);
    }

    private static string ResolveCurrentProvinceCode(string preferredProvinceCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredProvinceCode))
        {
            string preferred = preferredProvinceCode.Trim();
            if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(preferred, out string normalized) &&
                !string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            return preferred;
        }

        if (PlateProvinceFocusResolver.TryGetCachedProvinceCode(out string cached) &&
            !string.IsNullOrWhiteSpace(cached))
        {
            if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(cached, out string cachedNormalized) &&
                !string.IsNullOrWhiteSpace(cachedNormalized))
            {
                return cachedNormalized;
            }

            return cached.Trim();
        }

        return null;
    }

    private static bool ProvinceCodesEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        string a = left.Trim();
        string b = right.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        bool hasA = PlateMapBoundaryDatabase.TryNormalizeProvinceCode(a, out string na);
        bool hasB = PlateMapBoundaryDatabase.TryNormalizeProvinceCode(b, out string nb);
        if (hasA && hasB)
        {
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// 地图单元 code：国内优先 originalMap.province（adcode）；国外回退 country（SOC，如 250）。
    /// </summary>
    private static string ResolveMapUnitCode(SecurityEventDetailData data)
    {
        if (data?.originalMap == null)
        {
            return null;
        }

        SecurityEventOriginalMap map = data.originalMap;
        if (TryNormalizeMapUnitCode(map.province, out string fromProvince))
        {
            return fromProvince;
        }

        if (TryNormalizeMapUnitCode(map.country, out string fromCountry))
        {
            return fromCountry;
        }

        return null;
    }

    private static bool TryNormalizeMapUnitCode(string raw, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string trimmed = raw.Trim();
        if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(trimmed, out string code) &&
            !string.IsNullOrWhiteSpace(code) &&
            code != "0")
        {
            normalized = code;
            return true;
        }

        // 边界表未收录时仍保留原串，供 WorldMapPlateResolver / Geo 名称回退
        if (trimmed != "0")
        {
            normalized = trimmed;
            return true;
        }

        return false;
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
            LogManager.LogBackendWarning("[SecurityEventDetailApi] 请求结果为空。");
            return;
        }

        string body = string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody;
        if (result.IsCancelled)
        {
            LogManager.LogBackend("[SecurityEventDetailApi] 请求已取消。");
            return;
        }

        if (result.IsSuccess)
        {
            LogManager.LogBackend($"[SecurityEventDetailApi] 请求成功，状态码={result.StatusCode}，响应 JSON：\n{body}");
            return;
        }

        LogManager.LogBackendWarning(
            $"[SecurityEventDetailApi] 请求失败，状态码={result.StatusCode}，错误={result.Error}\n响应 JSON：\n{body}");
    }
}
