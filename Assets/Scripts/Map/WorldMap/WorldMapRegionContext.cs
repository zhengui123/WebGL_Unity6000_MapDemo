using System;
using UnityEngine;

/// <summary>
/// 当前世界地图区域运行时状态（由 <see cref="WorldMapRegionController"/> 写入）。
/// </summary>
public static class WorldMapRegionContext
{
    /// <summary>国内 / 国外。</summary>
    public static WorldMapRegionMode Mode { get; private set; } = WorldMapRegionMode.Domestic;

    /// <summary>
    /// 当前板块 code。
    /// 国内：固定为 <see cref="WorldMapRegionCodeTable.DomesticNationalCode"/>（"0"）。
    /// 国外：大板块 firstClassCode（如 EAST_ASIA）。
    /// </summary>
    public static string PlateCode { get; private set; } = "0";

    /// <summary>当前板块显示名（国内="中国"；国外如"东亚"）。</summary>
    public static string PlateName { get; private set; } = "中国";

    /// <summary>
    /// 当前模式下的「全国」单元 code：
    /// 国内="0"；国外=当前 PlateCode。
    /// </summary>
    public static string NationalUnitCode =>
        Mode == WorldMapRegionMode.Domestic
            ? WorldMapRegionCodeTable.DomesticNationalCode
            : PlateCode;

    /// <summary>是否已由控制器初始化过。</summary>
    public static bool IsInitialized { get; private set; }

    public static event Action OnRegionChanged;

    public static void ApplyDomestic()
    {
        Mode = WorldMapRegionMode.Domestic;
        PlateCode = WorldMapRegionCodeTable.DomesticNationalCode;
        PlateName = "中国";
        IsInitialized = true;
        OnRegionChanged?.Invoke();
    }

    public static void ApplyForeignPlate(string plateCode, string plateName)
    {
        Mode = WorldMapRegionMode.Foreign;
        PlateCode = string.IsNullOrWhiteSpace(plateCode) ? string.Empty : plateCode.Trim();
        PlateName = string.IsNullOrWhiteSpace(plateName) ? PlateCode : plateName.Trim();
        IsInitialized = true;

        if (string.IsNullOrWhiteSpace(PlateCode))
        {
            Debug.LogWarning(
                "[WorldMapRegionContext] 国外板块 code 为空。请配置 firstClassCode（如 EAST_ASIA）。");
        }

        OnRegionChanged?.Invoke();
    }

    public static string Describe()
    {
        return Mode == WorldMapRegionMode.Domestic
            ? $"Domestic | plateCode={PlateCode} | plateName={PlateName}"
            : $"Foreign | plateCode={PlateCode} | plateName={PlateName} | nationalUnit={NationalUnitCode}";
    }
}
