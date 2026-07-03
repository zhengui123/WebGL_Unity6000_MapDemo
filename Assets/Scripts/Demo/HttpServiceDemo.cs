using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 演示 <see cref="HttpService"/> GET / POST 请求（Inspector 可配置，与 <see cref="HttpApiTestUIDemo"/> 默认一致）。
/// </summary>
[DisallowMultipleComponent]
public class HttpServiceDemo : MonoBehaviour
{
    [Serializable]
    public class HttpHeaderEntry
    {
        public bool enabled = true;
        public string key;
        public string value;
    }

    [Header("GET")]
    [SerializeField] private string _getUrl = HttpApiTestUIDemo.DefaultGetUrl;
    [SerializeField] private bool _getOnStart = true;

    [Header("POST")]
    [SerializeField] private string _postHost = HttpApiTestUIDemo.DefaultPostHost;
    [SerializeField] private string _postPath = HttpApiTestUIDemo.DefaultPostPath;
    [TextArea(4, 8)]
    [SerializeField] private string _postBody = HttpApiTestUIDemo.DefaultPostBody;

    [Header("请求头（与 UI Demo 默认一致，可增删改）")]
    [SerializeField] private HttpHeaderEntry[] _requestHeaders = Array.Empty<HttpHeaderEntry>();

    [Header("快捷键")]
    [SerializeField] private KeyCode _getKey = KeyCode.H;
    [SerializeField] private KeyCode _postKey = KeyCode.P;
    [SerializeField] private KeyCode _stopKey = KeyCode.S;

    private void Reset()
    {
        ApplyDefaultHeadersIfEmpty();
        ApplyDefaultPostBodyIfEmpty();
    }

    private void Start()
    {
        ApplyDefaultHeadersIfEmpty();
        ApplyDefaultPostBodyIfEmpty();

        if (_getOnStart)
        {
            RequestGet();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_getKey))
        {
            RequestGet();
        }

        if (Input.GetKeyDown(_postKey))
        {
            RequestPost();
        }

        if (Input.GetKeyDown(_stopKey))
        {
            StopRequest();
        }
    }

    [ContextMenu("停止当前请求")]
    public void StopRequest()
    {
        HttpService.Instance.StopCurrentRequest();
    }

    [ContextMenu("发送 GET")]
    public void RequestGet()
    {
        string url = _getUrl != null ? _getUrl.Trim() : string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[HttpServiceDemo] GET 失败：URL 为空。");
            return;
        }

        Dictionary<string, string> headers = CollectHeaders();
        Debug.Log($"[HttpServiceDemo] GET 请求：{url}");

        HttpService.Instance.Get<JsonPlaceholderTodoData>(url, OnGetResponse, headers);
    }

    [ContextMenu("发送 POST")]
    public void RequestPost()
    {
        if (!TryBuildPostUrl(out string url, out string buildError))
        {
            Debug.LogError($"[HttpServiceDemo] POST 失败：{buildError}");
            return;
        }

        Dictionary<string, string> headers = CollectHeaders();
        string jsonBody = BuildPostJsonBody();

        Debug.Log($"[HttpServiceDemo] POST 请求：{url}\n提交 JSON：{jsonBody}");

        HttpService.Instance.Post(url, jsonBody, OnPostResponse, headers);
    }

    /// <summary>兼容旧调用：等同 <see cref="RequestGet"/>。</summary>
    [ContextMenu("请求 JsonPlaceholder Todo")]
    public void RequestTodo()
    {
        RequestGet();
    }

    private void OnGetResponse(HttpRequestResult result, JsonPlaceholderTodoData data)
    {
        LogRawResult("GET", result);

        if (result.IsCancelled || !result.IsSuccess || data == null)
        {
            return;
        }

        Debug.Log(
            $"[HttpServiceDemo] GET 解析 → userId={data.userId}, id={data.id}, " +
            $"title=\"{data.title}\", completed={data.completed}");
    }

    private void OnPostResponse(HttpRequestResult result)
    {
        LogRawResult("POST", result);
    }

    private static void LogRawResult(string method, HttpRequestResult result)
    {
        if (result == null)
        {
            Debug.LogError($"[HttpServiceDemo] {method} 失败：结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            Debug.Log($"[HttpServiceDemo] {method} 已停止。");
            return;
        }

        string message = BuildResultLogText(method, result);
        if (result.IsSuccess)
        {
            Debug.Log(message);
            return;
        }

        Debug.LogError(message);
    }

    private static string BuildResultLogText(string method, HttpRequestResult result)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[HttpServiceDemo] {method} {(result.IsSuccess ? "成功" : "失败")}");
        builder.AppendLine($"状态码：{result.StatusCode}");
        if (!string.IsNullOrEmpty(result.Error))
        {
            builder.AppendLine($"错误：{result.Error}");
        }

        builder.AppendLine("JSON：");
        builder.Append(string.IsNullOrEmpty(result.RawBody) ? "(空)" : result.RawBody);
        return builder.ToString();
    }

    private string BuildPostJsonBody()
    {
        string customBody = _postBody != null ? _postBody.Trim() : string.Empty;
        if (!string.IsNullOrEmpty(customBody))
        {
            return customBody;
        }

        return HttpApiTestUIDemo.DefaultPostBody;
    }

    private bool TryBuildPostUrl(out string url, out string errorMessage)
    {
        url = null;
        errorMessage = null;

        string host = _postHost != null ? _postHost.Trim() : string.Empty;
        string path = _postPath != null ? _postPath.Trim() : string.Empty;

        if (string.IsNullOrEmpty(host))
        {
            errorMessage = "POST 主机地址为空。";
            return false;
        }

        if (string.IsNullOrEmpty(path))
        {
            errorMessage = "POST 路径为空。";
            return false;
        }

        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        url = host.Contains("://") ? $"{host}{path}" : $"http://{host}{path}";
        return true;
    }

    private Dictionary<string, string> CollectHeaders()
    {
        Dictionary<string, string> headers = new Dictionary<string, string>();
        if (_requestHeaders == null)
        {
            return headers;
        }

        for (int i = 0; i < _requestHeaders.Length; i++)
        {
            HttpHeaderEntry entry = _requestHeaders[i];
            if (entry == null || !entry.enabled)
            {
                continue;
            }

            string key = entry.key != null ? entry.key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            headers[key] = entry.value ?? string.Empty;
        }

        return headers;
    }

    private void ApplyDefaultPostBodyIfEmpty()
    {
        if (!string.IsNullOrWhiteSpace(_postBody))
        {
            return;
        }

        _postBody = HttpApiTestUIDemo.DefaultPostBody;
    }

    private void ApplyDefaultHeadersIfEmpty()
    {
        if (_requestHeaders != null && _requestHeaders.Length > 0)
        {
            return;
        }

        _requestHeaders = new[]
        {
            new HttpHeaderEntry
            {
                enabled = true,
                key = "Satoken",
                value = "r5aP7flTO3wHSf9MHxEwAZ35GdSxDM4cu89axMdKKLOxZtfXBQQRgjLI1oRTOicc",
            },
            new HttpHeaderEntry { enabled = true, key = "X-Tenant-Id", value = "1" },
            new HttpHeaderEntry { enabled = true, key = "Sys-Lang", value = "zh-CN" },
        };
    }
}
