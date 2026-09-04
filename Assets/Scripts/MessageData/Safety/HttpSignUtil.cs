using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// HTTP 请求安全签名（对齐前端 signUtil.js 的 buildSignHeaders）。
/// 签名串：method\npathname\nquery\nbodySha256\ntimestamp\nnonce\ntoken\ntenantId
/// </summary>
public static class HttpSignUtil
{
    public const string HeaderTimestamp = "X-Timestamp";
    public const string HeaderNonce = "X-Nonce";
    public const string HeaderSign = "X-Sign";
    public const string HeaderToken = "Satoken";
    public const string HeaderTenantId = "X-Tenant-Id";

    /// <summary>
    /// 向请求头字典写入签名三件套；密钥为空时跳过并告警。
    /// bodyJson 必须与实际发出的请求体字符串一致（无 body 传空串）。
    /// </summary>
    public static void MergeSignHeaders(
        Dictionary<string, string> headers,
        string method,
        string url,
        string bodyJson)
    {
        if (headers == null)
        {
            return;
        }

        string appSecret = HttpProjectConfig.AppSecret;
        if (string.IsNullOrEmpty(appSecret))
        {
            Debug.LogWarning("[HttpSignUtil] appSecret 为空，跳过签名（请在 HttpBackendConfig.json 配置）。");
            return;
        }

        string token = GetHeaderValue(headers, HeaderToken);
        string tenantId = GetHeaderValue(headers, HeaderTenantId);
        Dictionary<string, string> signHeaders = BuildSignHeaders(
            method,
            url,
            bodyJson,
            token,
            tenantId,
            appSecret);

        headers[HeaderTimestamp] = signHeaders[HeaderTimestamp];
        headers[HeaderNonce] = signHeaders[HeaderNonce];
        headers[HeaderSign] = signHeaders[HeaderSign];
    }

    /// <summary>构建 X-Timestamp / X-Nonce / X-Sign（与 signUtil.js 一致，不做 /api 前缀裁剪）。</summary>
    public static Dictionary<string, string> BuildSignHeaders(
        string method,
        string url,
        string bodyJson,
        string token,
        string tenantId,
        string appSecret)
    {
        string normalizedMethod = string.IsNullOrEmpty(method) ? "get" : method.Trim().ToLowerInvariant();
        string body = bodyJson ?? string.Empty;
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string nonce = Guid.NewGuid().ToString();

        TryParseUrlParts(url, out string pathname, out string rawQuery);

        string signString = string.Join(
            "\n",
            normalizedMethod,
            pathname,
            rawQuery,
            Sha256Hex(body),
            timestamp,
            nonce,
            token ?? string.Empty,
            tenantId ?? string.Empty);

        return new Dictionary<string, string>
        {
            { HeaderTimestamp, timestamp },
            { HeaderNonce, nonce },
            { HeaderSign, HmacSha256Hex(signString, appSecret ?? string.Empty) },
        };
    }

    public static string Sha256Hex(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data ?? string.Empty);
        using (SHA256 sha = SHA256.Create())
        {
            return ToHex(sha.ComputeHash(bytes));
        }
    }

    public static string HmacSha256Hex(string message, string secret)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
        using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
        {
            return ToHex(hmac.ComputeHash(messageBytes));
        }
    }

    private static void TryParseUrlParts(string url, out string pathname, out string rawQuery)
    {
        pathname = "/";
        rawQuery = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            // 相对路径兜底：尽量取 path?query
            int queryIndex = url.IndexOf('?');
            if (queryIndex >= 0)
            {
                pathname = NormalizePath(url.Substring(0, queryIndex));
                rawQuery = url.Substring(queryIndex + 1);
            }
            else
            {
                pathname = NormalizePath(url);
            }

            return;
        }

        pathname = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        rawQuery = uri.Query.Length > 1 ? uri.Query.Substring(1) : string.Empty;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith("/") ? path : "/" + path;
    }

    private static string GetHeaderValue(Dictionary<string, string> headers, string key)
    {
        if (headers == null || string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (headers.TryGetValue(key, out string value))
        {
            return value ?? string.Empty;
        }

        foreach (KeyValuePair<string, string> pair in headers)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ToHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
