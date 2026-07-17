using System;

/// <summary>
/// 车辆态势：目标车辆各零部件防护状态请求体（partProtectionStatus）。
/// </summary>
[Serializable]
public class PartProtectionStatusRequest
{
    public string encryptVin = string.Empty;
    public string startTime = string.Empty;
    public string endTime = string.Empty;

    /// <summary>示例请求 JSON（与 Apifox 文档一致，便于联调）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"encryptVin\": \"ed49f47afa23e45b18d342767495643c\",\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"2026-06-30 23:00:00\"\n" +
        "}";

    public const string DefaultEncryptVin = "ed49f47afa23e45b18d342767495643c";

    public static PartProtectionStatusRequest CreateDefaultTest()
    {
        return Create(
            DefaultEncryptVin,
            startTime: string.Empty,
            endTime: HttpProjectConfig.DefaultQueryEndTime);
    }

    /// <summary>创建请求体；时间为 null 时用项目默认 endTime，startTime 默认可为空串。</summary>
    public static PartProtectionStatusRequest Create(
        string encryptVin,
        string startTime = null,
        string endTime = null)
    {
        return new PartProtectionStatusRequest
        {
            encryptVin = encryptVin != null ? encryptVin.Trim() : string.Empty,
            startTime = startTime ?? string.Empty,
            endTime = endTime ?? HttpProjectConfig.DefaultQueryEndTime,
        };
    }

    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
