using System.Collections.Generic;

/// <summary>
/// 单省威胁告警处理上下文（处理开始时快照）。
/// </summary>
public class ThreatProvinceAlertContext
{
    public string ProvinceCode { get; set; }

    public IReadOnlyList<HighRiskSecurityEventItem> Events { get; set; }
}
