/// <summary>
/// 事件溯源详情本地测试 JSON（对齐 getSourceEventDetail 示例响应）。
/// </summary>
public static class SecurityEventDetailMockJson
{
    /// <summary>图中完整成功响应。</summary>
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
}
