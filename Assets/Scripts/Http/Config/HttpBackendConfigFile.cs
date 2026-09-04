using System;

/// <summary>
/// 本地 HTTP 后端配置文件（StreamingAssets/HttpBackendConfig.json）反序列化结构。
/// </summary>
[Serializable]
public class HttpBackendConfigFile
{
    /// <summary>主机地址（IP:端口 或 域名:端口，不含协议）。</summary>
    public string apiHost;

    /// <summary>是否使用 HTTPS。</summary>
    public bool useHttps;

    /// <summary>
    /// 是否跳过 HTTPS 证书校验（仅开发/内网调试）。
    /// 用于自签名证书、用 IP 访问但证书 CN 为域名等场景。
    /// </summary>
    public bool skipSslCertificateValidation;

    /// <summary>HTTPS 测试环境主机（域名:端口，不含协议）。</summary>
    public string httpsTestApiHost;

    /// <summary>请求签名密钥（与前端 APP_SECRET 一致）。</summary>
    public string appSecret;

    /// <summary>默认请求头列表。</summary>
    public HttpBackendHeaderEntry[] headers;
}

/// <summary>请求头键值对（与配置文件 JSON 字段一致）。</summary>
[Serializable]
public class HttpBackendHeaderEntry
{
    public string key;
    public string value;
}
