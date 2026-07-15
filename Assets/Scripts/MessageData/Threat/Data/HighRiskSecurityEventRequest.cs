using System;

/// <summary>
/// 高危安全事件查询请求体（highRiskSecurityEvent）。
/// </summary>
[Serializable]
public class HighRiskSecurityEventRequest
{
    public string startTime = ThreatQueryDefaults.StartTime;
    public string endTime = ThreatQueryDefaults.EndTime;
    /// <summary>国家 code（空表示中国）。</summary>
    public string firstClassCode = string.Empty;
    /// <summary>省级 adcode（空表示全国不限省）。</summary>
    public string secondClassCode = string.Empty;

    /// <summary>创建国内单省筛选请求：firstClassCode 国家（空=中国），secondClassCode 省级 adcode。</summary>
    public static HighRiskSecurityEventRequest CreateForProvince(
        string provinceCode,
        string startTime = null,
        string endTime = null)
    {
        return new HighRiskSecurityEventRequest
        {
            startTime = ThreatQueryDefaults.ResolveStartTime(startTime),
            endTime = ThreatQueryDefaults.ResolveEndTime(endTime),
            firstClassCode = ThreatRegionCodeSettings.DomesticFirstClassCode,
            secondClassCode = provinceCode ?? string.Empty,
        };
    }

    /// <summary>创建单次区域请求（国内/国外通用）。</summary>
    public static HighRiskSecurityEventRequest CreateForRegion(
        ThreatRegionRequestCodes regionCodes,
        string startTime = null,
        string endTime = null)
    {
        return new HighRiskSecurityEventRequest
        {
            startTime = ThreatQueryDefaults.ResolveStartTime(startTime),
            endTime = ThreatQueryDefaults.ResolveEndTime(endTime),
            firstClassCode = regionCodes.FirstClassCode,
            secondClassCode = regionCodes.SecondClassCode,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
