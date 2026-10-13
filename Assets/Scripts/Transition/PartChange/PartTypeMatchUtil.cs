using System;
using System.Globalization;

/// <summary>
/// 零部件 partType 标号工具：有效范围为 1~5，与 <see cref="VehicleToPartTransitionController"/> 的 _partRoots 配置一致。
/// </summary>
public static class PartTypeMatchUtil
{
    public const int MinPartType = 1;
    public const int MaxPartType = 5;

    public static bool IsValidPartType(int partType)
    {
        return partType >= MinPartType && partType <= MaxPartType;
    }

    /// <summary>解析 string/int 形式的 partType（如 "2"、2）；非法返回 false。</summary>
    public static bool TryParsePartType(string raw, out int partType)
    {
        partType = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return false;
        }

        if (!IsValidPartType(value))
        {
            return false;
        }

        partType = value;
        return true;
    }

    public static bool TryNormalizePartType(int value, out int partType)
    {
        partType = 0;
        if (!IsValidPartType(value))
        {
            return false;
        }

        partType = value;
        return true;
    }
}
