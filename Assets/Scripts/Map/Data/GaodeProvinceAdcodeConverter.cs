using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 中国省级行政区中文名与高德地图 adcode 互转。
/// 配置见 GaodeProvinceAdcode.json（接口响应格式），运行时从 TextAsset 加载。
/// </summary>
public static class GaodeProvinceAdcodeConverter
{
    private const string ConfigAssetPath = "Assets/Scripts/Map/Data/GaodeProvinceAdcode.json";

    private static readonly (string ShortName, string FullName, string Adcode)[] Provinces = LoadProvinces();
    private static readonly Dictionary<string, string> NameToAdcode = BuildNameToAdcodeLookup();
    private static readonly Dictionary<string, string> AdcodeToName = BuildAdcodeToNameLookup();

    /// <summary>全部省级标准简称（如「山东」）。</summary>
    public static IReadOnlyList<string> AllProvinceNames => GetAllProvinceNames();

    /// <summary>全部省级 adcode（如「370000」）。</summary>
    public static IReadOnlyList<string> AllAdcodes => GetAllAdcodes();

    /// <summary>解析接口 JSON 为响应对象。</summary>
    public static bool TryParseResponse(string json, out GaodeProvinceAdcodeResponse response)
    {
        response = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            response = JsonUtility.FromJson<GaodeProvinceAdcodeResponse>(json);
            return response?.data != null && response.data.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>中文省名 → 高德 adcode。</summary>
    public static bool TryProvinceNameToAdcode(string provinceName, out string adcode)
    {
        adcode = null;
        if (string.IsNullOrWhiteSpace(provinceName))
        {
            return false;
        }

        return NameToAdcode.TryGetValue(NormalizeProvinceName(provinceName), out adcode);
    }

    /// <summary>高德 adcode → 中文省名（标准简称）。</summary>
    public static bool TryAdcodeToProvinceName(string adcode, out string provinceName)
    {
        provinceName = null;
        if (!TryNormalizeAdcode(adcode, out string normalizedAdcode))
        {
            return false;
        }

        return AdcodeToName.TryGetValue(normalizedAdcode, out provinceName);
    }

    /// <summary>中文省名 → adcode；失败时返回 null。</summary>
    public static string ProvinceNameToAdcode(string provinceName)
    {
        return TryProvinceNameToAdcode(provinceName, out string adcode) ? adcode : null;
    }

    /// <summary>adcode → 中文省名；失败时返回 null。</summary>
    public static string AdcodeToProvinceName(string adcode)
    {
        return TryAdcodeToProvinceName(adcode, out string provinceName) ? provinceName : null;
    }

    /// <summary>将「山东省」「北京市」等规范为标准简称「山东」「北京」。</summary>
    public static bool TryGetStandardProvinceName(string provinceName, out string standardName)
    {
        standardName = null;
        if (!TryProvinceNameToAdcode(provinceName, out string adcode))
        {
            return false;
        }

        return TryAdcodeToProvinceName(adcode, out standardName);
    }

    private static (string ShortName, string FullName, string Adcode)[] LoadProvinces()
    {
        string json = LoadConfigJsonText();
        if (!TryParseResponse(json, out GaodeProvinceAdcodeResponse response))
        {
            Debug.LogError($"[GaodeProvinceAdcodeConverter] 无法加载配置：{ConfigAssetPath}");
            return Array.Empty<(string, string, string)>();
        }

        List<(string, string, string)> list = new List<(string, string, string)>(response.data.Length);
        for (int i = 0; i < response.data.Length; i++)
        {
            GaodeProvinceAdcodeItem item = response.data[i];
            if (item == null || string.IsNullOrWhiteSpace(item.secondClassCode) || string.IsNullOrWhiteSpace(item.secondClass))
            {
                continue;
            }

            string adcode = item.secondClassCode.Trim();
            string fullName = item.secondClass.Trim();
            string shortName = ResolveShortName(fullName);
            list.Add((shortName, fullName, adcode));
        }

        return list.ToArray();
    }

    private static string LoadConfigJsonText()
    {
#if UNITY_EDITOR
        TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigAssetPath);
        if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
        {
            return asset.text;
        }
#endif
        TextAsset resourcesAsset = Resources.Load<TextAsset>("GaodeProvinceAdcode");
        return resourcesAsset != null ? resourcesAsset.text : null;
    }

