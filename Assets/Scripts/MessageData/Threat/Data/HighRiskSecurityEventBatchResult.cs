using System;
using System.Collections.Generic;

/// <summary>
/// 国内全国单次请求汇总结果。
/// </summary>
public sealed class HighRiskSecurityEventBatchResult
{
    public int TotalRegionCount;
    public int SuccessRegionCount;
    public int FailedRegionCount;
    public int TotalEventCount;
    public bool IsAllRegionsSucceeded => FailedRegionCount == 0 && TotalRegionCount > 0;
}
