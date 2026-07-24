using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界地图国外区域 code 对照表。
/// JSON 与国内省表同构（GaodeProvinceAdcode）：firstClass=大板块，secondClass=国家。
/// </summary>
public static class WorldMapRegionCodeTable
{
    private const string ResourcePath = "WorldMapRegionCodes";

    private static bool _loaded;
    private static WorldMapRegionCodesFile _file;
    private static readonly Dictionary<string, WorldMapPlateCodeEntry> PlateByCode =
        new Dictionary<string, WorldMapPlateCodeEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WorldMapPlateCodeEntry> PlateByName =
        new Dictionary<string, WorldMapPlateCodeEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WorldMapCountryCodeEntry> CountryByCode =
        new Dictionary<string, WorldMapCountryCodeEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> CountryNameByCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> CountryCodeByName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>国内全国板块 code（固定 "0"，不在国外 JSON 内）。</summary>
    public static string DomesticNationalCode => PlateMapBoundaryDatabase.NationalProvinceCode;

    /// <summary>国内国家 firstClassCode（高德：CHINA）。</summary>
    public static string DomesticFirstClassCode => "CHINA";

    /// <summary>对照表说明。</summary>
    public static string PlateCodeNote =>
        "国外 JSON 与 GaodeProvinceAdcode 同构：firstClassCode=大板块，secondClassCode=国家数字 code（如 392=日本）。";

    public static IReadOnlyCollection<WorldMapPlateCodeEntry> AllPlates
    {
        get
        {
            EnsureLoaded();
            return PlateByCode.Values;
        }
    }

    /// <summary>原始国外条目（isChina=false）。</summary>
    public static IReadOnlyList<GaodeProvinceAdcodeItem> AllRawItems
    {
        get
        {
            EnsureLoaded();
            return _file?.data ?? Array.Empty<GaodeProvinceAdcodeItem>();
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            Debug.LogWarning($"[WorldMapRegionCodeTable] 未找到 Resources/{ResourcePath}.json，国外对照表为空。");
            _file = new WorldMapRegionCodesFile { data = Array.Empty<GaodeProvinceAdcodeItem>() };
            _loaded = true;
            return;
        }

        try
        {
            _file = JsonUtility.FromJson<WorldMapRegionCodesFile>(asset.text) ?? new WorldMapRegionCodesFile();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WorldMapRegionCodeTable] 解析 JSON 失败：{ex.Message}");
            _file = new WorldMapRegionCodesFile { data = Array.Empty<GaodeProvinceAdcodeItem>() };
        }

