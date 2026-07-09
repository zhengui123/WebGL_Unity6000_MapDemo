using System.Collections.Generic;



/// <summary>

/// 项目通用 HTTP 配置：接口主机、默认请求头、常用路径。

/// 连接信息优先读取 StreamingAssets/<see cref="HttpBackendConfigLoader.ConfigFileName"/>，否则使用程序默认。

/// </summary>

public static class HttpProjectConfig

{

    private static HttpBackendResolvedConfig Backend => HttpBackendConfigLoader.Resolved;



    /// <summary>内网业务接口主机（IP:端口，不含协议）。</summary>

    public static string ApiHost => Backend.ApiHost;



    /// <summary>HTTPS 测试环境主机（域名:端口，不含协议）。</summary>

    public static string HttpsTestApiHost => Backend.HttpsTestApiHost;



    /// <summary>内网是否使用 HTTPS。</summary>

    public static bool UseHttps => Backend.UseHttps;



    /// <summary>内网 URL 协议（http / https）。</summary>

    public static string ApiScheme => Backend.ApiScheme;



    /// <summary>是否跳过 HTTPS 证书校验（仅开发内网，生产务必 false）。</summary>

    public static bool SkipSslCertificateValidation => Backend.SkipSslCertificateValidation;



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



    /// <summary>HTTPS 测试环境告警详情接口路径。</summary>

    public const string SecurityEventDetailPath = "/api/business/event/getSecurityEventDetail";



    /// <summary>HTTP 车辆点位默认同步目标板块（与场景 sd_map 根节点名称一致）。</summary>

    public const string DefaultPlateMapName = "sd_map";



    /// <summary>默认请求头键值对（只读，供 UI 初始化）。</summary>

    public static IReadOnlyList<(string Key, string Value)> DefaultHeaders => Backend.HeaderEntries;



    /// <summary>HTTPS 测试环境 getSecurityEventDetail 完整 URL。</summary>

    public static string DefaultHttpsTestSecurityEventDetailUrl => BuildHttpsTestApiUrl(SecurityEventDetailPath);



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



    /// <summary>根据内网配置构建完整 API URL（HTTP 业务接口）。</summary>

    public static string BuildApiUrl(string path)

    {

        return BuildUrlForHost(ApiHost, path, forceScheme: ApiScheme);

    }



    /// <summary>根据 HTTPS 测试主机构建完整 API URL。</summary>

    public static string BuildHttpsTestApiUrl(string path)

    {

        return BuildUrlForHost(HttpsTestApiHost, path, forceScheme: "https");

    }



    /// <summary>根据主机与路径拼接 URL；自动识别 HTTPS 测试主机。</summary>

    public static string BuildUrlForHost(string host, string path, string forceScheme = null)

    {

        string normalizedPath = NormalizePath(path);

        string trimmedHost = host != null ? host.Trim() : string.Empty;

        if (string.IsNullOrEmpty(trimmedHost))

        {

            return normalizedPath;

        }



        if (trimmedHost.Contains("://"))

        {

            return $"{trimmedHost.TrimEnd('/')}{normalizedPath}";

        }



        string scheme = !string.IsNullOrEmpty(forceScheme) ? forceScheme : ResolveUrlScheme(trimmedHost);

        return $"{scheme}://{trimmedHost}{normalizedPath}";

    }



    /// <summary>根据主机判断应使用的 URL 协议。</summary>

    public static string ResolveUrlScheme(string host)

    {

        if (string.IsNullOrWhiteSpace(host))

        {

            return ApiScheme;

        }



        string trimmedHost = host.Trim();

        if (trimmedHost.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))

        {

            return "https";

        }



        if (trimmedHost.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))

        {

            return "http";

        }



        if (IsHttpsTestHost(trimmedHost))

        {

            return "https";

        }



        return ApiScheme;

    }



    /// <summary>去掉主机字符串中的协议前缀。</summary>

    public static string StripUrlScheme(string host)

    {

        if (string.IsNullOrWhiteSpace(host))

        {

            return string.Empty;

        }



        string trimmedHost = host.Trim();

        if (trimmedHost.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))

        {

            return trimmedHost.Substring("https://".Length);

        }



        if (trimmedHost.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))

        {

            return trimmedHost.Substring("http://".Length);

        }



        return trimmedHost;

    }



    /// <summary>重新加载 StreamingAssets 中的 HTTP 后端配置文件。</summary>

    public static void ReloadBackendConfig()

    {

        HttpBackendConfigLoader.Reload();

    }



    private static bool IsHttpsTestHost(string host)

    {

        string normalizedHost = StripUrlScheme(host);

        int slashIndex = normalizedHost.IndexOf('/');

        if (slashIndex >= 0)

        {

            normalizedHost = normalizedHost.Substring(0, slashIndex);

        }



        string testHost = HttpsTestApiHost;

        if (string.IsNullOrWhiteSpace(testHost))

        {

            return false;

        }



        return string.Equals(normalizedHost, testHost.Trim(), System.StringComparison.OrdinalIgnoreCase);

    }



    private static string NormalizePath(string path)

    {

        if (string.IsNullOrWhiteSpace(path))

        {

            return "/";

        }



        return path.StartsWith("/") ? path : "/" + path;

    }

}


