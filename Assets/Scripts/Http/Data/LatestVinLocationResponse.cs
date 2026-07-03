using System;

/// <summary>
/// 车辆最新位置接口响应（latestVinLocation）。
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
/// 单辆车的最新位置记录（与接口 JSON 字段一致，供 JsonUtility 反序列化）。
/// </summary>
[Serializable]
public class LatestVinLocationItem
{
    public string longitude;
    public string latitude;
    public string province;
    public string city;
    public string district;
    public string region;
    public string country;
    public string vin;
    public string vinEncrypt;
}
