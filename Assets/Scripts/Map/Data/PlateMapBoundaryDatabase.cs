using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块地图边界数据查询。
/// 国内：Resources/PlateMapBoundaries.json（省级 adcode / "0"）。
/// 国外：Resources/PlateMapForeignBoundaries.json（国家 ISO 数字码 / 大板块 firstClassCode）。
/// </summary>
public static class PlateMapBoundaryDatabase
{
    private const string DomesticResourcesConfigName = "PlateMapBoundaries";
    private const string ForeignResourcesConfigName = "PlateMapForeignBoundaries";
    private const string DomesticConfigDisplayPath = "Assets/Resources/PlateMapBoundaries.json";
    private const string ForeignConfigDisplayPath = "Assets/Resources/PlateMapForeignBoundaries.json";

    private static readonly Dictionary<string, PlateMapBoundaryData> DomesticLookup = BuildDomesticLookup();
    private static readonly Dictionary<string, PlateMapBoundaryData> ForeignLookup = BuildForeignLookup();

    /// <summary>全国整体板块 code。</summary>
    public const string NationalProvinceCode = "0";

    /// <summary>国内边界条目（含全国 "0"）。</summary>
    public static IReadOnlyCollection<PlateMapBoundaryData> All => DomesticLookup.Values;

    /// <summary>国外边界条目（国家 + 大板块）。</summary>
    public static IReadOnlyCollection<PlateMapBoundaryData> AllForeign => ForeignLookup.Values;

    /// <summary>按省 adcode / 国外国家数字码 / 大板块 firstClassCode / "0" 查询边界。</summary>
    public static bool TryGet(string provinceCode, out PlateMapBoundaryData data)
    {
        data = null;
        if (!TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return false;
        }

        if (DomesticLookup.TryGetValue(normalizedCode, out data))
        {
            return true;
        }

        return ForeignLookup.TryGetValue(normalizedCode, out data);
    }

    /// <summary>获取边界；失败返回 null。</summary>
    public static PlateMapBoundaryData GetOrDefault(string provinceCode)
    {
        return TryGet(provinceCode, out PlateMapBoundaryData data) ? data : null;
    }

    /// <summary>
    /// 规范化查询 key：
    /// "0" 保留；国内 6 位 adcode 补零；国外 ISO 数字码保留原数字串；大板块 firstClassCode 原样。
    /// </summary>
    public static bool TryNormalizeProvinceCode(string provinceCode, out string normalizedCode)
    {
        normalizedCode = null;
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return false;
        }

        string trimmed = provinceCode.Trim();
        if (trimmed == NationalProvinceCode)
        {
            normalizedCode = NationalProvinceCode;
            return true;
        }

        // 大板块 firstClassCode（非纯数字）
        if (!IsAllDigits(trimmed))
        {
            if (ForeignLookup.ContainsKey(trimmed))
            {
                normalizedCode = trimmed;
                return true;
            }

            return false;
        }

        if (!int.TryParse(trimmed, out int code))
        {
            return false;
        }

        string d6 = code.ToString("D6");
        if (DomesticLookup.ContainsKey(d6))
        {
            normalizedCode = d6;
            return true;
        }

        // 国外 ISO 数字码：与 WorldMapRegionCodes.secondClassCode 对齐（如 "392"）
        string foreignKey = trimmed;
        if (ForeignLookup.ContainsKey(foreignKey))
        {
            normalizedCode = foreignKey;
            return true;
        }

        // 兼容前导零差异：096 <-> 96
        string noPad = code.ToString();
        if (ForeignLookup.ContainsKey(noPad))
        {
            normalizedCode = noPad;
            return true;
        }

        foreach (string key in ForeignLookup.Keys)
        {
            if (IsAllDigits(key) && int.TryParse(key, out int foreignCode) && foreignCode == code)
            {
                normalizedCode = key;
                return true;
            }
        }

        return false;
    }

    private static bool IsAllDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, PlateMapBoundaryData> BuildDomesticLookup()
    {
        Dictionary<string, PlateMapBoundaryData> lookup = new Dictionary<string, PlateMapBoundaryData>(40);
        string json = LoadConfigJsonText(DomesticResourcesConfigName);
        if (!TryParseResponse(json, out PlateMapBoundaryResponse response))
        {
            Debug.LogError($"[PlateMapBoundaryDatabase] 无法加载国内配置：{DomesticConfigDisplayPath}");
            return lookup;
        }

        AddEntries(lookup, response.entries, padDomesticAdcode: true);
        return lookup;
    }

    private static Dictionary<string, PlateMapBoundaryData> BuildForeignLookup()
    {
        Dictionary<string, PlateMapBoundaryData> lookup =
            new Dictionary<string, PlateMapBoundaryData>(256, StringComparer.OrdinalIgnoreCase);
        string json = LoadConfigJsonText(ForeignResourcesConfigName);
        if (!TryParseResponse(json, out PlateMapBoundaryResponse response))
        {
            Debug.LogWarning($"[PlateMapBoundaryDatabase] 未加载国外配置：{ForeignConfigDisplayPath}");
            return lookup;
        }

        AddEntries(lookup, response.entries, padDomesticAdcode: false);
        return lookup;
    }

    private static void AddEntries(
        Dictionary<string, PlateMapBoundaryData> lookup,
        PlateMapBoundaryData[] entries,
        bool padDomesticAdcode)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PlateMapBoundaryData entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.provinceCode))
            {
                continue;
            }

            entry.NormalizeBounds();
            string key = entry.provinceCode.Trim();
            if (padDomesticAdcode &&
                key != NationalProvinceCode &&
                int.TryParse(key, out int adcode))
            {
                key = adcode.ToString("D6");
                entry.provinceCode = key;
            }

            lookup[key] = entry;
        }
    }

    private static bool TryParseResponse(string json, out PlateMapBoundaryResponse response)
    {
        response = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            response = JsonUtility.FromJson<PlateMapBoundaryResponse>(json);
            return response?.entries != null && response.entries.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string LoadConfigJsonText(string resourcesName)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourcesName);
        return asset != null ? asset.text : null;
    }
}
