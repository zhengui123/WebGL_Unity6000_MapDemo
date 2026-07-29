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

    /// <summary>广东省内主要城市。</summary>
    private static readonly (double Lon, double Lat, string City)[] GuangdongCities =
    {
        (113.2644, 23.1291, "广州"),
        (114.0579, 22.5431, "深圳"),
        (113.5767, 22.2707, "珠海"),
        (113.1220, 23.0218, "佛山"),
        (113.7518, 23.0207, "东莞"),
        (116.1225, 24.2886, "梅州"),
        (114.4165, 23.1115, "惠州"),
        (113.3824, 22.5211, "中山"),
        (110.3594, 21.2707, "湛江"),
        (111.9822, 21.8579, "阳江"),
        (113.5945, 24.8104, "韶关"),
        (116.6819, 23.3541, "汕头"),
    };

    /// <summary>江苏省内主要城市。</summary>
    private static readonly (double Lon, double Lat, string City)[] JiangsuCities =
    {
        (118.7969, 32.0603, "南京"),
        (120.5853, 31.2989, "苏州"),
        (120.3119, 31.4912, "无锡"),
        (119.9469, 31.7720, "常州"),
        (120.8646, 32.0162, "南通"),
        (119.0153, 33.6104, "淮安"),
        (117.1848, 34.2618, "徐州"),
        (119.4210, 32.3932, "扬州"),
        (119.4528, 32.2044, "镇江"),
        (120.1399, 33.3776, "盐城"),
        (118.2752, 33.9630, "宿迁"),
        (119.9152, 32.4849, "泰州"),
    };

    /// <summary>日本主要城市（secondClassCode=392）。</summary>
    private static readonly (double Lon, double Lat, string City)[] JapanCities =
    {
        (139.6917, 35.6895, "东京"),
        (135.5023, 34.6937, "大阪"),
        (136.9066, 35.1815, "名古屋"),
        (141.3545, 43.0618, "札幌"),
        (130.4017, 33.5904, "福冈"),
        (140.8822, 38.2682, "仙台"),
        (132.4553, 34.3853, "广岛"),
        (139.6380, 35.4437, "横滨"),
        (135.7681, 35.0116, "京都"),
        (138.3831, 34.9756, "静冈"),
        (140.4468, 36.3414, "水户"),
        (138.1810, 36.6513, "长野"),
    };

    /// <summary>韩国主要城市（secondClassCode=410）。</summary>
    private static readonly (double Lon, double Lat, string City)[] KoreaCities =
    {
        (126.9780, 37.5665, "首尔"),
        (129.0756, 35.1796, "釜山"),
        (126.7052, 37.4563, "仁川"),
        (128.6014, 35.8714, "大邱"),
        (127.3845, 36.3504, "大田"),
        (126.8526, 35.1595, "光州"),
        (129.3114, 35.5384, "蔚山"),
        (127.2890, 36.4801, "世宗"),
        (126.7314, 37.4569, "富川"),
        (127.0467, 37.2636, "水原"),
        (128.5680, 35.8562, "昌原"),
        (127.1489, 37.4563, "城南"),
    };

    /// <summary>蒙古主要城市（secondClassCode=496）。</summary>
    private static readonly (double Lon, double Lat, string City)[] MongoliaCities =
    {
        (106.9057, 47.8864, "乌兰巴托"),
        (106.2700, 49.4800, "达尔汗"),
        (100.1540, 49.0260, "额尔登特"),
        (100.1620, 47.9070, "哈拉和林"),
        (111.9080, 48.0050, "乔巴山"),
        (91.6410, 46.3720, "科布多"),
        (100.7730, 46.2660, "巴彦洪戈尔"),
        (106.2700, 44.8800, "曼达勒戈壁"),
        (114.5030, 48.0730, "温都尔汗"),
        (89.1670, 48.9680, "乌列盖"),
        (100.1500, 47.4800, "布尔干"),
        (104.0500, 47.3200, "中戈壁"),
    };

    /// <summary>朝鲜主要城市（secondClassCode=408）。</summary>
    private static readonly (double Lon, double Lat, string City)[] NorthKoreaCities =
    {
        (125.7625, 39.0392, "平壤"),
        (127.4460, 39.1600, "元山"),
        (129.7500, 41.8000, "清津"),
        (125.2100, 39.9100, "新义州"),
        (127.4300, 39.0300, "咸兴"),
        (130.3800, 42.4400, "罗先"),
        (125.3200, 38.7400, "南浦"),
        (126.5700, 37.9300, "开城"),
        (125.9000, 39.1500, "顺川"),
        (127.2500, 40.9700, "江界"),
        (128.1700, 40.1000, "惠山"),
        (129.1800, 40.6700, "金策"),
    };

    /// <summary>山东+黑龙江各 10 条，用于测试国家多省高亮与取第一条进省。</summary>
    public static string BuildMultiProvinceQualifiedJson()
    {
        List<string> items = new List<string>(24);
        AppendProvinceEventsFromCities(items, "370000", "山东", ShandongCities, 10, "VIN_SD_", distinctVin: true);
        AppendProvinceEventsFromCities(items, "230000", "黑龙江", HeilongjiangCities, 10, "VIN_HLJ_", distinctVin: true);
        return WrapResponse(items);
    }

    /// <summary>
    /// 多省达标且每省多辆「热车」：每辆 Vin 出现 ≥3 次，省总条数 ≥10。
    /// 鲁 3 辆 / 黑 4 辆 / 粤 3 辆 / 苏 3 辆，共 13 个可下钻 Vin。
    /// </summary>
    public static string BuildSameVinQualifiedJson()
    {
        List<string> items = new List<string>(48);

        AppendProvinceWithHotVins(
            items,
            "370000",
            "山东",
            ShandongCities,
            new (string Vin, int Count)[]
            {
                ("VIN_SD_HOT_01", 4),
                ("VIN_SD_HOT_02", 3),
                ("VIN_SD_HOT_03", 3),
            });

        AppendProvinceWithHotVins(
            items,
            "230000",
            "黑龙江",
            HeilongjiangCities,
            new (string Vin, int Count)[]
            {
                ("VIN_HLJ_HOT_01", 3),
                ("VIN_HLJ_HOT_02", 3),
                ("VIN_HLJ_HOT_03", 3),
                ("VIN_HLJ_HOT_04", 3),
            });

        AppendProvinceWithHotVins(
            items,
            "440000",
            "广东",
            GuangdongCities,
            new (string Vin, int Count)[]
            {
                ("VIN_GD_HOT_01", 4),
                ("VIN_GD_HOT_02", 4),
                ("VIN_GD_HOT_03", 3),
            });

        AppendProvinceWithHotVins(
            items,
            "320000",
            "江苏",
            JiangsuCities,
            new (string Vin, int Count)[]
            {
                ("VIN_JS_HOT_01", 3),
                ("VIN_JS_HOT_02", 4),
                ("VIN_JS_HOT_03", 4),
            });

        return WrapResponse(items);
    }

    /// <summary>
    /// 东亚多国达标且每国多辆「热车」：每辆 Vin≥3，国总条数≥10。
    /// 日 3 辆 / 韩 4 辆 / 蒙 3 辆 / 朝 3 辆；province 使用 secondClassCode。
    /// </summary>
    public static string BuildEastAsiaSameVinQualifiedJson()
    {
        List<string> items = new List<string>(48);

        AppendProvinceWithHotVins(
            items,
            "392",
            "日本",
            JapanCities,
            new (string Vin, int Count)[]
            {
                ("VIN_JP_HOT_01", 4),
                ("VIN_JP_HOT_02", 3),
                ("VIN_JP_HOT_03", 3),
            },
            countryName: "日本");

        AppendProvinceWithHotVins(
            items,
            "410",
            "韩国",
            KoreaCities,
            new (string Vin, int Count)[]
            {
                ("VIN_KR_HOT_01", 3),
                ("VIN_KR_HOT_02", 3),
                ("VIN_KR_HOT_03", 3),
                ("VIN_KR_HOT_04", 3),
            },
            countryName: "韩国");

        AppendProvinceWithHotVins(
            items,
            "496",
            "蒙古",
            MongoliaCities,
            new (string Vin, int Count)[]
            {
                ("VIN_MN_HOT_01", 4),
                ("VIN_MN_HOT_02", 4),
                ("VIN_MN_HOT_03", 3),
            },
            countryName: "蒙古");

        AppendProvinceWithHotVins(
            items,
            "408",
            "朝鲜",
            NorthKoreaCities,
            new (string Vin, int Count)[]
            {
                ("VIN_KP_HOT_01", 3),
                ("VIN_KP_HOT_02", 4),
                ("VIN_KP_HOT_03", 4),
            },
            countryName: "朝鲜");

        return WrapResponse(items);
    }

    /// <summary>按热车 Vin 重复次数撒点；可选补充若干单次出现的冷车 Vin 抬高省条数。</summary>
    private static void AppendProvinceWithHotVins(
        List<string> target,
        string provinceCode,
        string provinceName,
        (double Lon, double Lat, string City)[] cities,
        (string Vin, int Count)[] hotVins,
        int fillerDistinctCount = 0,
        string fillerVinPrefix = null,
        string countryName = "中国")
    {
        if (cities == null || cities.Length == 0 || hotVins == null || hotVins.Length == 0)
        {
            return;
        }

        int eventSerial = 0;
        int cityCursor = 0;
        for (int v = 0; v < hotVins.Length; v++)
        {
            string vin = hotVins[v].Vin;
            int count = hotVins[v].Count;
            if (string.IsNullOrWhiteSpace(vin) || count <= 0)
            {
                continue;
            }

            for (int r = 0; r < count; r++)
            {
                eventSerial++;
                AppendEventAtCity(
                    target,
                    provinceCode,
                    provinceName,
                    cities,
                    ref cityCursor,
                    eventSerial,
                    vin.Trim(),
                    countryName);
            }
        }

        if (fillerDistinctCount > 0 && !string.IsNullOrWhiteSpace(fillerVinPrefix))
        {
            AppendProvinceEventsFromCities(
                target,
                provinceCode,
                provinceName,
                cities,
                fillerDistinctCount,
                fillerVinPrefix,
                distinctVin: true,
                cityOffset: cityCursor,
                eventSerialStart: eventSerial,
                countryName: countryName);
        }
    }

    private static void AppendEventAtCity(
        List<string> target,
        string provinceCode,
        string provinceName,
        (double Lon, double Lat, string City)[] cities,
        ref int cityCursor,
        int eventSerial,
        string vin,
        string countryName = "中国")
    {
        int cityIndex = cityCursor % cities.Length;
        (double lon, double lat, string city) = cities[cityIndex];
        int dup = cityCursor / cities.Length;
        lon += dup * 0.008d;
        lat += dup * 0.006d;
        cityCursor++;

        string eventId = $"LOCAL_{provinceCode}_{eventSerial:D3}_{city}_{vin}";
        target.Add(BuildItemJson(eventId, vin, provinceCode, provinceName, lon, lat, countryName));
    }

    private static void AppendProvinceEventsFromCities(
        List<string> target,
        string provinceCode,
        string provinceName,
        (double Lon, double Lat, string City)[] cities,
        int count,
        string vinPrefixOrFixed,
        bool distinctVin,
        int cityOffset = 0,
        int eventSerialStart = 0,
        string countryName = "中国")
    {
        if (cities == null || cities.Length == 0 || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int cityIndex = (cityOffset + i) % cities.Length;
            (double lon, double lat, string city) = cities[cityIndex];
            int dup = (cityOffset + i) / cities.Length;
            lon += dup * 0.008d;
            lat += dup * 0.006d;

            string vin = distinctVin ? $"{vinPrefixOrFixed}{i + 1:D2}" : vinPrefixOrFixed;
            string eventId = $"LOCAL_{provinceCode}_{eventSerialStart + i + 1:D3}_{city}_{vin}";
            target.Add(BuildItemJson(
                eventId,
                vin,
                provinceCode,
                provinceName,
                lon,
                lat,
                countryName));
        }
    }

    private static string BuildItemJson(
        string eventId,
        string vin,
        string provinceCode,
        string provinceName,
        double longitude,
        double latitude,
        string countryName = "中国")
    {
        string lon = longitude.ToString("F4", CultureInfo.InvariantCulture);
        string lat = latitude.ToString("F4", CultureInfo.InvariantCulture);
        string country = string.IsNullOrWhiteSpace(countryName) ? "中国" : countryName.Trim();
        return
            "{" +
            $"\"eventId\":\"{eventId}\"," +
            $"\"vin\":\"{vin}\"," +
            "\"eventLevel\":1," +
            $"\"province\":\"{provinceCode}\"," +
            "\"city\":\"\"," +
            "\"district\":\"\"," +
            $"\"region\":\"{provinceName}\"," +
            $"\"country\":\"{country}\"," +
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
