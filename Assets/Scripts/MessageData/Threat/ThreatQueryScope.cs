/// <summary>
/// 威胁态势查询范围：国内与国外使用不同的 firstClassCode / secondClassCode 组合。
/// </summary>
public enum ThreatQueryScope
{
    /// <summary>国内：每个省级 adcode 单独请求一次，secondClassCode 为空。</summary>
    Domestic = 0,

    /// <summary>国外（预留）：使用 <see cref="ThreatRegionCodeSettings"/> 国外编码，每组一次请求。</summary>
    International = 1,
}
