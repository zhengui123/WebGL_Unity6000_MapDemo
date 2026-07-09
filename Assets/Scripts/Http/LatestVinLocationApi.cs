using System;
using System.Collections.Generic;

/// <summary>
/// 已迁移至 <see cref="VehicleHeatmapApi"/>，保留此类名以兼容旧调用。
/// </summary>
[Obsolete("请改用 MessageData/Core/VehicleHeatmapApi。")]
public static class LatestVinLocationApi
{
    public static void RequestLatestVinLocations(
        string province,
        string region,
        string country,
        string startTime,
        string endTime,
        Action<HttpRequestResult, LatestVinLocationResponse> onCompleted,
        Dictionary<string, string> additionalHeaders = null)
    {
        VehicleHeatmapApi.Request(
            province,
            region,
            country,
            startTime,
            endTime,
            onCompleted,
            additionalHeaders);
    }

    public static bool TryApplySuccessfulResponseFromJson(string json, out string errorMessage)
    {
        return VehicleHeatmapApi.TryApplySuccessfulResponseFromJson(json, null, out errorMessage);
    }

    public static void ApplySuccessfulResponse(LatestVinLocationResponse response)
    {
        VehicleHeatmapApi.ApplySuccessfulResponse(response);
    }
}
