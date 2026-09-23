using System;

/// <summary>
/// 高危安全事件消费标记响应（highRiskEventConsumedMark）。
/// </summary>
[Serializable]
public class HighRiskEventConsumedMarkResponse
{
    public int code;
    public string msg;
    /// <summary>业务 data（文档为 long）。</summary>
    public long data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}
