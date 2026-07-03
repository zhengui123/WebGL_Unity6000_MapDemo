using System;
using System.Collections.Generic;

/// <summary>
/// 综合区域态势 - 事件范围内车辆最新位置接口。
/// province 为空表示全国，否则为指定省份 adcode；每个 vin 取 process_time 最新。
/// </summary>
public static class LatestVinLocationApi
{
    /// <summary>
    /// 请求车辆最新位置；成功且 code=10000 时自动写入 <see cref="HttpVehicleLocationDataStore"/>。
    /// </summary>
    /// <param name="province">省份 adcode，null/空表示全国。</param>
    /// <param name="region">区域，可空。</param>
    /// <param name="country">国家，可空。</param>
    /// <param name="startTime">查询开始时间，可空（使用项目默认）。</param>
    /// <param name="endTime">查询结束时间，可空（使用项目默认）。</param>
    /// <param name="onCompleted">请求完成回调（含 HTTP 与业务响应）。</param>
    /// <param name="additionalHeaders">额外请求头（与 <see cref="HttpProjectConfig"/> 默认头合并，同键覆盖）。</param>
    public static void RequestLatestVinLocations(
        string province,
        string region,
        string country,
        string startTime,
        string endTime,
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        ComprehensiveRegionRequest requestBody = ComprehensiveRegionRequest.Create(
            province,
            region,
            country,
            startTime,
            endTime);
        string url = HttpProjectConfig.BuildApiUrl(HttpProjectConfig.LatestVinLocationPath);

        HttpService.Instance.PostJson<ComprehensiveRegionRequest, LatestVinLocationResponse>(
            url,
            requestBody,
            (result, response) =>
            {
                if (result != null && result.IsSuccess && response != null && response.IsSuccess)
                {
                    HttpVehicleLocationDataStore.Instance.MergeFromResponse(response);
                }

                onCompleted?.Invoke(result, response);
            },
            HttpProjectConfig.MergeDefaultHeaders(additionalHeaders));
    }
}
