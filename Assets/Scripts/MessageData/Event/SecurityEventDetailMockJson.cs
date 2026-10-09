/// <summary>
/// 事件溯源详情本地测试 JSON（对齐 getSourceEventDetail 示例响应）。
/// </summary>
public static class SecurityEventDetailMockJson
{
    /// <summary>国内省市区样例（完整成功响应）。</summary>
    public const string SuccessResponseJson =
        "{" +
        "\"code\":10000," +
        "\"msg\":\"操作成功！\"," +
        "\"data\":{" +
        "\"event_id\":\"123dfdsafffff\"," +
        "\"event_name\":\"风险异常事件\"," +
        "\"event_level\":\"7\"," +
        "\"happen_time\":\"2026-06-30 17:41:23\"," +
        "\"vin\":\"123***\"," +
        "\"risk_type\":1," +
        "\"risk_type_name\":\"车辆异常\"," +
        "\"risk_subtype\":1," +
        "\"risk_subtype_name\":\"离权指令命令执行\"," +
        "\"part_type\":null," +
        "\"part_type_name\":null," +
        "\"source_ip\":null," +
        "\"target_ip\":null," +
        "\"vehicle_brand_name\":null," +
        "\"vehicle_series_name\":null," +
        "\"vehicle_model_name\":null," +
        "\"message\":null," +
        "\"originalMap\":{" +
        "\"city\":\"330100\"," +
        "\"latitude\":\"30.215549\"," +
        "\"match_number\":0," +
        "\"province_name\":\"浙江省\"," +
        "\"district_name\":\"滨江区\"," +
        "\"city_name\":\"杭州市\"," +
        "\"province\":\"330000\"," +
        "\"district\":\"330108\"," +
        "\"longitude\":\"120.219191\"" +
        "}," +
        "\"record_data\":null," +
        "\"metri_tag_pk_id\":\"1233578650046\"," +
        "\"fieldDescMap\":{}," +
        "\"saasInnerEventType\":2" +
        "}" +
        "}";

    /// <summary>法国 / 运维监控样例（含 record_data，POST 成功返回）。</summary>
    public const string SuccessResponseTongjiJson =
        "{" +
        "\"code\":10000," +
        "\"msg\":\"操作成功！\"," +
        "\"data\":{" +
        "\"event_id\":\"98a91c495aa648a0aa03edbc290f5195\"," +
        "\"event_name\":\"test-tongji\"," +
        "\"event_level\":\"0\"," +
        "\"happen_time\":\"2026-09-24 13:58:04\"," +
        "\"vin\":\"YQF******00000093\"," +
        "\"risk_type\":3," +
        "\"risk_type_name\":\"运维监控\"," +
        "\"risk_subtype\":null," +
        "\"risk_subtype_name\":null," +
        "\"part_type\":2," +
        "\"part_type_name\":\"IDC\"," +
        "\"source_ip\":null," +
        "\"target_ip\":null," +
        "\"vehicle_brand_name\":null," +
        "\"vehicle_series_name\":null," +
        "\"vehicle_model_name\":null," +
        "\"message\":\"safsdfdsf\"," +
        "\"originalMap\":{" +
        "\"country\":\"250\"," +
        "\"latitude\":\"48.8566\"," +
        "\"process_time\":\"2026-09-24 14:02:00\"," +
        "\"country_name\":\"法国\"," +
        "\"longitude\":\"2.3522\"," +
        "\"model_id\":\"1461521428561920\"," +
        "\"region\":\"WESTERN_EUROPE\"" +
        "}," +
        "\"record_data\":\"{\\\"cmd\\\":10,\\\"data\\\":\\\"eyJ2aW4iOiJJTlRFU1RURVNURzMwMTM4OSIsImFjZCI6MiwiYXR0dHAiOjIwMDMsImF0dHRwX3R5cGUiOjAsImRpc2MiOiJJRFBTIGFsZXJ0IiwidGltZSI6MTcxNTkzNjM1NjE3NiwicmVzIjoyLCJyZXBlYXQiOltdLCJkZXQiOnsiaWRwc21kIjoxLCJzcmNpcCI6IjE5Mi4xNjguNS4yMDAiLCJzcmNwbyI6IjE1MzMiLCJkZXNpcCI6IjE5Mi4xNjguNS4yMDAiLCJkZXNwbyI6IjgwIiwiZGV0IjoiVENQX0FDS19QT1JUX1NDQU4ifSwibWNkIjoiZzMifQ==\\\",\\\"challenge\\\":\\\"0f7b4d91e2a6c308\\\",\\\"vin\\\":\\\"YQFT2026000000091\\\",\\\"longitude\\\":\\\"2.3522\\\",\\\"latitude\\\":\\\"48.8566\\\",\\\"ids_version\\\":\\\"2.4.5\\\",\\\"part_type\\\":\\\"4\\\",\\\"source_ip\\\":\\\"192.168.0.202\\\",\\\"target_ip\\\":\\\"192.168.6.203\\\"}\"," +
        "\"metri_tag_pk_id\":\"2103000996096303104\"," +
        "\"fieldDescMap\":{}," +
        "\"saasInnerEventType\":2" +
        "}" +
        "}";
}
