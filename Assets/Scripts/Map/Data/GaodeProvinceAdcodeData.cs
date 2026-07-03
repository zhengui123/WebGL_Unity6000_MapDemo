using System;

/// <summary>
/// 省级行政区 adcode 接口响应（与后端 JSON 结构一致，供 JsonUtility 反序列化）。
/// </summary>
[Serializable]
public class GaodeProvinceAdcodeResponse
{
    public int code;
    public string msg;
    public GaodeProvinceAdcodeItem[] data;
}

/// <summary>
/// 单条省级行政区对照：secondClassCode 为高德 adcode，secondClass 为中文全称。
/// </summary>
[Serializable]
public class GaodeProvinceAdcodeItem
{
    public string firstClassCode;
    public string firstClass;
    public string secondClassCode;
    public string secondClass;
    public bool isChina;
}