    private static string ResolveShortName(string fullName)
    {
        if (ChinaProvinceMapDatabase.TryGet(fullName, out ChinaProvinceMapFocusData data))
        {
            return data.ProvinceName;
        }

        return StripAdministrativeSuffix(fullName);
    }

    private static string StripAdministrativeSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        string trimmed = name.Trim();
        string[] suffixes =
        {
            "特别行政区",
            "维吾尔自治区",
            "壮族自治区",
            "回族自治区",
            "自治区",
            "省",
            "市",
        };

        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (trimmed.EndsWith(suffix, StringComparison.Ordinal) && trimmed.Length > suffix.Length)
            {
                return trimmed.Substring(0, trimmed.Length - suffix.Length);
            }
        }

        return trimmed;
    }

    private static List<string> GetAllProvinceNames()
    {
        List<string> names = new List<string>(Provinces.Length);
        for (int i = 0; i < Provinces.Length; i++)
        {
            names.Add(Provinces[i].ShortName);
        }

        return names;
    }

    private static List<string> GetAllAdcodes()
    {
        List<string> adcodes = new List<string>(Provinces.Length);
        for (int i = 0; i < Provinces.Length; i++)
        {
            adcodes.Add(Provinces[i].Adcode);
        }

        return adcodes;
    }

    private static Dictionary<string, string> BuildNameToAdcodeLookup()
    {
        Dictionary<string, string> lookup = new Dictionary<string, string>(Provinces.Length * 3);
        for (int i = 0; i < Provinces.Length; i++)
        {
            string shortName = Provinces[i].ShortName;
            string fullName = Provinces[i].FullName;
            string adcode = Provinces[i].Adcode;

            RegisterNameKey(lookup, shortName, adcode);
            RegisterNameKey(lookup, fullName, adcode);
            RegisterNameKey(lookup, shortName + "省", adcode);
            RegisterNameKey(lookup, shortName + "市", adcode);

            if (shortName is "内蒙古" or "广西" or "西藏" or "宁夏" or "新疆")
            {
                RegisterNameKey(lookup, shortName + "自治区", adcode);
            }

            if (shortName is "香港" or "澳门")
            {
                RegisterNameKey(lookup, shortName + "特别行政区", adcode);
            }
        }

        return lookup;
    }

    private static Dictionary<string, string> BuildAdcodeToNameLookup()
    {
        Dictionary<string, string> lookup = new Dictionary<string, string>(Provinces.Length);
        for (int i = 0; i < Provinces.Length; i++)
        {
            lookup[Provinces[i].Adcode] = Provinces[i].ShortName;
        }

        return lookup;
    }

    private static void RegisterNameKey(Dictionary<string, string> lookup, string key, string adcode)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lookup[NormalizeProvinceName(key)] = adcode;
    }

    private static string NormalizeProvinceName(string name)
    {
        return name.Trim()
            .Replace(" ", string.Empty)
            .Replace("　", string.Empty);
    }

    private static bool TryNormalizeAdcode(string adcode, out string normalizedAdcode)
    {
        normalizedAdcode = null;
        if (string.IsNullOrWhiteSpace(adcode))
        {
            return false;
        }

        string trimmed = adcode.Trim();
        if (!int.TryParse(trimmed, out int code))
        {
            return false;
        }

        normalizedAdcode = code.ToString("D6");
        return AdcodeToName.ContainsKey(normalizedAdcode);
    }
}
