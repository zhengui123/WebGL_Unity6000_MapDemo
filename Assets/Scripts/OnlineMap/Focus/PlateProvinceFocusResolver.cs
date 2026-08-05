using UnityEngine;

/// <summary>
/// 省/国家级单元缓存：国内=省 adcode，国外=国家 SOC。
/// 进省级写入；回区域级 / 换板块清空；供下钻与层级回退恢复原单元。
/// </summary>
public static class PlateProvinceFocusResolver
{
    private static string _cachedProvinceCode;
    private static string _cachedProvinceName;
    private static string _cachedModuleName;

    /// <summary>进省/国家缓存的单元 code；无缓存为 null。</summary>
    public static string CachedProvinceCode => _cachedProvinceCode;

    /// <summary>进省/国家缓存的显示名；无缓存为 null。</summary>
    public static string CachedProvinceName => _cachedProvinceName;

    /// <summary>进省/国家时聚焦的板块模块名（ModuleKey）；无缓存为 null。</summary>
    public static string CachedModuleName => _cachedModuleName;

    /// <summary>是否已缓存有效省/国家信息。</summary>
    public static bool HasCachedProvince =>
        !string.IsNullOrWhiteSpace(_cachedProvinceCode) &&
        !string.IsNullOrWhiteSpace(_cachedProvinceName);

    /// <summary>是否已缓存板块模块名（可与 code 缓存独立存在）。</summary>
    public static bool HasCachedModuleName => !string.IsNullOrWhiteSpace(_cachedModuleName);

    /// <summary>
    /// 解析用于二维地图聚焦的显示名。
    /// 优先级：显式覆盖 → 进省缓存 → 当前聚焦模块 → 默认名。
    /// </summary>
    public static string ResolveProvinceName(string overrideNameOrCode, string defaultProvinceName)
    {
        if (TryResolveFromOverride(overrideNameOrCode, out string fromOverride))
        {
            return fromOverride;
        }

        if (HasCachedProvince)
        {
            return _cachedProvinceName;
        }

        if (TryGetFocusedPlateProvinceCode(out string focusedCode) &&
            TryProvinceCodeToFocusName(focusedCode, out string fromFocused))
        {
            return fromFocused;
        }

        return string.IsNullOrWhiteSpace(defaultProvinceName) ? "山东" : defaultProvinceName.Trim();
    }

    /// <summary>写入省/国家缓存（name + code）。</summary>
    public static bool TryCacheProvince(string provinceCode, string provinceName = null)
    {
        if (string.IsNullOrWhiteSpace(provinceCode) ||
            provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode)
        {
            ClearCache();
            return false;
        }

        if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(provinceCode, out string normalized))
        {
            normalized = provinceCode.Trim();
        }

        string name = provinceName;
        if (string.IsNullOrWhiteSpace(name) &&
            !TryResolveUnitDisplayName(normalized, out name))
        {
            Debug.LogWarning($"[PlateProvinceFocusResolver] 缓存失败：code={normalized} 无法解析显示名。");
            return false;
        }

