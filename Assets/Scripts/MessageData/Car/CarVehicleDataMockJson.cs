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

    /// <summary>2026-10-12 实网样例：四件均未防护，protectedParts 为空。</summary>
    public const string PartProtectionStatusSuccessJson1012 =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":{\"unprotectedParts\":[" +
        "{\"partType\":1,\"partTypeName\":\"CCU666\",\"pendingEvents\":[" +
        "{\"eventId\":\"5f3c2ca3cabe4a339309aed5e770d758\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:49\"}," +
        "{\"eventId\":\"a3f44f6cd7934a95910e7f99e7b01b3e\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:48\"}," +
        "{\"eventId\":\"2dc034a3bc5b4012b5887de4f110a6d8\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:47\"}," +
        "{\"eventId\":\"0c559542afb34c4fb5fa5faa3f176ba5\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:46\"}," +
        "{\"eventId\":\"1dc471b316304eb688f1ed8b41e24427\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:37\"}" +
        "]}," +
        "{\"partType\":3,\"partTypeName\":\"ADC666\",\"pendingEvents\":[" +
        "{\"eventId\":\"79555a40123c40478d6aeb24f8686b79\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:40\"}," +
        "{\"eventId\":\"ca87b2ee47004b5bb93e9dad3e2fb7a7\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:43:22\"}," +
        "{\"eventId\":\"aa8d430877094b6497b0720e42eef824\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 13:57:57\"}," +
        "{\"eventId\":\"b2a62ffd9fe943af8be4e1db2c79b4be\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 11:30:59\"}," +
        "{\"eventId\":\"4724df8a86304591b215af9adae9a6e1\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 11:20:06\"}" +
        "]}," +
        "{\"partType\":2,\"partTypeName\":\"IDC666\",\"pendingEvents\":[" +
        "{\"eventId\":\"d8d2e2fbbe6b42dcbfde01909c75d938\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:38\"}," +
        "{\"eventId\":\"f0d555b16ba0454ba750f6ea133df7a6\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:43:20\"}," +
        "{\"eventId\":\"6686df948b244fdd851f9ff3682ec07e\",\"eventName\":\"test-tongji\",\"processTime\":\"2026-10-12 13:58:00\"}," +
        "{\"eventId\":\"0669f872f6ef451fa799bbf895f71060\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 13:58:00\"}," +
        "{\"eventId\":\"90d5cc7a92d048ab96c0d9d3d9aa152e\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 13:57:57\"}" +
        "]}," +
        "{\"partType\":4,\"partTypeName\":\"TBOX666\",\"pendingEvents\":[" +
        "{\"eventId\":\"7bd45d9464df4700b411e6e857bab109\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:52:39\"}," +
        "{\"eventId\":\"049e917f6db540c288ef45caf91e5f21\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 14:43:21\"}," +
        "{\"eventId\":\"af4295a1511f4f929a5127112e792fe9\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 13:57:57\"}," +
        "{\"eventId\":\"02a37843a700490594b3c654605ff288\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 11:30:59\"}," +
        "{\"eventId\":\"9c5bd5eaeea04d1f814bdef13fc82a60\",\"eventName\":\"规则碰撞的安全高危威胁警示\",\"processTime\":\"2026-10-12 11:20:09\"}" +
        "]}],\"protectedParts\":[]}}";

    /// <summary>2026-10-12 实网样例：CCU/IDC/ADC/TBOX；TBOX IP 已按 192.168.0.202 修正以便 links 对照。</summary>
    public const string AttackChainSuccessJson1012 =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":{\"nodes\":[" +
        "{\"id\":\"1466356575215616\",\"partType\":\"1\",\"partTypeName\":\"CCU666\",\"partsModel\":\"CCU001\",\"partsCode\":null,\"partsIp\":\"192.168.5.200\"}," +
        "{\"id\":\"1466490932486144\",\"partType\":\"2\",\"partTypeName\":\"IDC666\",\"partsModel\":\"IDC-01\",\"partsCode\":null,\"partsIp\":\"192.168.0.201\"}," +
        "{\"id\":\"1433366079176704\",\"partType\":\"3\",\"partTypeName\":\"AD666C\",\"partsModel\":\"x1000\",\"partsCode\":null,\"partsIp\":\"192.168.6.203\"}," +
        "{\"id\":\"1466264241029120\",\"partType\":\"4\",\"partTypeName\":\"TBOX666\",\"partsModel\":\"TBOX001\",\"partsCode\":null,\"partsIp\":\"192.168.0.202\"}" +
        "],\"links\":[" +
        "{\"partType\":\"1\",\"sourceIp\":\"192.168.5.200\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"4\",\"sourceIp\":\"192.168.0.202\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"2\",\"sourceIp\":\"192.168.0.202\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"2\",\"sourceIp\":\"192.168.0.201\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"3\",\"sourceIp\":\"192.168.6.203\",\"targetIp\":\"192.168.0.201\"}" +
        "]}}";

    /// <summary>
    /// 含 IVI_CANIDS(type=5) 样例：防护状态与下方攻击链路 nodes 名称一致。
    /// </summary>
    public const string PartProtectionStatusSuccessJsonWithIvi =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":{\"unprotectedParts\":[" +
        "{\"partType\":2,\"partTypeName\":\"IDC\",\"pendingEvents\":[" +
        "{\"eventId\":\"ivi-idc-evt-01\",\"eventName\":\"IDC 通信异常\",\"processTime\":\"2026-10-13 15:00:01\"}," +
        "{\"eventId\":\"ivi-idc-evt-02\",\"eventName\":\"IDC 未授权访问\",\"processTime\":\"2026-10-13 15:00:12\"}" +
        "]}," +
        "{\"partType\":5,\"partTypeName\":\"IVI_CANIDS\",\"pendingEvents\":[" +
        "{\"eventId\":\"ivi-can-evt-01\",\"eventName\":\"IVI CANIDS 告警\",\"processTime\":\"2026-10-13 15:01:01\"}," +
        "{\"eventId\":\"ivi-can-evt-02\",\"eventName\":\"IVI CANIDS 策略冲突\",\"processTime\":\"2026-10-13 15:01:22\"}" +
        "]}],\"protectedParts\":[" +
        "{\"partType\":1,\"partTypeName\":\"CCU\",\"pendingEvents\":[" +
        "{\"eventId\":\"ivi-ccu-evt-01\",\"eventName\":\"CCU 配置变更提醒\",\"processTime\":\"2026-10-13 14:40:00\"}" +
        "]}," +
        "{\"partType\":3,\"partTypeName\":\"ADC\",\"pendingEvents\":[" +
        "{\"eventId\":\"ivi-adc-evt-01\",\"eventName\":\"ADC 感知数据延迟\",\"processTime\":\"2026-10-13 14:41:10\"}" +
        "]}," +
        "{\"partType\":4,\"partTypeName\":\"TBOX\",\"pendingEvents\":[" +
        "{\"eventId\":\"ivi-tbox-evt-01\",\"eventName\":\"TBOX 蜂窝信号波动\",\"processTime\":\"2026-10-13 14:42:20\"}" +
        "]}]}}";

    /// <summary>含 IVI_CANIDS 的攻击链路实网样例（5 节点 + 5 links）。</summary>
    public const string AttackChainSuccessJsonWithIvi =
        "{\"code\":10000,\"msg\":\"操作成功！\",\"data\":{\"nodes\":[" +
        "{\"id\":\"1466356575215616\",\"partType\":\"1\",\"partTypeName\":\"CCU\",\"partsModel\":\"CCU001\",\"partsCode\":null,\"partsIp\":\"192.168.5.200\"}," +
        "{\"id\":\"1466490932486144\",\"partType\":\"2\",\"partTypeName\":\"IDC\",\"partsModel\":\"IDC-01\",\"partsCode\":null,\"partsIp\":\"192.168.0.201\"}," +
        "{\"id\":\"1433366079176704\",\"partType\":\"3\",\"partTypeName\":\"ADC\",\"partsModel\":\"x1000\",\"partsCode\":null,\"partsIp\":\"192.168.6.203\"}," +
        "{\"id\":\"1466264241029120\",\"partType\":\"4\",\"partTypeName\":\"TBOX\",\"partsModel\":\"TBOX001\",\"partsCode\":null,\"partsIp\":\"192.168.0.202\"}," +
        "{\"id\":\"1467909405261824\",\"partType\":\"5\",\"partTypeName\":\"IVI_CANIDS\",\"partsModel\":\"IVI001\",\"partsCode\":null,\"partsIp\":\"192.168.0.205\"}" +
        "],\"links\":[" +
        "{\"partType\":\"2\",\"sourceIp\":\"192.168.0.201\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"3\",\"sourceIp\":\"192.168.6.203\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"1\",\"sourceIp\":\"192.168.5.200\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"4\",\"sourceIp\":\"192.168.0.202\",\"targetIp\":\"192.168.0.201\"}," +
        "{\"partType\":\"2\",\"sourceIp\":\"192.168.0.202\",\"targetIp\":\"192.168.0.201\"}" +
        "]}}";
}
