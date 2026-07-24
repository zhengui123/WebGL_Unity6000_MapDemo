using UnityEngine;

/// <summary>
/// 世界地图区域模式：国内省级体系 / 国外大板块体系。
/// </summary>
public enum WorldMapRegionMode
{
    /// <summary>国内：全国 code="0"，子级为省级 adcode。</summary>
    [InspectorName("国内")]
    Domestic = 0,

    /// <summary>国外：全国=当前大板块（firstClassCode），子级为国家 secondClassCode。</summary>
    [InspectorName("国外")]
    Foreign = 1
}
