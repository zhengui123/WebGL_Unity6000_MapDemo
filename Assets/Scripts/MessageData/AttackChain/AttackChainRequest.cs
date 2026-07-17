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

    /// <summary>示例请求 JSON（与 Apifox 文档一致，便于联调）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"encryptVin\": \"ed49f47afa23e45b18d342767495643c\",\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"2026-06-30 00:00:00\"\n" +
        "}";

    public const string DefaultEncryptVin = "ed49f47afa23e45b18d342767495643c";
    public const string DefaultEndTime = "2026-06-30 00:00:00";

    public static AttackChainRequest CreateDefaultTest()
    {
        return Create(DefaultEncryptVin, startTime: string.Empty, endTime: DefaultEndTime);
    }

    /// <summary>创建请求体；时间为 null 时 startTime 为空串，endTime 用文档默认。</summary>
    public static AttackChainRequest Create(
        string encryptVin,
        string startTime = null,
        string endTime = null)
    {
        return new AttackChainRequest
        {
            encryptVin = encryptVin != null ? encryptVin.Trim() : string.Empty,
            startTime = startTime ?? string.Empty,
            endTime = endTime ?? DefaultEndTime,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
