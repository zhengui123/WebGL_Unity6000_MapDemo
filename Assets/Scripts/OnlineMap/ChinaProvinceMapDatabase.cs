using System;
using System.Collections.Generic;

/// <summary>
/// 中国省级地图聚焦数据表（34 个省级行政区）。
/// </summary>
public static class ChinaProvinceMapDatabase
{
    private static readonly ChinaProvinceMapFocusData[] Provinces =
    {
        new("北京", 116.4074, 39.9042, 7),
        new("天津", 117.2010, 39.0842, 7),
        new("上海", 121.4737, 31.2304, 7),
        new("重庆", 106.5516, 29.5630, 7),
        new("河北", 114.5025, 38.0455, 7),
        new("山西", 112.5492, 37.8570, 7),
        new("辽宁", 123.4291, 41.7968, 7),
        new("吉林", 125.3245, 43.8868, 7),
        new("黑龙江", 126.6425, 45.7569, 7),
        new("江苏", 118.7674, 32.0415, 7),
        new("浙江", 120.1536, 30.2875, 7),
        new("安徽", 117.2830, 31.8612, 7),
        new("福建", 119.3062, 26.0753, 7),
        new("江西", 115.8922, 28.6765, 7),
        new("山东", 117.0009, 36.6758, 7),
        new("河南", 113.6654, 34.7579, 7),
        new("湖北", 114.2986, 30.5844, 7),
        new("湖南", 112.9823, 28.1941, 7),
        new("广东", 113.2806, 23.1252, 7),
        new("海南", 110.3312, 20.0319, 7),
        new("四川", 104.0657, 30.6595, 7),
        new("贵州", 106.7135, 26.5783, 7),
        new("云南", 102.7123, 25.0406, 7),
        new("陕西", 108.9480, 34.2632, 7),
        new("甘肃", 103.8236, 36.0580, 7),
        new("青海", 101.7789, 36.6232, 7),
        new("台湾", 121.5091, 25.0443, 7),
        new("内蒙古", 111.6708, 40.8183, 7),
        new("广西", 108.3200, 22.8240, 7),
        new("西藏", 91.1322, 29.6604, 7),
        new("宁夏", 106.2782, 38.4664, 7),
        new("新疆", 87.6177, 43.7928, 7),
        new("香港", 114.1734, 22.3200, 7),
        new("澳门", 113.5491, 22.1987, 7),
    };

    private static readonly Dictionary<string, ChinaProvinceMapFocusData> Lookup = BuildLookup();

    /// <summary>全部省级数据（只读）。</summary>
    public static IReadOnlyList<ChinaProvinceMapFocusData> All => Provinces;

    /// <summary>
    /// 按省名称查询（支持「山东」「山东省」及常见全称）。
    /// </summary>
    public static bool TryGet(string provinceName, out ChinaProvinceMapFocusData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(provinceName))
        {
            return false;
        }

        string key = NormalizeProvinceName(provinceName);
        return Lookup.TryGetValue(key, out data);
    }

    private static Dictionary<string, ChinaProvinceMapFocusData> BuildLookup()
    {
        var dict = new Dictionary<string, ChinaProvinceMapFocusData>(Provinces.Length * 3);
        for (int i = 0; i < Provinces.Length; i++)
        {
            ChinaProvinceMapFocusData item = Provinces[i];
            RegisterKey(dict, item.ProvinceName, item);
            RegisterKey(dict, item.ProvinceName + "省", item);
            RegisterKey(dict, item.ProvinceName + "市", item);

            if (item.ProvinceName is "内蒙古" or "广西" or "西藏" or "宁夏" or "新疆")
            {
                RegisterKey(dict, item.ProvinceName + "自治区", item);
            }

            if (item.ProvinceName is "香港" or "澳门")
            {
                RegisterKey(dict, item.ProvinceName + "特别行政区", item);
            }
        }

        // 常见全称别名
        RegisterKey(dict, "内蒙古自治区", dict["内蒙古"]);
        RegisterKey(dict, "广西壮族自治区", dict["广西"]);
        RegisterKey(dict, "西藏自治区", dict["西藏"]);
        RegisterKey(dict, "宁夏回族自治区", dict["宁夏"]);
        RegisterKey(dict, "新疆维吾尔自治区", dict["新疆"]);
        RegisterKey(dict, "台湾省", dict["台湾"]);

        return dict;
    }

    private static void RegisterKey(
        Dictionary<string, ChinaProvinceMapFocusData> dict,
        string key,
        ChinaProvinceMapFocusData data)
    {
        if (string.IsNullOrWhiteSpace(key) || data == null)
        {
            return;
        }

        dict[NormalizeProvinceName(key)] = data;
    }

    private static string NormalizeProvinceName(string name)
    {
        return name.Trim()
            .Replace(" ", string.Empty)
            .Replace("　", string.Empty);
    }
}
