using System;

/// <summary>
/// 告警事件分页查询响应（getBasicEventPage）。
/// </summary>
[Serializable]
public class BasicEventPageResponse
{
    public int code;
    public string msg;
    public BasicEventPageData data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>分页数据容器。</summary>
[Serializable]
public class BasicEventPageData
{
    public int total;
    public int pageNum;
    public int pageSize;
    public BasicEventItem[] records;
}

/// <summary>单条告警事件（字段与后端 JSON 对齐，可按实际接口增补）。</summary>
[Serializable]
public class BasicEventItem
{
    public string eventId;
    public string eventName;
    public string eventLevel;
    public string eventType;
    public string eventTime;
    public string vin;
    public string vinEncrypt;
    public string province;
    public string city;
}
