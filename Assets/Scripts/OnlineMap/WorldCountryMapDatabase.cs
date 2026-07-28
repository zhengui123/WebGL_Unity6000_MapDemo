using System;
using System.Collections.Generic;

/// <summary>
/// 国外国家级地图聚焦数据（国家名/代码 + 中心经纬度 + 推荐 zoom）。
/// 数据来源：公开国家平均经纬度表；zoom 复用国内默认值 7。
/// </summary>
public static class WorldCountryMapDatabase
{
    private static readonly Entry[] Countries =
    {
        new("004", "阿富汗", 65, 33, 7),
        new("008", "阿尔巴尼亚", 20, 41, 7),
        new("010", "南极（多国主张，冻结状态）", 0, -90, 7),
        new("012", "阿尔及利亚", 3, 28, 7),
        new("020", "安道尔", 1.6, 42.5, 7),
        new("024", "安哥拉", 18.5, -12.5, 7),
        new("028", "安提瓜和巴布达", -61.8, 17.05, 7),
        new("032", "阿根廷", -64, -34, 7),
        new("036", "澳大利亚", 133, -27, 7),
        new("040", "奥地利", 13.3333, 47.3333, 7),
        new("044", "巴哈马", -76, 24.25, 7),
        new("048", "巴林", 50.55, 26, 7),
        new("050", "孟加拉国", 90, 24, 7),
        new("052", "巴巴多斯", -59.5333, 13.1667, 7),
        new("056", "比利时", 4, 50.8333, 7),
        new("064", "不丹", 90.5, 27.5, 7),
        new("068", "玻利维亚", -65, -17, 7),
        new("070", "波黑", 18, 44, 7),
        new("072", "博茨瓦纳", 24, -22, 7),
        new("076", "巴西", -55, -10, 7),
        new("084", "伯利兹", -88.75, 17.25, 7),
        new("090", "所罗门群岛", 159, -8, 7),
        new("096", "文莱", 114.6667, 4.5, 7),
        new("100", "保加利亚", 25, 43, 7),
        new("104", "缅甸", 98, 22, 7),
        new("108", "布隆迪", 30, -3.5, 7),
        new("112", "白俄罗斯", 28, 53, 7),
        new("116", "柬埔寨", 105, 13, 7),
        new("120", "喀麦隆", 12, 6, 7),
        new("124", "加拿大", -95, 60, 7),
        new("132", "佛得角", -24, 16, 7),
        new("140", "中非共和国", 21, 7, 7),
        new("144", "斯里兰卡", 81, 7, 7),
        new("148", "乍得", 19, 15, 7),
        new("152", "智利", -71, -30, 7),
        new("156", "中国", 105, 35, 7),
        new("158", "中国台湾地区", 121, 23.5, 7),
        new("170", "哥伦比亚", -72, 4, 7),
        new("174", "科摩罗", 44.25, -12.1667, 7),
        new("178", "刚果（布）", 15, -1, 7),
        new("180", "刚果（金）", 25, 0, 7),
        new("184", "库克群岛", -159.7667, -21.2333, 7),
        new("188", "哥斯达黎加", -84, 10, 7),
        new("191", "克罗地亚", 15.5, 45.1667, 7),
        new("192", "古巴", -80, 21.5, 7),
        new("196", "塞浦路斯", 33, 35, 7),
        new("203", "捷克", 15.5, 49.75, 7),
        new("204", "贝宁", 2.25, 9.5, 7),
        new("208", "丹麦", 10, 56, 7),
        new("212", "多米尼克", -61.3333, 15.4167, 7),
        new("214", "多米尼加", -70.6667, 19, 7),
        new("218", "厄瓜多尔", -77.5, -2, 7),
        new("222", "萨尔瓦多", -88.9167, 13.8333, 7),
        new("226", "赤道几内亚", 10, 2, 7),
        new("231", "埃塞俄比亚", 38, 8, 7),
        new("232", "厄立特里亚", 39, 15, 7),
        new("233", "爱沙尼亚", 26, 59, 7),
        new("242", "斐济", 175, -18, 7),
        new("246", "芬兰", 26, 64, 7),
        new("250", "法国", 2, 46, 7),
        new("254", "法属圭亚那（法）", -53, 4, 7),
        new("258", "法属波利尼西亚", -140, -15, 7),
        new("262", "吉布提", 43, 11.5, 7),
        new("266", "加蓬", 11.75, -1, 7),
        new("270", "冈比亚", -16.5667, 13.4667, 7),
        new("275", "巴勒斯坦", 35.25, 32, 7),
        new("276", "德国", 9, 51, 7),
        new("288", "加纳", -2, 8, 7),
        new("296", "基里巴斯", 173, 1.4167, 7),
        new("300", "希腊", 22, 39, 7),
        new("308", "格林纳达", -61.6667, 12.1167, 7),
        new("316", "关岛（美）", 144.7833, 13.4667, 7),
        new("320", "危地马拉", -90.25, 15.5, 7),
        new("324", "几内亚", -10, 11, 7),
        new("328", "圭亚那", -59, 5, 7),
        new("332", "海地", -72.4167, 19, 7),
        new("336", "梵蒂冈", 12.45, 41.9, 7),
        new("340", "洪都拉斯", -86.5, 15, 7),
        new("348", "匈牙利", 20, 47, 7),
        new("352", "冰岛", -18, 65, 7),
        new("356", "印度", 77, 20, 7),
        new("360", "印度尼西亚", 120, -5, 7),
        new("364", "伊朗", 53, 32, 7),
        new("368", "伊拉克", 44, 33, 7),
        new("372", "爱尔兰", -8, 53, 7),
        new("376", "以色列", 34.75, 31.5, 7),
        new("380", "意大利", 12.8333, 42.8333, 7),
        new("384", "科特迪瓦", -5, 8, 7),
        new("388", "牙买加", -77.5, 18.25, 7),
        new("392", "日本", 138, 36, 7),
        new("398", "哈萨克斯坦", 68, 48, 7),
        new("400", "约旦", 36, 31, 7),
        new("404", "肯尼亚", 38, 1, 7),
        new("408", "朝鲜", 127, 40, 7),
        new("410", "韩国", 127.5, 37, 7),
        new("414", "科威特", 47.6581, 29.3375, 7),
        new("417", "吉尔吉斯斯坦", 75, 41, 7),
        new("418", "老挝", 105, 18, 7),
        new("422", "黎巴嫩", 35.8333, 33.8333, 7),
        new("426", "莱索托", 28.5, -29.5, 7),
        new("428", "拉脱维亚", 25, 57, 7),
        new("430", "利比里亚", -9.5, 6.5, 7),
        new("434", "利比亚", 17, 25, 7),
        new("438", "列支敦士登", 9.5333, 47.1667, 7),
        new("440", "立陶宛", 24, 56, 7),
        new("442", "卢森堡", 6.1667, 49.75, 7),
        new("450", "马达加斯加", 47, -20, 7),
        new("454", "马拉维", 34, -13.5, 7),
        new("458", "马来西亚", 112.5, 2.5, 7),
        new("462", "马尔代夫", 73, 3.25, 7),
        new("466", "马里", -4, 17, 7),
        new("470", "马耳他", 14.5833, 35.8333, 7),
        new("478", "毛里塔尼亚", -12, 20, 7),
        new("480", "毛里求斯", 57.55, -20.2833, 7),
        new("484", "墨西哥", -102, 23, 7),
        new("492", "摩纳哥", 7.4, 43.7333, 7),
        new("496", "蒙古", 105, 46, 7),
        new("498", "摩尔多瓦", 29, 47, 7),
        new("499", "黑山", 19, 42, 7),
        new("504", "摩洛哥", -5, 32, 7),
        new("508", "莫桑比克", 35, -18.25, 7),
        new("512", "阿曼", 57, 21, 7),
        new("516", "纳米比亚", 17, -22, 7),
        new("520", "瑙鲁", 166.9167, -0.5333, 7),
        new("524", "尼泊尔", 84, 28, 7),
        new("528", "荷兰", 5.75, 52.5, 7),
        new("540", "新喀里多尼亚（法）", 165.5, -21.5, 7),
        new("548", "瓦努阿图", 167, -16, 7),
        new("554", "新西兰", 174, -41, 7),
        new("558", "尼加拉瓜", -85, 13, 7),
        new("562", "尼日尔", 8, 16, 7),
        new("566", "尼日利亚", 8, 10, 7),
        new("578", "挪威", 10, 62, 7),
        new("583", "密克罗尼西亚联邦", 158.25, 6.9167, 7),
        new("584", "马绍尔群岛", 168, 9, 7),
        new("585", "帕劳", 134.5, 7.5, 7),
        new("586", "巴基斯坦", 70, 30, 7),
        new("591", "巴拿马", -80, 9, 7),
        new("598", "巴布亚新几内亚", 147, -6, 7),
        new("600", "巴拉圭", -58, -23, 7),
        new("604", "秘鲁", -76, -10, 7),
        new("608", "菲律宾", 122, 13, 7),
        new("616", "波兰", 20, 52, 7),
        new("620", "葡萄牙", -8, 39.5, 7),
        new("624", "几内亚比绍", -15, 12, 7),
        new("626", "东帝汶", 125.5167, -8.55, 7),
        new("630", "波多黎各（美）", -66.5, 18.25, 7),
        new("634", "卡塔尔", 51.25, 25.5, 7),
        new("642", "罗马尼亚", 25, 46, 7),
        new("643", "俄罗斯", 100, 60, 7),
        new("646", "卢旺达", 30, -2, 7),
        new("659", "圣基茨和尼维斯", -62.75, 17.3333, 7),
        new("662", "圣卢西亚", -61.1333, 13.8833, 7),
        new("670", "圣文森特和格林纳丁斯", -61.2, 13.25, 7),
        new("674", "圣马力诺", 12.4167, 43.7667, 7),
        new("678", "圣多美和普林西比", 7, 1, 7),
        new("682", "沙特阿拉伯", 45, 25, 7),
        new("686", "塞内加尔", -14, 14, 7),
        new("688", "塞尔维亚", 21, 44, 7),
        new("694", "塞拉利昂", -11.5, 8.5, 7),
        new("702", "新加坡", 103.8, 1.3667, 7),
        new("703", "斯洛伐克", 19.5, 48.6667, 7),
        new("704", "越南", 106, 16, 7),
        new("705", "斯洛文尼亚", 15, 46, 7),
        new("706", "索马里", 49, 10, 7),
        new("710", "南非", 24, -29, 7),
        new("716", "津巴布韦", 30, -20, 7),
        new("724", "西班牙", -4, 40, 7),
        new("728", "南苏丹", 30, 8, 7),
        new("729", "苏丹", 30, 15, 7),
        new("740", "苏里南", -56, 4, 7),
        new("748", "斯威士兰", 31.5, -26.5, 7),
        new("752", "瑞典", 15, 62, 7),
        new("756", "瑞士", 8, 47, 7),
        new("760", "叙利亚", 38, 35, 7),
        new("762", "塔吉克斯坦", 71, 39, 7),
        new("764", "泰国", 100, 15, 7),
        new("768", "多哥", 1.1667, 8, 7),
        new("776", "汤加", -175, -20, 7),
        new("780", "特立尼达和多巴哥", -61, 11, 7),
        new("784", "阿联酋", 54, 24, 7),
        new("788", "突尼斯", 9, 34, 7),
        new("792", "土耳其", 35, 39, 7),
        new("795", "土库曼斯坦", 60, 40, 7),
        new("798", "图瓦卢", 178, -8, 7),
        new("800", "乌干达", 32, 1, 7),
        new("804", "乌克兰", 32, 49, 7),
        new("807", "北马其顿", 22, 41.8333, 7),
        new("818", "埃及", 30, 27, 7),
        new("826", "英国", -2, 54, 7),
        new("834", "坦桑尼亚", 35, -6, 7),
        new("840", "美国", -97, 38, 7),
        new("854", "布基纳法索", -2, 13, 7),
        new("858", "乌拉圭", -56, -33, 7),
        new("860", "乌兹别克斯坦", 64, 41, 7),
        new("862", "委内瑞拉", -66, 8, 7),
        new("876", "瓦利斯和富图纳", -176.2, -13.3, 7),
        new("882", "萨摩亚", -172.3333, -13.5833, 7),
        new("887", "也门", 48, 15, 7),
        new("894", "赞比亚", 30, -15, 7),
    };

