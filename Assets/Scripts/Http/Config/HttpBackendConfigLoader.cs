using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 从 StreamingAssets 加载 HTTP 后端配置；无文件或解析失败时使用程序内置默认值。
/// </summary>
public static class HttpBackendConfigLoader
{
    public const string ConfigFileName = "HttpBackendConfig.json";

    private static HttpBackendResolvedConfig _cached;

    /// <summary>当前生效的配置（优先本地文件，否则程序默认）。</summary>
    public static HttpBackendResolvedConfig Resolved => _cached ??= Load();

    /// <summary>强制重新读取本地配置文件（修改 JSON 后可在运行时调用）。</summary>
    public static void Reload()
    {
        _cached = Load();
    }

    private static HttpBackendResolvedConfig Load()
    {
        HttpBackendResolvedConfig defaults = HttpBackendResolvedConfig.CreateProgramDefaults();

        if (!TryReadConfigJson(out string json))
        {
            Debug.Log($"[HttpBackendConfig] 未找到本地配置，使用程序默认：{defaults.ApiScheme}://{defaults.ApiHost}");
            return defaults;
        }

        if (!HttpJsonParser.TryParse(json, out HttpBackendConfigFile file, out string parseError))
        {
            Debug.LogWarning($"[HttpBackendConfig] 配置解析失败，使用程序默认：{parseError}");
            return defaults;
        }

        HttpBackendResolvedConfig resolved = MergeFileIntoDefaults(file, defaults);
        Debug.Log($"[HttpBackendConfig] 已加载本地配置：{resolved.ApiScheme}://{resolved.ApiHost}，请求头 {resolved.HeaderEntries.Count} 项");
        return resolved;
    }

    private static bool TryReadConfigJson(out string json)
    {
        json = null;
        string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            json = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[HttpBackendConfig] 读取配置失败：{path}\n{exception.Message}");
            return false;
        }
    }

    private static HttpBackendResolvedConfig MergeFileIntoDefaults(
        HttpBackendConfigFile file,
        HttpBackendResolvedConfig defaults)
    {
        string apiHost = string.IsNullOrWhiteSpace(file.apiHost) ? defaults.ApiHost : file.apiHost.Trim();
        bool useHttps = file.useHttps;
        bool skipSsl = file.skipSslCertificateValidation;
        string httpsTestHost = string.IsNullOrWhiteSpace(file.httpsTestApiHost)
            ? defaults.HttpsTestApiHost
            : file.httpsTestApiHost.Trim();
        string appSecret = string.IsNullOrWhiteSpace(file.appSecret)
            ? defaults.AppSecret
            : file.appSecret.Trim();
        List<(string Key, string Value)> headers = BuildHeaders(file.headers, defaults.HeaderEntries);

        return new HttpBackendResolvedConfig(
            apiHost,
            useHttps,
            skipSsl,
            httpsTestHost,
            appSecret,
            headers,
            loadedFromFile: true);
    }

    private static List<(string Key, string Value)> BuildHeaders(
        HttpBackendHeaderEntry[] fileHeaders,
        IReadOnlyList<(string Key, string Value)> defaultHeaders)
    {
        if (fileHeaders == null || fileHeaders.Length == 0)
        {
            return CopyHeaderList(defaultHeaders);
        }

        List<(string Key, string Value)> headers = new List<(string Key, string Value)>(fileHeaders.Length);
        for (int i = 0; i < fileHeaders.Length; i++)
        {
            HttpBackendHeaderEntry entry = fileHeaders[i];
            if (entry == null)
            {
                continue;
            }

            string key = entry.key != null ? entry.key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            headers.Add((key, entry.value ?? string.Empty));
        }

        return headers.Count > 0 ? headers : CopyHeaderList(defaultHeaders);
    }

    private static List<(string Key, string Value)> CopyHeaderList(
        IReadOnlyList<(string Key, string Value)> source)
    {
        List<(string Key, string Value)> copy = new List<(string Key, string Value)>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            copy.Add(source[i]);
        }

        return copy;
    }
}

/// <summary>合并后的 HTTP 后端连接配置快照。</summary>
public sealed class HttpBackendResolvedConfig
{
    public string ApiHost { get; }
    public bool UseHttps { get; }
    public bool SkipSslCertificateValidation { get; }
    public string HttpsTestApiHost { get; }
    public string AppSecret { get; }
    public string ApiScheme => UseHttps ? "https" : "http";
    public IReadOnlyList<(string Key, string Value)> HeaderEntries { get; }
    public bool LoadedFromFile { get; }

    public HttpBackendResolvedConfig(
        string apiHost,
        bool useHttps,
        bool skipSslCertificateValidation,
        string httpsTestApiHost,
        string appSecret,
        List<(string Key, string Value)> headerEntries,
        bool loadedFromFile)
    {
        ApiHost = apiHost;
        UseHttps = useHttps;
        SkipSslCertificateValidation = skipSslCertificateValidation;
        HttpsTestApiHost = httpsTestApiHost;
        AppSecret = appSecret ?? string.Empty;
        HeaderEntries = headerEntries;
        LoadedFromFile = loadedFromFile;
    }

    /// <summary>程序内置默认（无本地配置文件时使用）。</summary>
    public static HttpBackendResolvedConfig CreateProgramDefaults()
    {
        return new HttpBackendResolvedConfig(
            DefaultApiHost,
            DefaultUseHttps,
            DefaultSkipSslCertificateValidation,
            DefaultHttpsTestApiHost,
            DefaultAppSecret,
            new List<(string Key, string Value)>(DefaultHeaderEntries),
            loadedFromFile: false);
    }

    public const string DefaultApiHost = "metritag.vsoc.saas.test.press.com:10777";//"10.60.16.96:38000";
    public const bool DefaultUseHttps = false;
    public const bool DefaultSkipSslCertificateValidation = true;
    public const string DefaultHttpsTestApiHost = "metritag.vsoc.saas.test.press.com:10778";
    /// <summary>与前端 signUtil.js 中 APP_SECRET 默认一致。</summary>
    public const string DefaultAppSecret = "c9eyidzx6tn0us0kzw69w8injlxbq9";

    private static readonly (string Key, string Value)[] DefaultHeaderEntries =
    {
        ("Satoken", "hD0BPUQN0MnpZ0D1ol36I7pElepRU7JBXmv46Pj9ij3OIL1aPZ561QAqiK66wu4L"),
        ("X-Tenant-Id", "1"),
        ("Sys-Lang", "zh-CN"),
    };
}
