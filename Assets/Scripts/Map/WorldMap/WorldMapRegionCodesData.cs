using System;

/// <summary>
/// Resources/WorldMapRegionCodes.json 反序列化结构。
/// 与国内 <see cref="GaodeProvinceAdcodeResponse"/> 相同：仅包含 isChina=false 的国外条目。
/// firstClassCode/firstClass = 大板块；secondClassCode/secondClass = 国家。
/// </summary>
[Serializable]
public class WorldMapRegionCodesFile
{
    public int code;
    public string msg;
    public GaodeProvinceAdcodeItem[] data;
}

/// <summary>运行时按大板块聚合后的条目（由对照表从 JSON 构建）。</summary>
[Serializable]
public class WorldMapPlateCodeEntry
{
    /// <summary>大板块 code，对应 JSON firstClassCode（如 EAST_ASIA）。</summary>
    public string plateCode;

    /// <summary>大板块中文名，对应 JSON firstClass（如 东亚）。</summary>
    public string plateName;

    public WorldMapCountryCodeEntry[] countries;
}

/// <summary>运行时国家条目（由对照表从 JSON 构建）。</summary>
[Serializable]
public class WorldMapCountryCodeEntry
{
    /// <summary>国家 code，对应 JSON secondClassCode（如 392）。</summary>
    public string countryCode;

    /// <summary>国家中文名，对应 JSON secondClass（如 日本）。</summary>
    public string countryName;

    /// <summary>所属大板块 firstClassCode。</summary>
    public string plateCode;

    /// <summary>所属大板块中文名。</summary>
    public string plateName;
}
