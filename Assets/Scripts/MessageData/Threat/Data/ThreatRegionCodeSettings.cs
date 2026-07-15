using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁态势接口区域编码：firstClassCode=国家，secondClassCode=省。
/// 国内默认全国一次请求（国家/省 code 均为空，后端默认中国）。
/// </summary>
public static class ThreatRegionCodeSettings
{
    /// <summary>当前默认查询范围（可按业务切换为国内/国外）。</summary>
    public static ThreatQueryScope ActiveScope = ThreatQueryScope.Domestic;

    /// <summary>国内国家 code（空表示中国，对应 firstClassCode）。</summary>
    public const string DomesticFirstClassCode = "";

    /// <summary>国内全国查询时不传省 code（对应 secondClassCode 为空）。</summary>
    public const string DomesticSecondClassCode = "";

    // —— 国外调用预留 ——
    public const string InternationalFirstClassCode = "";
    public const string InternationalSecondClassCode = "";

    private static readonly List<ThreatRegionRequestCodes> DomesticRegionBuffer = new List<ThreatRegionRequestCodes>(1);
    private static readonly List<ThreatRegionRequestCodes> InternationalRegionBuffer = new List<ThreatRegionRequestCodes>(4);
    private static readonly List<string> DomesticProvinceCodeBuffer = new List<string>(40);

    /// <summary>打包兜底：与 GaodeProvinceAdcode.json 一致的省级 adcode（仅用于展示/校验，不再用于分批请求）。</summary>
    private static readonly string[] BuiltInDomesticProvinceCodes =
    {
        "110000", "120000", "130000", "140000", "150000", "210000", "220000", "230000",
        "310000", "320000", "330000", "340000", "350000", "360000", "370000", "410000",
        "420000", "430000", "440000", "450000", "460000", "500000", "510000", "520000",
        "530000", "540000", "610000", "620000", "630000", "640000", "650000", "710000",
        "810000", "820000",
    };

    /// <summary>国内全国单次请求编码（firstClassCode 国家，secondClassCode 省；均为空）。</summary>
    public static ThreatRegionRequestCodes GetDomesticNationalRequestRegion()
    {
        return new ThreatRegionRequestCodes(DomesticFirstClassCode, DomesticSecondClassCode);
    }

    /// <summary>已知省级 adcode 列表（用于 UI 展示等，非接口分批请求）。</summary>
    public static IReadOnlyList<string> GetDomesticProvinceCodes()
    {
        DomesticProvinceCodeBuffer.Clear();

        AppendProvinceCodes(DomesticProvinceCodeBuffer, GaodeProvinceAdcodeConverter.AllAdcodes);
        if (DomesticProvinceCodeBuffer.Count > 0)
        {
            return DomesticProvinceCodeBuffer;
        }

        AppendProvinceCodesFromPlateMapBoundary(DomesticProvinceCodeBuffer);
        if (DomesticProvinceCodeBuffer.Count > 0)
        {
            return DomesticProvinceCodeBuffer;
        }

        AppendProvinceCodes(DomesticProvinceCodeBuffer, BuiltInDomesticProvinceCodes);
        return DomesticProvinceCodeBuffer;
    }

    /// <summary>国内请求列表（全国一次）。</summary>
    public static IReadOnlyList<ThreatRegionRequestCodes> GetDomesticRequestRegions()
    {
        DomesticRegionBuffer.Clear();
        DomesticRegionBuffer.Add(GetDomesticNationalRequestRegion());
        return DomesticRegionBuffer;
    }

    /// <summary>按查询范围返回待请求区域列表。</summary>
    public static IReadOnlyList<ThreatRegionRequestCodes> GetRequestRegions(ThreatQueryScope scope)
    {
        switch (scope)
        {
            case ThreatQueryScope.International:
                return BuildInternationalRequestRegions();

            default:
                return GetDomesticRequestRegions();
        }
    }

    private static IReadOnlyList<ThreatRegionRequestCodes> BuildInternationalRequestRegions()
    {
        InternationalRegionBuffer.Clear();
        InternationalRegionBuffer.Add(new ThreatRegionRequestCodes(
            InternationalFirstClassCode,
            InternationalSecondClassCode));
        return InternationalRegionBuffer;
    }

    private static void AppendProvinceCodes(List<string> target, IReadOnlyList<string> source)
    {
        if (target == null || source == null || source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            string code = source[i];
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            string trimmed = code.Trim();
            if (!target.Contains(trimmed))
            {
                target.Add(trimmed);
            }
        }
    }

    private static void AppendProvinceCodes(List<string> target, string[] source)
    {
        if (target == null || source == null || source.Length == 0)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            string code = source[i];
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            string trimmed = code.Trim();
            if (!target.Contains(trimmed))
            {
                target.Add(trimmed);
            }
        }
    }

    private static void AppendProvinceCodesFromPlateMapBoundary(List<string> target)
    {
        if (target == null)
        {
            return;
        }

        foreach (PlateMapBoundaryData entry in PlateMapBoundaryDatabase.All)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.provinceCode))
            {
                continue;
            }

            if (entry.provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode)
            {
                continue;
            }

            if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(entry.provinceCode, out string normalizedCode))
            {
                continue;
            }

            if (!target.Contains(normalizedCode))
            {
                target.Add(normalizedCode);
            }
        }
    }
}