        RebuildLookups();
        _loaded = true;
    }

    private static void RebuildLookups()
    {
        PlateByCode.Clear();
        PlateByName.Clear();
        CountryByCode.Clear();
        CountryNameByCode.Clear();
        CountryCodeByName.Clear();

        if (_file?.data == null || _file.data.Length == 0)
        {
            return;
        }

        // plateCode -> 临时国家列表
        Dictionary<string, List<WorldMapCountryCodeEntry>> countriesByPlate =
            new Dictionary<string, List<WorldMapCountryCodeEntry>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> plateNameByCode =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _file.data.Length; i++)
        {
            GaodeProvinceAdcodeItem item = _file.data[i];
            if (item == null || item.isChina)
            {
                continue;
            }

            string plateCode = NormalizeToken(item.firstClassCode);
            string plateName = NormalizeToken(item.firstClass);
            string countryCode = NormalizeToken(item.secondClassCode);
            string countryName = NormalizeToken(item.secondClass);
            if (string.IsNullOrEmpty(plateCode) || string.IsNullOrEmpty(countryCode))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(plateName) && !plateNameByCode.ContainsKey(plateCode))
            {
                plateNameByCode[plateCode] = plateName;
            }

            WorldMapCountryCodeEntry country = new WorldMapCountryCodeEntry
            {
                countryCode = countryCode,
                countryName = countryName,
                plateCode = plateCode,
                plateName = plateNameByCode.TryGetValue(plateCode, out string pn) ? pn : plateName
            };

            if (!countriesByPlate.TryGetValue(plateCode, out List<WorldMapCountryCodeEntry> list))
            {
                list = new List<WorldMapCountryCodeEntry>(8);
                countriesByPlate[plateCode] = list;
            }

            list.Add(country);

            if (!CountryByCode.ContainsKey(countryCode))
            {
                CountryByCode[countryCode] = country;
            }

            if (!string.IsNullOrEmpty(countryName))
            {
                CountryNameByCode[countryCode] = countryName;
                if (!CountryCodeByName.ContainsKey(countryName))
                {
                    CountryCodeByName[countryName] = countryCode;
                }
            }
        }

        foreach (KeyValuePair<string, List<WorldMapCountryCodeEntry>> kv in countriesByPlate)
        {
            string plateCode = kv.Key;
            string plateName = plateNameByCode.TryGetValue(plateCode, out string name) ? name : plateCode;
            WorldMapPlateCodeEntry plate = new WorldMapPlateCodeEntry
            {
                plateCode = plateCode,
                plateName = plateName,
                countries = kv.Value.ToArray()
            };
            PlateByCode[plateCode] = plate;
            if (!string.IsNullOrEmpty(plateName) && !PlateByName.ContainsKey(plateName))
            {
                PlateByName[plateName] = plate;
            }
        }
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r", string.Empty);
    }

    public static bool TryGetPlateByCode(string plateCode, out WorldMapPlateCodeEntry plate)
    {
        EnsureLoaded();
        plate = null;
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            return false;
        }

        return PlateByCode.TryGetValue(NormalizeToken(plateCode), out plate);
    }

    public static bool TryGetPlateByName(string plateName, out WorldMapPlateCodeEntry plate)
    {
        EnsureLoaded();
        plate = null;
        if (string.IsNullOrWhiteSpace(plateName))
        {
            return false;
        }

        return PlateByName.TryGetValue(NormalizeToken(plateName), out plate);
    }

    public static bool TryGetCountryByCode(string countryCode, out WorldMapCountryCodeEntry country)
    {
        EnsureLoaded();
        country = null;
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return CountryByCode.TryGetValue(NormalizeToken(countryCode), out country);
    }

    public static bool TryGetCountryName(string countryCode, out string countryName)
    {
        EnsureLoaded();
        countryName = null;
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return CountryNameByCode.TryGetValue(NormalizeToken(countryCode), out countryName);
    }

    public static bool TryGetCountryCodeByName(string countryName, out string countryCode)
    {
        EnsureLoaded();
        countryCode = null;
        if (string.IsNullOrWhiteSpace(countryName))
        {
            return false;
        }

        return CountryCodeByName.TryGetValue(NormalizeToken(countryName), out countryCode);
    }

    /// <summary>
    /// 解析单元显示名：国内走高德省名；国外走国家/大板块名。
    /// </summary>
    public static bool TryResolveUnitDisplayName(string unitCode, out string displayName)
    {
        displayName = null;
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return false;
        }

        string code = NormalizeToken(unitCode);
        if (string.Equals(code, DomesticNationalCode, StringComparison.OrdinalIgnoreCase) ||
            code == "0")
        {
            displayName = "全国";
            return true;
        }

        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(code, out string provinceName) &&
            !string.IsNullOrWhiteSpace(provinceName))
        {
            displayName = provinceName.Trim();
            return true;
        }

        if (TryGetPlateByCode(code, out WorldMapPlateCodeEntry plate) &&
            !string.IsNullOrWhiteSpace(plate.plateName))
        {
            displayName = plate.plateName.Trim();
            return true;
        }

        if (TryGetCountryName(code, out string countryName) && !string.IsNullOrWhiteSpace(countryName))
        {
            displayName = countryName;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    /// <summary>编辑器：强制重新加载 JSON（改表后刷新面板）。</summary>
    public static void ReloadForEditor()
    {
        _loaded = false;
        EnsureLoaded();
    }
#endif
}