        _cachedProvinceCode = normalized;
        _cachedProvinceName = name.Trim();
        Debug.Log(
            $"[PlateProvinceFocusResolver] 已缓存省级 | code={_cachedProvinceCode} | name={_cachedProvinceName}");
        return true;
    }

    /// <summary>从板块模块解析并缓存 name/code。</summary>
    public static bool TryCacheFromModule(PlateMapDisplayModule module)
    {
        if (module == null)
        {
            return false;
        }

        _cachedModuleName = module.ModuleKey;
        return TryCacheFromModuleName(module.gameObject.name, module);
    }

    /// <summary>从模块名 / 可选模块引用解析并缓存。</summary>
    public static bool TryCacheFromModuleName(string moduleName, PlateMapDisplayModule module = null)
    {
        if (!TryResolveProvinceCodeFromModule(moduleName, module, out string code))
        {
            return false;
        }

        if (!TryResolveUnitDisplayName(code, out string name))
        {
            if (module != null && !string.IsNullOrWhiteSpace(module.DisplayName))
            {
                name = module.DisplayName.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(moduleName))
            {
                name = moduleName.Trim();
            }
            else
            {
                return false;
            }
        }

        _cachedModuleName = module != null ? module.ModuleKey : moduleName;
        return TryCacheProvince(code, name);
    }

    /// <summary>清空缓存（回区域/国家级，或换板块时）。</summary>
    public static void ClearCache()
    {
        if (!HasCachedProvince && string.IsNullOrEmpty(_cachedModuleName))
        {
            return;
        }

        Debug.Log(
            $"[PlateProvinceFocusResolver] 清空省级缓存 | was code={_cachedProvinceCode} name={_cachedProvinceName}");
        _cachedProvinceCode = null;
        _cachedProvinceName = null;
        _cachedModuleName = null;
    }

    /// <summary>读取缓存的 code；无缓存返回 false。</summary>
    public static bool TryGetCachedProvinceCode(out string provinceCode)
    {
        provinceCode = _cachedProvinceCode;
        return !string.IsNullOrWhiteSpace(provinceCode);
    }

    /// <summary>从当前 FocusedModule 读取板块 provinceCode（不写缓存）。</summary>
    public static bool TryGetFocusedPlateProvinceCode(out string provinceCode)
    {
        provinceCode = null;

        PlateMapDisplayController display = PlateMapDisplayController.Instance;
        if (display == null || display.FocusedModule == null)
        {
            return false;
        }

        return TryResolveProvinceCodeFromModule(
            display.FocusedModule.gameObject.name,
            display.FocusedModule,
            out provinceCode);
    }

    /// <summary>单元 code → 显示名（国内省简称 / 国外国家名）。</summary>
    public static bool TryProvinceCodeToFocusName(string provinceCode, out string provinceName)
    {
        return TryResolveUnitDisplayName(provinceCode, out provinceName);
    }

    /// <summary>单元 code → 显示名；国内优先 Gaode/Boundary，国外走世界地图对照表。</summary>
    private static bool TryResolveUnitDisplayName(string provinceCode, out string provinceName)
    {
        provinceName = null;
        if (string.IsNullOrWhiteSpace(provinceCode) ||
            provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode)
        {
            return false;
        }

        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string shortName) &&
            ChinaProvinceMapDatabase.TryGet(shortName, out _))
        {
            provinceName = shortName;
            return true;
        }

        if (PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData boundary) &&
            !string.IsNullOrWhiteSpace(boundary.provinceName))
        {
            if (ChinaProvinceMapDatabase.TryGet(boundary.provinceName, out ChinaProvinceMapFocusData data))
            {
                provinceName = data.ProvinceName;
                return true;
            }

            provinceName = boundary.provinceName.Trim();
            return true;
        }

        if (WorldMapRegionCodeTable.TryResolveUnitDisplayName(provinceCode.Trim(), out string worldName) &&
            !string.IsNullOrWhiteSpace(worldName))
        {
            provinceName = worldName.Trim();
            return true;
        }

        return false;
    }

    private static bool TryResolveProvinceCodeFromModule(
        string moduleName,
        PlateMapDisplayModule module,
        out string provinceCode)
    {
        provinceCode = null;

        if (module != null)
        {
            PlateMapGeoConverter geo =
                module.GetComponentInParent<PlateMapGeoConverter>() ??
                module.GetComponentInChildren<PlateMapGeoConverter>(true);
            if (geo != null && !string.IsNullOrWhiteSpace(geo.ProvinceCode))
            {
                if (PlateMapBoundaryDatabase.TryNormalizeProvinceCode(geo.ProvinceCode, out provinceCode))
                {
                    return provinceCode != PlateMapBoundaryDatabase.NationalProvinceCode;
                }

                provinceCode = geo.ProvinceCode.Trim();
                return provinceCode != PlateMapBoundaryDatabase.NationalProvinceCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            provinceCode = CarHotManager.ResolveProvinceCodeFromModuleName(moduleName);
            return !string.IsNullOrWhiteSpace(provinceCode) &&
                   provinceCode != PlateMapBoundaryDatabase.NationalProvinceCode;
        }

        return false;
    }

    private static bool TryResolveFromOverride(string overrideNameOrCode, out string provinceName)
    {
        provinceName = null;
        if (string.IsNullOrWhiteSpace(overrideNameOrCode))
        {
            return false;
        }

        string trimmed = overrideNameOrCode.Trim();

        if (TryProvinceCodeToFocusName(trimmed, out provinceName))
        {
            return true;
        }

        if (ChinaProvinceMapDatabase.TryGet(trimmed, out ChinaProvinceMapFocusData data))
        {
            provinceName = data.ProvinceName;
            return true;
        }

        provinceName = trimmed;
        return true;
    }
}
