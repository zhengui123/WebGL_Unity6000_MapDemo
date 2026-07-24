using System;
using UnityEngine;

/// <summary>
/// 世界地图单元解析：provinceCode / 国家 SOC / 板块 code → 场景模块名 / 显示名。
/// 业务侧应统一经由此类，不再直接依赖国内 Boundary / Gaode 分支。
/// </summary>
public static class WorldMapPlateResolver
{
    /// <summary>当前模式的「全国」单元 code。</summary>
    public static string ResolveNationalUnitCode()
    {
        if (!WorldMapRegionContext.IsInitialized)
        {
            return WorldMapRegionCodeTable.DomesticNationalCode;
        }

        return WorldMapRegionContext.NationalUnitCode;
    }

    /// <summary>
    /// 空 code 时返回默认子级单元：国内=GameManager 默认省；国外=控制器默认国家。
    /// </summary>
    public static string ResolveUnitCode(string unitCode)
    {
        if (!string.IsNullOrWhiteSpace(unitCode))
        {
            string code = unitCode.Trim();
            if (WorldMapRegionContext.Mode == WorldMapRegionMode.Domestic &&
                PlateMapBoundaryDatabase.TryNormalizeProvinceCode(code, out string normalized))
            {
                return normalized;
            }

            return code;
        }

        if (WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign)
        {
            WorldMapRegionController controller = WorldMapRegionController.Instance;
            if (controller != null && !string.IsNullOrWhiteSpace(controller.DefaultForeignCountryCode))
            {
                return controller.DefaultForeignCountryCode.Trim();
            }

            return string.Empty;
        }

        GameManager gm = GameManager.Instance;
        return gm != null ? gm.DefaultProvinceCode : "330000";
    }

    /// <summary>解析单元显示名（省名 / 国家名 / 板块名）。</summary>
    public static string ResolveUnitDisplayName(string unitCode)
    {
        string code = ResolveUnitCode(unitCode);
        if (WorldMapRegionCodeTable.TryResolveUnitDisplayName(code, out string name))
        {
            return name;
        }

        if (WorldMapRegionContext.Mode == WorldMapRegionMode.Domestic)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null &&
                string.Equals(code, gm.DefaultProvinceCode, StringComparison.OrdinalIgnoreCase))
            {
                return gm.DefaultProvinceName;
            }
        }

        return code ?? string.Empty;
    }

    /// <summary>
    /// code → 场景聚焦模块名（GO.name）。
    /// 优先事件总线 GeoConverter 注册；其次对照表中文名；国内还可回落省名。
    /// </summary>
    public static bool TryResolveUnitModuleName(string unitCode, out string moduleName)
    {
        moduleName = null;
        string code = ResolveUnitCode(unitCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // 「全国」不对应子模块聚焦名
        if (string.Equals(code, ResolveNationalUnitCode(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, WorldMapRegionCodeTable.DomesticNationalCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub != null)
        {
            string mapped = hub.ResolvePlateMapNameByProvinceCode(code);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                moduleName = mapped.Trim();
                return true;
            }
        }

        if (WorldMapRegionCodeTable.TryResolveUnitDisplayName(code, out string displayName) &&
            !string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(displayName, "全国", StringComparison.Ordinal))
        {
            moduleName = displayName;
            return true;
        }

        return false;
    }

    /// <summary>兼容旧命名：provinceCode → 板块模块名。</summary>
    public static bool TryResolvePlateMapName(string provinceCode, out string plateMapName)
    {
        return TryResolveUnitModuleName(provinceCode, out plateMapName);
    }
}
