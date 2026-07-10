/// <summary>
/// 单次威胁接口调用的区域编码（firstClassCode / secondClassCode）。
/// </summary>
public readonly struct ThreatRegionRequestCodes
{
    public ThreatRegionRequestCodes(string firstClassCode, string secondClassCode = "")
    {
        FirstClassCode = firstClassCode ?? string.Empty;
        SecondClassCode = secondClassCode ?? string.Empty;
    }

    public string FirstClassCode { get; }

    public string SecondClassCode { get; }
}
