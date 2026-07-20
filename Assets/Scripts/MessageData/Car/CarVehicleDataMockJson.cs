/// <summary>
/// 车辆态势本地测试 JSON（跳过 HTTP，走响应成功逻辑）。
/// IDC 在 unprotectedParts；CCU/TBOX 在 protectedParts；每件多条且 eventId/eventName 互不相同。
/// </summary>
public static class CarVehicleDataMockJson
{
    /// <summary>零部件防护状态成功响应。</summary>
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
        "            \"eventId\": \"idc-evt-01\",\n" +
        "            \"eventName\": \"IDC 通信异常\",\n" +
        "            \"processTime\": \"2026-06-30 12:41:23\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"idc-evt-02\",\n" +
        "            \"eventName\": \"IDC 固件校验失败\",\n" +
        "            \"processTime\": \"2026-06-30 12:45:10\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"idc-evt-03\",\n" +
        "            \"eventName\": \"IDC 未授权访问\",\n" +
        "            \"processTime\": \"2026-06-30 12:50:01\"\n" +
        "          }\n" +
        "        ]\n" +
        "      }\n" +
        "    ],\n" +
        "    \"protectedParts\": [\n" +
        "      {\n" +
        "        \"partType\": 1,\n" +
        "        \"partTypeName\": \"CCU\",\n" +
        "        \"pendingEvents\": [\n" +
        "          {\n" +
        "            \"eventId\": \"ccu-evt-01\",\n" +
        "            \"eventName\": \"CCU 配置变更提醒\",\n" +
        "            \"processTime\": \"2026-06-30 11:10:00\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"ccu-evt-02\",\n" +
        "            \"eventName\": \"CCU 证书即将过期\",\n" +
        "            \"processTime\": \"2026-06-30 11:22:33\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"ccu-evt-03\",\n" +
        "            \"eventName\": \"CCU 策略同步延迟\",\n" +
        "            \"processTime\": \"2026-06-30 11:30:45\"\n" +
        "          }\n" +
        "        ]\n" +
        "      },\n" +
        "      {\n" +
        "        \"partType\": 4,\n" +
        "        \"partTypeName\": \"TBOX\",\n" +
        "        \"pendingEvents\": [\n" +
        "          {\n" +
        "            \"eventId\": \"tbox-evt-01\",\n" +
        "            \"eventName\": \"TBOX 蜂窝信号波动\",\n" +
        "            \"processTime\": \"2026-06-30 10:05:12\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"tbox-evt-02\",\n" +
        "            \"eventName\": \"TBOX 远程诊断待确认\",\n" +
        "            \"processTime\": \"2026-06-30 10:18:40\"\n" +
        "          },\n" +
        "          {\n" +
        "            \"eventId\": \"tbox-evt-03\",\n" +
        "            \"eventName\": \"TBOX OTA 任务排队\",\n" +
        "            \"processTime\": \"2026-06-30 10:25:55\"\n" +
        "          }\n" +
        "        ]\n" +
        "      }\n" +
        "    ]\n" +
        "  }\n" +
        "}";

    /// <summary>攻击链路成功响应（nodes IP 对照 + 多条 links）。</summary>
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
        "      },\n" +
        "      {\n" +
        "        \"id\": 3,\n" +
        "        \"partType\": \"4\",\n" +
        "        \"partTypeName\": \"TBOX\",\n" +
        "        \"partsModel\": \"TBOX-DEMO\",\n" +
        "        \"partsCode\": \"TBOX001\",\n" +
        "        \"partsIp\": \"192.168.1.12\"\n" +
        "      }\n" +
        "    ],\n" +
        "    \"links\": [\n" +
        "      {\n" +
        "        \"partType\": \"4\",\n" +
        "        \"sourceIp\": \"192.168.1.12\",\n" +
        "        \"targetIp\": \"192.168.1.10\"\n" +
        "      },\n" +
        "      {\n" +
        "        \"partType\": \"2\",\n" +
        "        \"sourceIp\": \"192.168.1.10\",\n" +
        "        \"targetIp\": \"192.168.1.11\"\n" +
        "      }\n" +
        "    ]\n" +
        "  }\n" +
        "}";
}
