using System;

/// <summary>
/// 车辆热力图接口响应（latestVinLocation）。
/// data 为经纬度点列表：x=经度，y=纬度。
/// </summary>
[Serializable]
public class LatestVinLocationResponse
{
    public int code;
    public string msg;
    public LatestVinLocationItem[] data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>
/// 热力点坐标（与接口 JSON 字段一致，供 JsonUtility 反序列化）。
/// </summary>
[Serializable]
public class LatestVinLocationItem
{
    public double x;
    public double y;
}
