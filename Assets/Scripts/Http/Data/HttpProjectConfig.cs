using System.Collections.Generic;

/// <summary>
/// 项目通用 HTTP 配置：接口主机、默认请求头、常用路径。
/// 连接信息优先读取 StreamingAssets/<see cref="HttpBackendConfigLoader.ConfigFileName"/>，否则使用程序默认。
/// </summary>
public static class HttpProjectConfig
{
    private static HttpBackendResolvedConfig Backend => HttpBackendConfigLoader.Resolved;

    /// <summary>业务接口主机（IP:端口，不含协议）。</summary>
    public static string ApiHost => Backend.ApiHost;

    /// <summary>是否使用 HTTPS。</summary>
    public static bool UseHttps => Backend.UseHttps;

    /// <summary>URL 协议（http / https）。</summary>
    public static string ApiScheme => Backend.ApiScheme;

    /// <summary>综合态势查询默认开始时间（空表示不限）。</summary>
    public const string DefaultQueryStartTime = "";

    /// <summary>综合态势查询默认结束时间。</summary>
    public const string DefaultQueryEndTime = "2026-06-30 23:00:00";

    /// <summary>综合态势接口成功响应码。</summary>
    public const int SuccessResponseCode = 10000;

    public const string WorkOrderDisposalOverviewPath =
        "/business/bigScreen/comprehensivePosture/workOrderDisposalOverview";

    public const string LatestVinLocationPath =
        "/business/bigScreen/comprehensivePosture/latestVinLocation";

    /// <summary>HTTP 车辆点位默认同步目标板块（与场景 sd_map 根节点名称一致）。</summary>
    public const string DefaultPlateMapName = "sd_map";

    /// <summary>默认请求头键值对（只读，供 UI 初始化）。</summary>
    public static IReadOnlyList<(string Key, string Value)> DefaultHeaders => Backend.HeaderEntries;

    /// <summary>创建默认请求头字典副本（每次调用独立实例，避免被修改污染配置）。</summary>
    public static Dictionary<string, string> CreateDefaultHeaders()
    {
        IReadOnlyList<(string Key, string Value)> entries = Backend.HeaderEntries;
        Dictionary<string, string> headers = new Dictionary<string, string>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            headers[entries[i].Key] = entries[i].Value;
        }

        return headers;
    }

    /// <summary>
    /// 将项目默认请求头合并进目标字典：默认项打底，<paramref name="additionalHeaders"/> 中非空键可覆盖。
    /// </summary>
    public static Dictionary<string, string> MergeDefaultHeaders(Dictionary<string, string> additionalHeaders = null)
    {
        Dictionary<string, string> merged = CreateDefaultHeaders();
        if (additionalHeaders == null)
        {
            return merged;
        }

        foreach (KeyValuePair<string, string> header in additionalHeaders)
        {
            if (string.IsNullOrEmpty(header.Key))
            {
                continue;
            }

            merged[header.Key] = header.Value ?? string.Empty;
        }

        return merged;
    }

    /// <summary>根据相对路径构建完整 API URL。</summary>
    public static string BuildApiUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }
        else if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        string host = ApiHost;
        if (host.Contains("://"))
        {
            return $"{host}{path}";
        }

        return $"{ApiScheme}://{host}{path}";
    }

    /// <summary>重新加载 StreamingAssets 中的 HTTP 后端配置文件。</summary>
    public static void ReloadBackendConfig()
    {
        HttpBackendConfigLoader.Reload();
    }
}
