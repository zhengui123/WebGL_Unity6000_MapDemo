using System;

/// <summary>
/// 本地 HTTP 后端配置文件（StreamingAssets/HttpBackendConfig.json）反序列化结构。
/// </summary>
[Serializable]
public class HttpBackendConfigFile
{
    /// <summary>主机地址（IP:端口 或 域名:端口，不含协议）。</summary>
    public string apiHost;

    /// <summary>是否使用 HTTPS；false 为 HTTP。</summary>
    public bool useHttps;

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
