using System;

/// <summary>
/// 车辆态势：目标车辆各零部件防护状态响应（partProtectionStatus）。
/// </summary>
[Serializable]
public class PartProtectionStatusResponse
{
    public int code;
    public string msg;
    public PartProtectionStatusData data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>防护状态 data 节点。</summary>
[Serializable]
public class PartProtectionStatusData
{
    /// <summary>当日安全态势上报且含待处理事件的未防护零部件。</summary>
    public PartProtectionStatusPart[] unprotectedParts;

    /// <summary>已防护零部件类型。</summary>
    public PartProtectionStatusPart[] protectedParts;

    public int UnprotectedCount => unprotectedParts != null ? unprotectedParts.Length : 0;
    public int ProtectedCount => protectedParts != null ? protectedParts.Length : 0;
}

/// <summary>单个零部件防护条目。</summary>
[Serializable]
public class PartProtectionStatusPart
{
    /// <summary>零部件类型。</summary>
    public int partType;

    /// <summary>零部件类型名称（如 IDC / CCU / TBOX）。</summary>
    public string partTypeName;

    /// <summary>最新 5 条未处理待办安全事件；无事件时可为 null。</summary>
    public PartProtectionPendingEvent[] pendingEvents;

    public int PendingEventCount => pendingEvents != null ? pendingEvents.Length : 0;
}

/// <summary>零部件待处理安全事件摘要。</summary>
[Serializable]
public class PartProtectionPendingEvent
{
    public string eventId;
    public string eventName;
    /// <summary>入库时间。</summary>
    public string processTime;
}
