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

    /// <summary>
    /// 2026-10-13 真实样例：重庆 500000（11 条多 VIN）+ 上海 310000（8 条同 VIN）。
    /// </summary>
    public const string ChongqingShanghai1013Json =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"eventId\":\"636bfaa92e6a49dfaa04cad4eba42e96\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:11\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"22be9a64810b4fad8099e97a42ecd38f\",\"vin\":\"YQF******00000053\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvvzuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:06\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"ecc6048eea774048a633e867f8683756\",\"vin\":\"YQF******00000052\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvg3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:06\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"3f811eb50ed14247a2be9103555e5792\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"8236eddf9a7b4ddc9120a0706e755135\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"a8bd20a8d3264b458f20765c54a02e23\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"248a1fd1c2ce4eb3b93b4615a9587962\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"aeba0cad9f5149498412bb01645bc801\",\"vin\":\"YQF******00000052\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvg3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"13ec89e562d24bb7893b1b73a1e845cf\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"f53aeb1fd9264e13b828dd34519ed137\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"ce1255b83bee4e019b626494bcb5573e\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"d8814dd028bc4f5e85d5f937792fbfeb\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:02:40\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"8c722e6d106444ce9261c5b74f32cdbe\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:02:40\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"e43c1719a60d4ad2a67718b2c6428547\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 09:47:39\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"a85cd8949f8841ed8e595bf4ce8175a5\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 09:47:39\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"99efc6db7d914640924f4b846d528d03\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:55:13\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"3ae27a2c7028492eb649d9bf4549f7f7\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:55:13\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"3758c3b5ca714ef9be1e8dbbbc2692ad\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:52:02\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"c2c88ddf89fb41d3a9cf653349787e07\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:52:02\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}" +
        "]}";

    /// <summary>
    /// 2026-10-13 真实样例：重庆 500000（11）+ 黑龙江 230000（10）+ 上海 310000（8）。
    /// </summary>
    public const string ChongqingHeilongjiangShanghai1013Json =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"eventId\":\"636bfaa92e6a49dfaa04cad4eba42e96\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:11\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"cee5a44f849542d490d186e23936089a\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230600\",\"district\":\"230602\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:08\",\"longitude\":\"125.1111\",\"latitude\":\"46.5878\"}," +
        "{\"eventId\":\"22be9a64810b4fad8099e97a42ecd38f\",\"vin\":\"YQF******00000053\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvvzuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:06\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"ecc6048eea774048a633e867f8683756\",\"vin\":\"YQF******00000052\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvg3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:06\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"39881498750f4459acb69d96d100f389\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230900\",\"district\":\"230903\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"131.0033\",\"latitude\":\"45.7719\"}," +
        "{\"eventId\":\"932f51c8ac18466f9e23bce57ea7e8c5\",\"vin\":\"YQF******00000042\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBw3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"231000\",\"district\":\"231003\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"129.6339\",\"latitude\":\"44.5875\"}," +
        "{\"eventId\":\"5e8bf2fb30e544ddb61e4493c8adb2b7\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230300\",\"district\":\"230302\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"130.9517\",\"latitude\":\"45.2947\"}," +
        "{\"eventId\":\"5d94126aa5d24f5faa89f818b79db20b\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230600\",\"district\":\"230602\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"125.1111\",\"latitude\":\"46.5878\"}," +
        "{\"eventId\":\"3f811eb50ed14247a2be9103555e5792\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"8236eddf9a7b4ddc9120a0706e755135\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"a8bd20a8d3264b458f20765c54a02e23\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"248a1fd1c2ce4eb3b93b4615a9587962\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"aeba0cad9f5149498412bb01645bc801\",\"vin\":\"YQF******00000052\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvg3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"bec3c68c40124c71a7eab56b5e02f24e\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"82bcbb5af3a742feb3cf461bdca69ae2\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230500\",\"district\":\"230502\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"131.1527\",\"latitude\":\"46.6461\"}," +
        "{\"eventId\":\"0a78a742e93841f3a1ac1fb441a2ecde\",\"vin\":\"YQF******00000043\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB/zuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230100\",\"district\":\"230109\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"126.5349\",\"latitude\":\"45.8038\"}," +
        "{\"eventId\":\"13ec89e562d24bb7893b1b73a1e845cf\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"f53aeb1fd9264e13b828dd34519ed137\",\"vin\":\"YQF******00000050\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvmAVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"ce1255b83bee4e019b626494bcb5573e\",\"vin\":\"YQF******00000051\",\"vinEncrypt\":\"NaKWoX9U2FqQ8dF2B3oGvgQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"500000\",\"city\":\"500100\",\"district\":\"500103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:05\",\"longitude\":\"106.5516\",\"latitude\":\"29.5630\"}," +
        "{\"eventId\":\"0a446b0268d5490798a34d637864ea77\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:04\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"304e619ed90349f89f16872dbae883ad\",\"vin\":\"YQF******00000043\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB/zuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230100\",\"district\":\"230109\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:40:04\",\"longitude\":\"126.5349\",\"latitude\":\"45.8038\"}," +
        "{\"eventId\":\"d8814dd028bc4f5e85d5f937792fbfeb\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:02:40\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"8c722e6d106444ce9261c5b74f32cdbe\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 10:02:40\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"e43c1719a60d4ad2a67718b2c6428547\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 09:47:39\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"a85cd8949f8841ed8e595bf4ce8175a5\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 09:47:39\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"99efc6db7d914640924f4b846d528d03\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:55:13\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"3ae27a2c7028492eb649d9bf4549f7f7\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:55:13\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"3758c3b5ca714ef9be1e8dbbbc2692ad\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:52:02\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"c2c88ddf89fb41d3a9cf653349787e07\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-13 08:52:02\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}" +
        "]}";

    /// <summary>
    /// 2026-10-14 真实样例（仅国内）：黑龙江 230000（19）+ 天津 120000（1）+ 上海 310000（2）+ 浙江 330000（1）。
    /// 已丢弃截断尾条；国外 CENTRAL_EUROPE 等已忽略。
    /// </summary>
    public const string HeilongjiangTianjinShanghaiZhejiang1014Json =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":[" +
        "{\"eventId\":\"16982f37b7f64fb6ba20669fb8d7f1fd\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:54\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"6b457ade087e460b8ae49b55fe138c3f\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:53\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"ea31fa9dc468493c881fe3d5e741a70d\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:52\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"8136ef541d7d4d3a8275a2a358e4a6e7\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:51\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"66f4a521283743e79ed8fa904613c871\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:50\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"64d0836748904029aecaf2eb8f93492d\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:49\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"0ebcdbfc349245ddb39e1a6206714e86\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:48\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"67e32511684f4d718a707510ddc9974f\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:46\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"52e3fed02c4d4a7f88bc0cd1d0459e26\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:45\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"8326502f956247f385c464618015d14d\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:44\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"76967985bc90438baef61f46f94905c9\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230900\",\"district\":\"230903\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:41\",\"longitude\":\"131.0033\",\"latitude\":\"45.7719\"}," +
        "{\"eventId\":\"19763c150ef54b3ba9544bbb1d04cb94\",\"vin\":\"YQF******00000041\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBwQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230700\",\"district\":\"230702\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:40\",\"longitude\":\"128.9019\",\"latitude\":\"47.7272\"}," +
        "{\"eventId\":\"523a56892d6e467ba154630f51e7d1a3\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230500\",\"district\":\"230502\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:39\",\"longitude\":\"131.1527\",\"latitude\":\"46.6461\"}," +
        "{\"eventId\":\"1bc705a079904db1b3ff7b6e7b8602a9\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230300\",\"district\":\"230302\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:38\",\"longitude\":\"130.9517\",\"latitude\":\"45.2947\"}," +
        "{\"eventId\":\"f201eb729ddb497884f2a2b17c29e4c8\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230600\",\"district\":\"230602\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:37\",\"longitude\":\"125.1111\",\"latitude\":\"46.5878\"}," +
        "{\"eventId\":\"26202504bcc24529bc6b1cc98a812f77\",\"vin\":\"YQF******00000040\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB2AVrMjwzo6odkWG4xzMd5k=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230800\",\"district\":\"230803\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:36\",\"longitude\":\"130.3614\",\"latitude\":\"46.7963\"}," +
        "{\"eventId\":\"c15212733e894775aba1a83595d2aa11\",\"vin\":\"YQF******00000042\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhBw3ucRhO0ryAIXhEit6M82U=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"231000\",\"district\":\"231003\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:35\",\"longitude\":\"129.6339\",\"latitude\":\"44.5875\"}," +
        "{\"eventId\":\"6754bec3543549af9d6df6a62a29f653\",\"vin\":\"YQF******00000043\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB/zuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230100\",\"district\":\"230109\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:34\",\"longitude\":\"126.5349\",\"latitude\":\"45.8038\"}," +
        "{\"eventId\":\"a368f29ba6414de09bf6f5aaf094a396\",\"vin\":\"YQF******00000043\",\"vinEncrypt\":\"URUIhjoMRTZR3JE1Q3bhB/zuR6oo7+wSUHAkJqf2Ji8=\",\"eventLevel\":9,\"province\":\"230000\",\"city\":\"230100\",\"district\":\"230109\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 09:55:33\",\"longitude\":\"126.5349\",\"latitude\":\"45.8038\"}," +
        "{\"eventId\":\"e1e9ad6c03e647b897ee8ab36d5e0254\",\"vin\":\"YQF******00000011\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswQMdVAcu5wA2l4VgSIhRwQ=\",\"eventLevel\":9,\"province\":\"120000\",\"city\":\"120100\",\"district\":\"120103\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 08:55:14\",\"longitude\":\"117.2009\",\"latitude\":\"39.0842\"}," +
        "{\"eventId\":\"2fbdc34069dc4acab07747bbb92edf5a\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":9,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 08:55:14\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"3c551d4955eb44c2abcb3bd04c2432e7\",\"vin\":\"YQF******00000014\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYuswvtpp2ePlDTS5NPM6vGCAI=\",\"eventLevel\":10,\"province\":\"310000\",\"city\":\"310100\",\"district\":\"310101\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 08:55:14\",\"longitude\":\"121.4737\",\"latitude\":\"31.2304\"}," +
        "{\"eventId\":\"f37e7c59e110450ebda9d56a8be1cc88\",\"vin\":\"YQF******00000015\",\"vinEncrypt\":\"z/Zssy5El0jE+O5oiBYus5titGnQ3/+rCbKZpb33KVk=\",\"eventLevel\":9,\"province\":\"330000\",\"city\":\"330100\",\"district\":\"330105\",\"region\":null,\"country\":null,\"processTime\":\"2026-10-14 08:55:14\",\"longitude\":\"120.1551\",\"latitude\":\"30.2741\"}" +
        "]}";

    /// <summary>山东+黑龙江各 10 条，用于测试国家多省高亮与取第一条进省。</summary>
    public static string BuildMultiProvinceQualifiedJson()
    {
        List<string> items = new List<string>(24);
        AppendProvinceEventsFromCities(items, "370000", "山东", ShandongCities, 10, "VIN_SD_", distinctVin: true);
        AppendProvinceEventsFromCities(items, "230000", "黑龙江", HeilongjiangCities, 10, "VIN_HLJ_", distinctVin: true);
        return WrapResponse(items);
    }

    /// <summary>
    /// 多省达标且每省多辆「热车」：每辆 Vin 出现 &gt;3 次（即 ≥4），省总条数 ≥10。
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
                ("VIN_SD_HOT_02", 4),
                ("VIN_SD_HOT_03", 4),
            });

        AppendProvinceWithHotVins(
            items,
            "230000",
            "黑龙江",
            HeilongjiangCities,
            new (string Vin, int Count)[]
            {
                ("VIN_HLJ_HOT_01", 4),
                ("VIN_HLJ_HOT_02", 4),
                ("VIN_HLJ_HOT_03", 4),
                ("VIN_HLJ_HOT_04", 4),
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
                ("VIN_GD_HOT_03", 4),
            });

        AppendProvinceWithHotVins(
            items,
            "320000",
            "江苏",
            JiangsuCities,
            new (string Vin, int Count)[]
            {
                ("VIN_JS_HOT_01", 4),
                ("VIN_JS_HOT_02", 4),
                ("VIN_JS_HOT_03", 4),
            });

        return WrapResponse(items);
    }

    /// <summary>
    /// 东亚多国达标且每国多辆「热车」：每辆 Vin&gt;3（即 ≥4），国总条数≥10。
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
                ("VIN_JP_HOT_02", 4),
                ("VIN_JP_HOT_03", 4),
            },
            countryName: "日本");

        AppendProvinceWithHotVins(
            items,
            "410",
            "韩国",
            KoreaCities,
            new (string Vin, int Count)[]
            {
                ("VIN_KR_HOT_01", 4),
                ("VIN_KR_HOT_02", 4),
                ("VIN_KR_HOT_03", 4),
                ("VIN_KR_HOT_04", 4),
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
                ("VIN_MN_HOT_03", 4),
            },
            countryName: "蒙古");

        AppendProvinceWithHotVins(
            items,
            "408",
            "朝鲜",
            NorthKoreaCities,
            new (string Vin, int Count)[]
            {
                ("VIN_KP_HOT_01", 4),
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
        target.Add(BuildItemJson(eventId, vin, $"ENC_{vin}", provinceCode, provinceName, lon, lat, countryName));
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
                $"ENC_{vin}",
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
        string vinEncrypt,
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
            $"\"vinEncrypt\":\"{vinEncrypt}\"," +
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