    private static readonly Dictionary<string, ChinaProvinceMapFocusData> LookupByCode = BuildCodeLookup();
    private static readonly Dictionary<string, ChinaProvinceMapFocusData> LookupByName = BuildNameLookup();

    public static bool TryGetByCode(string countryCode, out ChinaProvinceMapFocusData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        return LookupByCode.TryGetValue(NormalizeCode(countryCode), out data);
    }

    public static bool TryGetByName(string countryName, out ChinaProvinceMapFocusData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(countryName))
        {
            return false;
        }

        return LookupByName.TryGetValue(NormalizeName(countryName), out data);
    }

    private static Dictionary<string, ChinaProvinceMapFocusData> BuildCodeLookup()
    {
        var dict = new Dictionary<string, ChinaProvinceMapFocusData>(Countries.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Countries.Length; i++)
        {
            Entry item = Countries[i];
            dict[NormalizeCode(item.Code)] = item.ToFocusData();
        }

        return dict;
    }

    private static Dictionary<string, ChinaProvinceMapFocusData> BuildNameLookup()
    {
        var dict = new Dictionary<string, ChinaProvinceMapFocusData>(Countries.Length * 2, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Countries.Length; i++)
        {
            Entry item = Countries[i];
            ChinaProvinceMapFocusData data = item.ToFocusData();
            RegisterName(dict, item.Name, data);
        }

        RegisterAlias(dict, "中国台湾地区", "台湾");
        RegisterAlias(dict, "中国", "中国");
        RegisterAlias(dict, "韩国", "韩国");
        RegisterAlias(dict, "朝鲜", "朝鲜");
        return dict;
    }

    private static void RegisterAlias(Dictionary<string, ChinaProvinceMapFocusData> dict, string alias, string existingName)
    {
        if (dict.TryGetValue(NormalizeName(existingName), out ChinaProvinceMapFocusData data))
        {
            RegisterName(dict, alias, data);
        }
    }

    private static void RegisterName(Dictionary<string, ChinaProvinceMapFocusData> dict, string name, ChinaProvinceMapFocusData data)
    {
        if (string.IsNullOrWhiteSpace(name) || data == null)
        {
            return;
        }

        dict[NormalizeName(name)] = data;
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().PadLeft(3, '0');
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().Replace(" ", string.Empty).Replace("　", string.Empty);
    }

    private sealed class Entry
    {
        public string Code { get; }
        public string Name { get; }
        public double Longitude { get; }
        public double Latitude { get; }
        public int Zoom { get; }

        public Entry(string code, string name, double longitude, double latitude, int zoom)
        {
            Code = code;
            Name = name;
            Longitude = longitude;
            Latitude = latitude;
            Zoom = zoom;
        }

        public ChinaProvinceMapFocusData ToFocusData()
        {
            return new ChinaProvinceMapFocusData(Name, Longitude, Latitude, Zoom);
        }
    }
}