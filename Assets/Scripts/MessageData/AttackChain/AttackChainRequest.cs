using System;

/// <summary>
/// 车辆态势：攻击链路请求体（attackChain）。
/// </summary>
[Serializable]
public class AttackChainRequest
{
    public string encryptVin = string.Empty;
    public string startTime = string.Empty;
    public string endTime = string.Empty;

    /// <summary>示例请求 JSON（文档示意；运行时默认窗为当日 0 点～当前）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"encryptVin\": \"ed49f47afa23e45b18d342767495643c\",\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"\"\n" +
        "}";

    public const string DefaultEncryptVin = "ed49f47afa23e45b18d342767495643c";

    public static AttackChainRequest CreateDefaultTest()
    {
        return Create(DefaultEncryptVin, startTime: null, endTime: null);
    }

    /// <summary>创建请求体；时间为 null/空时：start=当日 0 点，end=当前时间。</summary>
    public static AttackChainRequest Create(
        string encryptVin,
        string startTime = null,
        string endTime = null)
    {
        return new AttackChainRequest
        {
            encryptVin = encryptVin != null ? encryptVin.Trim() : string.Empty,
            startTime = BackendDateTimeTool.ResolveStartTimeOrToday(startTime),
            endTime = BackendDateTimeTool.ResolveEndTimeOrNow(endTime),
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
