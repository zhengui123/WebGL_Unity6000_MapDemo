using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁态势接口区域编码配置：国内按省单次请求，国外预留多套编码。
/// </summary>
public static class ThreatRegionCodeSettings
{
    /// <summary>当前默认查询范围（可按业务切换为国内/国外）。</summary>
    public static ThreatQueryScope ActiveScope = ThreatQueryScope.Domestic;

    // —— 国外调用预留（后续可扩展为多组编码，每组一次请求）——
    public const string InternationalFirstClassCode = "";
    public const string InternationalSecondClassCode = "";

    private static readonly List<ThreatRegionRequestCodes> DomesticRegionBuffer = new List<ThreatRegionRequestCodes>(40);
    private static readonly List<ThreatRegionRequestCodes> InternationalRegionBuffer = new List<ThreatRegionRequestCodes>(4);
    private static readonly List<string> DomesticProvinceCodeBuffer = new List<string>(40);

    /// <summary>打包兜底：与 GaodeProvinceAdcode.json 一致的省级 adcode。</summary>
    private static readonly string[] BuiltInDomesticProvinceCodes =
    {
        "110000", "120000", "130000", "140000", "150000", "210000", "220000", "230000",
        "310000", "320000", "330000", "340000", "350000", "360000", "370000", "410000",
        "420000", "430000", "440000", "450000", "460000", "500000", "510000", "520000",
        "530000", "540000", "610000", "620000", "630000", "640000", "650000", "710000",
        "810000", "820000",
    };

    /// <summary>国内全部省级 adcode（每个 adcode 对应一次接口调用）。</summary>
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
            Debug.LogWarning(
                $"[ThreatRegionCodeSettings] GaodeProvinceAdcode 未加载，已回退 PlateMapBoundary，" +
                $"省级数={DomesticProvinceCodeBuffer.Count}");
            return DomesticProvinceCodeBuffer;
        }

        AppendProvinceCodes(DomesticProvinceCodeBuffer, BuiltInDomesticProvinceCodes);
        Debug.LogWarning(
            $"[ThreatRegionCodeSettings] 使用内置省级 adcode 列表（打包兜底），省级数={DomesticProvinceCodeBuffer.Count}");
        return DomesticProvinceCodeBuffer;
    }

    /// <summary>国内全部省份请求列表（每省一条）。</summary>
    public static IReadOnlyList<ThreatRegionRequestCodes> GetDomesticRequestRegions()
    {
        return BuildDomesticRequestRegions();
    }

    /// <summary>按查询范围返回待请求区域列表（国内=每省一条，国外=预留编码列表）。</summary>
    public static IReadOnlyList<ThreatRegionRequestCodes> GetRequestRegions(ThreatQueryScope scope)
    {
        switch (scope)
        {
            case ThreatQueryScope.International:
                return BuildInternationalRequestRegions();

            default:
                return BuildDomesticRequestRegions();
        }
    }

    private static IReadOnlyList<ThreatRegionRequestCodes> BuildDomesticRequestRegions()
    {
        DomesticRegionBuffer.Clear();
        IReadOnlyList<string> provinceCodes = GetDomesticProvinceCodes();
        if (provinceCodes == null || provinceCodes.Count == 0)
        {
            return DomesticRegionBuffer;
        }

        for (int i = 0; i < provinceCodes.Count; i++)
        {
            string code = provinceCodes[i];
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            DomesticRegionBuffer.Add(new ThreatRegionRequestCodes(code.Trim(), string.Empty));
        }

        return DomesticRegionBuffer;
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
