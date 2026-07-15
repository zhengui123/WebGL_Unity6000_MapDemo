using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// 本地威胁测试用内嵌 JSON（与 highRiskSecurityEvent 响应对齐）。
/// 经纬度使用省内主要城市坐标，保证落在对应省份内部。
/// </summary>
public static class ThreatLocalAlertTestMockJson
{
    /// <summary>山东省内主要城市（简称区域内，可用于 Demo 撒点）。</summary>
    private static readonly (double Lon, double Lat, string City)[] ShandongCities =
    {
        (117.0009, 36.6758, "济南"),
        (120.3826, 36.0671, "青岛"),
        (121.4479, 37.4638, "烟台"),
        (122.1164, 37.5097, "威海"),
        (118.6746, 37.4345, "东营"),
        (118.0476, 36.8141, "淄博"),
        (117.5565, 34.8571, "枣庄"),
        (119.1067, 36.7093, "潍坊"),
        (116.5872, 35.4154, "济宁"),
        (117.1293, 36.2003, "泰安"),
        (119.4612, 35.4286, "日照"),
        (115.4807, 35.2336, "菏泽"),
    };

    /// <summary>黑龙江省内主要城市。</summary>
    private static readonly (double Lon, double Lat, string City)[] HeilongjiangCities =
    {
        (126.6425, 45.7569, "哈尔滨"),
        (123.9182, 47.3477, "齐齐哈尔"),
        (130.9750, 45.3000, "牡丹江"),
        (130.2925, 47.3047, "佳木斯"),
        (128.8413, 47.7224, "绥化"),
        (130.8451, 46.8082, "七台河"),
        (131.1580, 46.6373, "鸡西"),
        (130.9693, 47.3181, "双鸭山"),
        (125.1362, 46.5893, "大庆"),
        (128.8415, 44.5775, "尚志"),
        (129.5981, 44.5853, "海林"),
        (126.9687, 45.8905, "阿城"),
    };

    /// <summary>山东+黑龙江各 10 条，用于测试国家多省高亮与取第一条进省。</summary>
    public static string BuildMultiProvinceQualifiedJson()
    {
        List<string> items = new List<string>(24);
        AppendProvinceEventsFromCities(items, "370000", "山东", ShandongCities, 10, "VIN_SD_", distinctVin: true);
        AppendProvinceEventsFromCities(items, "230000", "黑龙江", HeilongjiangCities, 10, "VIN_HLJ_", distinctVin: true);
        return WrapResponse(items);
    }

    /// <summary>山东 12 条，其中同一 Vin 出现 ≥3 次，用于车辆大屏预留。</summary>
    public static string BuildSameVinQualifiedJson()
    {
        List<string> items = new List<string>(16);
        const string hotVin = "VIN_THREAT_HOT_001";
        AppendProvinceEventsFromCities(items, "370000", "山东", ShandongCities, 3, hotVin, distinctVin: false);
        AppendProvinceEventsFromCities(items, "370000", "山东", ShandongCities, 9, "VIN_SD_OTHER_", distinctVin: true, cityOffset: 3);
        return WrapResponse(items);
    }

    private static void AppendProvinceEventsFromCities(
        List<string> target,
        string provinceCode,
        string provinceName,
        (double Lon, double Lat, string City)[] cities,
        int count,
        string vinPrefixOrFixed,
        bool distinctVin,
        int cityOffset = 0)
    {
        if (cities == null || cities.Length == 0 || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int cityIndex = (cityOffset + i) % cities.Length;
            (double lon, double lat, string city) = cities[cityIndex];
            // 同一城市多次出现时加极小抖动，仍落在市辖区内
            int dup = (cityOffset + i) / cities.Length;
            lon += dup * 0.008d;
            lat += dup * 0.006d;

            string vin = distinctVin ? $"{vinPrefixOrFixed}{i + 1:D2}" : vinPrefixOrFixed;
            string eventId = $"LOCAL_{provinceCode}_{i + 1:D3}_{city}_{vin}";
            target.Add(BuildItemJson(
                eventId,
                vin,
                provinceCode,
                provinceName,
                lon,
                lat));
        }
    }

    private static string BuildItemJson(
        string eventId,
        string vin,
        string provinceCode,
        string provinceName,
        double longitude,
        double latitude)
    {
        string lon = longitude.ToString("F4", CultureInfo.InvariantCulture);
        string lat = latitude.ToString("F4", CultureInfo.InvariantCulture);
        return
            "{" +
            $"\"eventId\":\"{eventId}\"," +
            $"\"vin\":\"{vin}\"," +
            "\"eventLevel\":1," +
            $"\"province\":\"{provinceCode}\"," +
            "\"city\":\"\"," +
            "\"district\":\"\"," +
            $"\"region\":\"{provinceName}\"," +
            "\"country\":\"中国\"," +
            "\"processTime\":\"2026-07-15 12:00:00\"," +
            $"\"longitude\":\"{lon}\"," +
            $"\"latitude\":\"{lat}\"" +
            "}";
    }

    private static string WrapResponse(List<string> items)
    {
        StringBuilder builder = new StringBuilder(items.Count * 180 + 64);
        builder.Append("{\"code\":");
        builder.Append(HttpProjectConfig.SuccessResponseCode);
        builder.Append(",\"msg\":\"success\",\"data\":[");
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(items[i]);
        }

        builder.Append("]}");
        return builder.ToString();
    }
}
