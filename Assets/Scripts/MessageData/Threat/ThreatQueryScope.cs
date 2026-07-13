/// <summary>
/// 威胁态势查询范围：国内与国外使用不同的 firstClassCode / secondClassCode 组合。
/// </summary>
public enum ThreatQueryScope
{
    /// <summary>国内：全国单次请求，firstClassCode/secondClassCode 均为空（默认中国、不限省）。</summary>
    Domestic = 0,

    /// <summary>国外（预留）：使用 <see cref="ThreatRegionCodeSettings"/> 国外编码，每组一次请求。</summary>
    International = 1,
}
