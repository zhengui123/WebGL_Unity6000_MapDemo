using System;

/// <summary>
/// 工单处置概览 POST 请求参数（与 Apifox raw JSON 字段一致）。
/// </summary>
[Serializable]
public class WorkOrderDisposalOverviewRequest
{
    public string startTime = string.Empty;
    public string endTime = "2026-06-30 23:00:00";
    public string province = string.Empty;
    public string region = string.Empty;
    public string country = string.Empty;

    /// <summary>Demo 默认请求参数 JSON（格式化，便于 UI 展示）。</summary>
    public const string DefaultJson =
        "{\n" +
        "  \"startTime\": \"\",\n" +
        "  \"endTime\": \"2026-06-30 23:00:00\",\n" +
        "  \"province\": \"\",\n" +
        "  \"region\": \"\",\n" +
        "  \"country\": \"\"\n" +
        "}";

    public static WorkOrderDisposalOverviewRequest CreateDefault()
    {
        return new WorkOrderDisposalOverviewRequest();
    }

    /// <summary>序列化为紧凑 JSON（实际 POST 提交用）。</summary>
    public string ToCompactJson()
    {
        return HttpJsonParser.ToJson(this);
    }
}
