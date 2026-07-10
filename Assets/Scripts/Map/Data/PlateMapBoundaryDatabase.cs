using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块地图边界数据查询：从 Resources/PlateMapBoundaries.json 加载各省及全国外接矩形。
/// provinceCode 为 string；"0" 表示全国整体大板块，其余与 GaodeProvinceAdcode 一致。
/// </summary>
public static class PlateMapBoundaryDatabase
{
    private const string ResourcesConfigName = "PlateMapBoundaries";
    private const string ConfigDisplayPath = "Assets/Resources/PlateMapBoundaries.json";

    private static readonly Dictionary<string, PlateMapBoundaryData> Lookup = BuildLookup();

    /// <summary>全国整体板块 code。</summary>
    public const string NationalProvinceCode = "0";

    /// <summary>全部边界条目（只读副本引用）。</summary>
    public static IReadOnlyCollection<PlateMapBoundaryData> All => Lookup.Values;

    /// <summary>按省级 adcode 或 "0" 查询边界；失败返回 false。</summary>
    public static bool TryGet(string provinceCode, out PlateMapBoundaryData data)
    {
        data = null;
        if (!TryNormalizeProvinceCode(provinceCode, out string normalizedCode))
        {
            return false;
        }

        return Lookup.TryGetValue(normalizedCode, out data);
    }

    /// <summary>获取边界；失败返回 null。</summary>
    public static PlateMapBoundaryData GetOrDefault(string provinceCode)
    {
        return TryGet(provinceCode, out PlateMapBoundaryData data) ? data : null;
    }

    /// <summary>规范化省级 code：去空白；6 位 adcode 补零；"0" 保留。</summary>
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

        if (!int.TryParse(trimmed, out int code))
        {
            return false;
        }

        normalizedCode = code.ToString("D6");
        return Lookup.ContainsKey(normalizedCode);
    }

    private static Dictionary<string, PlateMapBoundaryData> BuildLookup()
    {
        Dictionary<string, PlateMapBoundaryData> lookup = new Dictionary<string, PlateMapBoundaryData>(40);
        string json = LoadConfigJsonText();
        if (!TryParseResponse(json, out PlateMapBoundaryResponse response))
        {
            Debug.LogError($"[PlateMapBoundaryDatabase] 无法加载配置：{ConfigDisplayPath}");
            return lookup;
        }

        for (int i = 0; i < response.entries.Length; i++)
        {
            PlateMapBoundaryData entry = response.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.provinceCode))
            {
                continue;
            }

            entry.NormalizeBounds();
            string key = entry.provinceCode.Trim();
            if (key != NationalProvinceCode && int.TryParse(key, out int adcode))
            {
                key = adcode.ToString("D6");
                entry.provinceCode = key;
            }

            lookup[key] = entry;
        }

        return lookup;
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

    private static string LoadConfigJsonText()
    {
        TextAsset asset = Resources.Load<TextAsset>(ResourcesConfigName);
        return asset != null ? asset.text : null;
    }
}
