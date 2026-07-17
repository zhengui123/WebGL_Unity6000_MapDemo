/// <summary>
/// 车辆态势本地测试 JSON（跳过 HTTP，走响应成功逻辑）。
/// 防护状态示例与 Apifox 一致：首个 unprotectedParts.partTypeName = IDC。
/// </summary>
public static class CarVehicleDataMockJson
{
    /// <summary>零部件防护状态成功响应（含 IDC + 2 条风险异常事件）。</summary>
    public const string PartProtectionStatusSuccessJson =
        "{\n" +
        "  \"code\": 10000,\n" +
        "  \"msg\": \"操作成功！\",\n" +
        "  \"data\": {\n" +
        "    \"unprotectedParts\": [\n" +
        "      {\n" +
        "        \"partType\": 2,\n" +
        "        \"partTypeName\": \"IDC\",\n" +
        "        \"pendingEvents\": [\n" +
        "          {\n" +
        "            \"eventId\": \"123d336fs\",\n" +
        "            \"eventName\": \"风险异常事件\",\n" +
        "            \"processTime\": \"2026-06-30 12:41:23\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"123d336fs\",\n" +
        "            \"eventName\": \"风险异常事件\",\n" +
        "            \"processTime\": \"2026-06-30 12:41:23\"\n" +
        "          }\n" +
        "        ]\n" +
        "      }\n" +
        "    ],\n" +
        "    \"protectedParts\": [\n" +
        "      {\n" +
        "        \"partType\": 1,\n" +
        "        \"partTypeName\": \"CCU\",\n" +
        "        \"pendingEvents\": null\n" +
        "      },\n" +
        "      {\n" +
        "        \"partType\": 4,\n" +
        "        \"partTypeName\": \"TBOX\",\n" +
        "        \"pendingEvents\": null\n" +
        "      }\n" +
        "    ]\n" +
        "  }\n" +
        "}";

    /// <summary>攻击链路成功响应（最小可用 nodes/links）。</summary>
    public const string AttackChainSuccessJson =
        "{\n" +
        "  \"code\": 10000,\n" +
        "  \"msg\": \"操作成功！\",\n" +
        "  \"data\": {\n" +
        "    \"nodes\": [\n" +
        "      {\n" +
        "        \"id\": 1,\n" +
        "        \"partType\": \"2\",\n" +
        "        \"partTypeName\": \"IDC\",\n" +
        "        \"partsModel\": \"IDC-DEMO\",\n" +
        "        \"partsCode\": \"IDC001\",\n" +
        "        \"partsIp\": \"192.168.1.10\"\n" +
        "      },\n" +
        "      {\n" +
        "        \"id\": 2,\n" +
        "        \"partType\": \"1\",\n" +
        "        \"partTypeName\": \"CCU\",\n" +
        "        \"partsModel\": \"CCU-DEMO\",\n" +
        "        \"partsCode\": \"CCU001\",\n" +
        "        \"partsIp\": \"192.168.1.11\"\n" +
        "      }\n" +
        "    ],\n" +
        "    \"links\": [\n" +
        "      {\n" +
        "        \"partType\": \"2\",\n" +
        "        \"sourceIp\": \"10.0.0.1\",\n" +
        "        \"targetIp\": \"192.168.1.10\"\n" +
        "      }\n" +
        "    ]\n" +
        "  }\n" +
        "}";
}
