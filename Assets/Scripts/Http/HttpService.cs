using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP 请求服务：通过 UnityWebRequest 拉取/提交 JSON，并解析为指定数据类。
/// 场景可不挂载，首次访问 <see cref="Instance"/> 时会自动创建。
/// </summary>
[DisallowMultipleComponent]
public class HttpService : UnitySingle<HttpService>
{
    [SerializeField] private float _timeoutSeconds = 30f;
    [SerializeField] private string _defaultContentType = "application/json";

    private Coroutine _activeCoroutine;
    private UnityWebRequest _activeRequest;
    private int _requestGeneration;
    private int _currentRequestGeneration;
    private Action<HttpRequestResult> _pendingCallback;

    /// <summary>是否有进行中的请求。</summary>
    public bool IsRequestInProgress => _activeCoroutine != null;

    /// <summary>停止当前进行中的请求，并通过回调返回 <see cref="HttpRequestResult.Cancelled"/>。</summary>
    public void StopCurrentRequest()
    {
        if (_pendingCallback == null && !IsRequestInProgress && _activeRequest == null)
        {
            return;
        }

        _requestGeneration++;
        AbortActiveRequest();
        StopActiveCoroutine();

        Action<HttpRequestResult> callback = _pendingCallback;
        _pendingCallback = null;
        callback?.Invoke(HttpRequestResult.Cancelled());
    }

    /// <summary>GET 请求，仅返回原始响应。</summary>
    public void Get(string url, Action<HttpRequestResult> onCompleted, Dictionary<string, string> headers = null)
    {
        LaunchRequest(SendGetCoroutine(url, headers), onCompleted);
    }

    /// <summary>GET 请求，成功时将响应体解析为 <typeparamref name="T"/>。</summary>
    public void Get<T>(string url, Action<HttpRequestResult, T> onCompleted, Dictionary<string, string> headers = null)
        where T : class
    {
        Get(url, result =>
        {
            if (result.IsCancelled)
            {
                onCompleted?.Invoke(result, null);
                return;
            }

            if (!result.IsSuccess)
            {
                onCompleted?.Invoke(result, null);
                return;
            }

            if (!HttpJsonParser.TryParse(result.RawBody, out T data, out string parseError))
            {
                onCompleted?.Invoke(HttpRequestResult.Failure(parseError, result.StatusCode, result.RawBody), null);
                return;
            }

            onCompleted?.Invoke(result, data);
        }, headers);
    }

    /// <summary>POST 原始 JSON 字符串，返回原始响应。</summary>
    public void Post(
        string url,
        string jsonBody,
        Action<HttpRequestResult> onCompleted,
        Dictionary<string, string> headers = null)
    {
        LaunchRequest(SendPostCoroutine(url, jsonBody, headers), onCompleted);
    }

    /// <summary>POST JSON 请求体，返回原始响应。</summary>
    public void PostJson<TRequest>(
        string url,
        TRequest requestBody,
        Action<HttpRequestResult> onCompleted,
        Dictionary<string, string> headers = null)
    {
        string json = HttpJsonParser.ToJson(requestBody);
        Post(url, json, onCompleted, headers);
    }

    /// <summary>POST JSON 请求体，并将响应解析为 <typeparamref name="TResponse"/>。</summary>
    public void PostJson<TRequest, TResponse>(
        string url,
        TRequest requestBody,
        Action<HttpRequestResult, TResponse> onCompleted,
        Dictionary<string, string> headers = null)
        where TResponse : class
    {
        string json = HttpJsonParser.ToJson(requestBody);
        Post(url, json, result =>
        {
            if (result.IsCancelled)
            {
                onCompleted?.Invoke(result, null);
                return;
            }

            if (!result.IsSuccess)
            {
                onCompleted?.Invoke(result, null);
                return;
            }

            if (!HttpJsonParser.TryParse(result.RawBody, out TResponse data, out string parseError))
            {
                onCompleted?.Invoke(HttpRequestResult.Failure(parseError, result.StatusCode, result.RawBody), null);
                return;
            }

            onCompleted?.Invoke(result, data);
        }, headers);
    }

    private void LaunchRequest(IEnumerator routine, Action<HttpRequestResult> onCompleted)
    {
        CancelRequestSilently();
        _pendingCallback = onCompleted;
        _currentRequestGeneration = ++_requestGeneration;
        _activeCoroutine = StartCoroutine(routine);
    }

    private void CancelRequestSilently()
    {
        if (!IsRequestInProgress && _activeRequest == null)
        {
            return;
        }

        _requestGeneration++;
        AbortActiveRequest();
        StopActiveCoroutine();
        _pendingCallback = null;
    }

    private void StopActiveCoroutine()
    {
        if (_activeCoroutine == null)
        {
            return;
        }

        StopCoroutine(_activeCoroutine);
        _activeCoroutine = null;
    }

    private void AbortActiveRequest()
    {
        if (_activeRequest == null)
        {
            return;
        }

        _activeRequest.Abort();
        _activeRequest = null;
    }

    private bool TryFinishRequest(HttpRequestResult result)
    {
        if (_currentRequestGeneration != _requestGeneration)
        {
            return false;
        }

        _activeCoroutine = null;
        _activeRequest = null;

        Action<HttpRequestResult> callback = _pendingCallback;
        _pendingCallback = null;
        callback?.Invoke(result);
        return true;
    }

    private IEnumerator SendGetCoroutine(string url, Dictionary<string, string> headers)
    {
        if (!TryValidateUrl(url, out string urlError))
        {
            TryFinishRequest(HttpRequestResult.Failure(urlError));
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get(url);
        _activeRequest = request;
        ApplyRequestSettings(request, url, headers);

        if (!TrySendWebRequest(request, out UnityWebRequestAsyncOperation operation, out string sendError))
        {
            request.Dispose();
            _activeRequest = null;
            TryFinishRequest(HttpRequestResult.Failure(sendError));
            yield break;
        }

        yield return operation;

        if (_currentRequestGeneration != _requestGeneration)
        {
            request.Dispose();
            yield break;
        }

        HttpRequestResult result = BuildResult(request);
        request.Dispose();
        _activeRequest = null;
        TryFinishRequest(result);
    }

    private IEnumerator SendPostCoroutine(string url, string jsonBody, Dictionary<string, string> headers)
    {
        if (!TryValidateUrl(url, out string urlError))
        {
            TryFinishRequest(HttpRequestResult.Failure(urlError));
            yield break;
        }

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);
        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        _activeRequest = request;
        ApplyRequestSettings(request, url, headers, setJsonContentType: true);

        if (!TrySendWebRequest(request, out UnityWebRequestAsyncOperation operation, out string sendError))
        {
            request.Dispose();
            _activeRequest = null;
            TryFinishRequest(HttpRequestResult.Failure(sendError));
            yield break;
        }

        yield return operation;

        if (_currentRequestGeneration != _requestGeneration)
        {
            request.Dispose();
            yield break;
        }

        HttpRequestResult result = BuildResult(request);
        request.Dispose();
        _activeRequest = null;
        TryFinishRequest(result);
    }

    private void ApplyRequestSettings(
        UnityWebRequest request,
        string url,
        Dictionary<string, string> headers,
        bool setJsonContentType = false)
    {
        request.timeout = Mathf.Max(1, Mathf.RoundToInt(_timeoutSeconds));

        if (ShouldBypassSslValidation(url))
        {
            request.certificateHandler = new DevBypassCertificateHandler();
        }

        if (setJsonContentType)
        {
            request.SetRequestHeader("Content-Type", _defaultContentType);
        }

        if (headers == null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> header in headers)
        {
            if (!string.IsNullOrEmpty(header.Key))
            {
                request.SetRequestHeader(header.Key, header.Value ?? string.Empty);
            }
        }
    }

    private static bool ShouldBypassSslValidation(string url)
    {
        if (!HttpProjectConfig.SkipSslCertificateValidation)
        {
            return false;
        }

        return !string.IsNullOrEmpty(url)
            && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidateUrl(string url, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            errorMessage = "URL 为空。";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            errorMessage = $"URL 无效：{url}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TrySendWebRequest(
        UnityWebRequest request,
        out UnityWebRequestAsyncOperation operation,
        out string errorMessage)
    {
        operation = null;
        errorMessage = null;

        try
        {
            operation = request.SendWebRequest();
            return true;
        }
        catch (InvalidOperationException exception)
        {
            errorMessage = BuildInsecureHttpErrorMessage(exception.Message);
            return false;
        }
    }

    private static string BuildInsecureHttpErrorMessage(string rawMessage)
    {
        if (!string.IsNullOrEmpty(rawMessage)
            && rawMessage.IndexOf("Insecure connection", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "当前项目禁止 HTTP 明文请求（Insecure connection not allowed）。" +
                   "请在 Project Settings > Player > Other Settings > Allow downloads over HTTP " +
                   "设置为 Development Only 或 Always Allowed；若后端支持 HTTPS，请改用 https://。";
        }

        return string.IsNullOrEmpty(rawMessage) ? "HTTP 请求启动失败。" : rawMessage;
    }

    private static HttpRequestResult BuildResult(UnityWebRequest request)
    {
        long statusCode = request.responseCode;

#if UNITY_2020_2_OR_NEWER
        bool isNetworkError = request.result == UnityWebRequest.Result.ConnectionError
            || request.result == UnityWebRequest.Result.ProtocolError;
#else
        bool isNetworkError = request.isNetworkError || request.isHttpError;
#endif

        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (isNetworkError)
        {
            string error = string.IsNullOrEmpty(request.error) ? "HTTP 请求失败" : request.error;
            return HttpRequestResult.Failure(EnhanceSslErrorMessage(error), statusCode, body);
        }

        return HttpRequestResult.Success(statusCode, body);
    }

    private static string EnhanceSslErrorMessage(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return error;
        }

        bool isSslError = error.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0
            || error.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
            || error.IndexOf("Cert verify", StringComparison.OrdinalIgnoreCase) >= 0
            || error.IndexOf("UnityTls", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isSslError)
        {
            return error;
        }

        return error +
               "\n\n【SSL 证书校验失败】常见原因：" +
               "\n1. 用 IP 访问 HTTPS，但证书 CN/SAN 只签了域名（Curl 60 / UnityTls 7）" +
               "\n2. 自签名或企业内网证书未被信任" +
               "\n解决：" +
               "\n- 内网测试优先用 http://（HttpBackendConfig.json 中 useHttps: false）" +
               "\n- 或改用证书对应的域名访问" +
               "\n- 仅开发环境可在 HttpBackendConfig.json 设置 skipSslCertificateValidation: true";
    }
}
