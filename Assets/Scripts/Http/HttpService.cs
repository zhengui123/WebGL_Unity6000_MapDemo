using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP 请求服务：通过 UnityWebRequest 拉取/提交 JSON，并解析为指定数据类。
/// 支持请求队列 + 有限并行（默认最多 3 路同时在途）；新请求入队，不因忙碌丢弃。
/// 场景可不挂载，首次访问 <see cref="Instance"/> 时会自动创建。
/// </summary>
[DisallowMultipleComponent]
public class HttpService : UnitySingle<HttpService>
{
    private sealed class PendingJob
    {
        public Func<ActiveSlot, IEnumerator> RoutineFactory;
        public Action<HttpRequestResult> OnCompleted;
    }

    private sealed class ActiveSlot
    {
        public int Id;
        public Coroutine Coroutine;
        public UnityWebRequest Request;
        public Action<HttpRequestResult> OnCompleted;
        public bool IsCancelled;
    }

    [SerializeField] private float _timeoutSeconds = 30f;
    [SerializeField] private string _defaultContentType = "application/json";
    [Tooltip("同时在途的最大 HTTP 请求数。")]
    [SerializeField] private int _maxConcurrent = 3;

    private readonly Queue<PendingJob> _pendingJobs = new Queue<PendingJob>();
    private readonly List<ActiveSlot> _activeSlots = new List<ActiveSlot>();
    private int _nextSlotId;

    /// <summary>是否有进行中或排队中的请求。</summary>
    public bool IsRequestInProgress => _activeSlots.Count > 0 || _pendingJobs.Count > 0;

    /// <summary>当前在途请求数。</summary>
    public int ActiveRequestCount => _activeSlots.Count;

    /// <summary>排队等待中的请求数。</summary>
    public int PendingRequestCount => _pendingJobs.Count;

    /// <summary>并行上限（至少为 1）。</summary>
    public int MaxConcurrent
    {
        get => Mathf.Max(1, _maxConcurrent);
        set => _maxConcurrent = Mathf.Max(1, value);
    }

    /// <summary>
    /// 停止全部在途请求并清空队列；每个未完成任务回调 <see cref="HttpRequestResult.Cancelled"/>。
    /// </summary>
    public void StopCurrentRequest()
    {
        if (_pendingJobs.Count == 0 && _activeSlots.Count == 0)
        {
            return;
        }

        while (_pendingJobs.Count > 0)
        {
            PendingJob job = _pendingJobs.Dequeue();
            job.OnCompleted?.Invoke(HttpRequestResult.Cancelled());
        }

        // 复制列表，避免回调中再改集合
        List<ActiveSlot> slots = new List<ActiveSlot>(_activeSlots);
        _activeSlots.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            CancelActiveSlot(slots[i], invokeCallback: true);
        }
    }

    /// <summary>GET 请求，仅返回原始响应。</summary>
    public void Get(string url, Action<HttpRequestResult> onCompleted, Dictionary<string, string> headers = null)
    {
        Enqueue(slot => SendGetCoroutine(slot, url, headers), onCompleted);
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
        Enqueue(slot => SendPostCoroutine(slot, url, jsonBody, headers), onCompleted);
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

    private void Enqueue(Func<ActiveSlot, IEnumerator> routineFactory, Action<HttpRequestResult> onCompleted)
    {
        int activeBefore = _activeSlots.Count;
        _pendingJobs.Enqueue(new PendingJob
        {
            RoutineFactory = routineFactory,
            OnCompleted = onCompleted,
        });

        int queueLen = _pendingJobs.Count;
        int queuePosition = queueLen;
        Debug.Log(
            $"[HttpService] 入队 | 在途={activeBefore} | 排队总长={queueLen} | 本请求排队位置={queuePosition} | 并行上限={MaxConcurrent}");

        TryStartPendingJobs();
    }

    private void TryStartPendingJobs()
    {
        int max = MaxConcurrent;
        while (_activeSlots.Count < max && _pendingJobs.Count > 0)
        {
            PendingJob job = _pendingJobs.Dequeue();
            ActiveSlot slot = new ActiveSlot
            {
                Id = ++_nextSlotId,
                OnCompleted = job.OnCompleted,
            };
            _activeSlots.Add(slot);
            slot.Coroutine = StartCoroutine(WrapRoutine(slot, job.RoutineFactory));
        }
    }

    private IEnumerator WrapRoutine(ActiveSlot slot, Func<ActiveSlot, IEnumerator> factory)
    {
        IEnumerator routine = factory(slot);
        while (true)
        {
            if (slot.IsCancelled)
            {
                yield break;
            }

            bool moved;
            object current = null;
            try
            {
                moved = routine.MoveNext();
                if (moved)
                {
                    current = routine.Current;
                }
            }
            catch (Exception exception)
            {
                CompleteSlot(slot, HttpRequestResult.Failure($"HTTP 协程异常：{exception.Message}"));
                yield break;
            }

            if (!moved)
            {
                yield break;
            }

            yield return current;
        }
    }

    private void CompleteSlot(ActiveSlot slot, HttpRequestResult result)
    {
        if (slot == null || slot.IsCancelled)
        {
            return;
        }

        // 标记完成，防止 Stop / 重复 Complete 二次回调
        slot.IsCancelled = true;
        _activeSlots.Remove(slot);
        slot.Coroutine = null;
        AbortSlotRequest(slot);

        Action<HttpRequestResult> callback = slot.OnCompleted;
        slot.OnCompleted = null;
        callback?.Invoke(result);

        TryStartPendingJobs();
    }

    private void CancelActiveSlot(ActiveSlot slot, bool invokeCallback)
    {
        if (slot == null || slot.IsCancelled)
        {
            return;
        }

        slot.IsCancelled = true;

        if (slot.Coroutine != null)
        {
            StopCoroutine(slot.Coroutine);
            slot.Coroutine = null;
        }

        AbortSlotRequest(slot);

        if (!invokeCallback)
        {
            slot.OnCompleted = null;
            return;
        }

        Action<HttpRequestResult> callback = slot.OnCompleted;
        slot.OnCompleted = null;
        callback?.Invoke(HttpRequestResult.Cancelled());
    }

    private static void AbortSlotRequest(ActiveSlot slot)
    {
        if (slot.Request == null)
        {
            return;
        }

        try
        {
            slot.Request.Abort();
        }
        catch
        {
            // Abort 后 Dispose 仍尝试
        }

        slot.Request.Dispose();
        slot.Request = null;
    }

    private IEnumerator SendGetCoroutine(ActiveSlot slot, string url, Dictionary<string, string> headers)
    {
        if (!TryValidateUrl(url, out string urlError))
        {
            CompleteSlot(slot, HttpRequestResult.Failure(urlError));
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get(url);
        slot.Request = request;
        ApplyRequestSettings(request, url, headers);

        if (!TrySendWebRequest(request, out UnityWebRequestAsyncOperation operation, out string sendError))
        {
            request.Dispose();
            slot.Request = null;
            CompleteSlot(slot, HttpRequestResult.Failure(sendError));
            yield break;
        }

        yield return operation;

        if (slot.IsCancelled)
        {
            yield break;
        }

        HttpRequestResult result = BuildResult(request);
        request.Dispose();
        slot.Request = null;
        CompleteSlot(slot, result);
    }

    private IEnumerator SendPostCoroutine(
        ActiveSlot slot,
        string url,
        string jsonBody,
        Dictionary<string, string> headers)
    {
        if (!TryValidateUrl(url, out string urlError))
        {
            CompleteSlot(slot, HttpRequestResult.Failure(urlError));
            yield break;
        }

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);
        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        slot.Request = request;
        ApplyRequestSettings(request, url, headers, setJsonContentType: true);

        if (!TrySendWebRequest(request, out UnityWebRequestAsyncOperation operation, out string sendError))
        {
            request.Dispose();
            slot.Request = null;
            CompleteSlot(slot, HttpRequestResult.Failure(sendError));
            yield break;
        }

        yield return operation;

        if (slot.IsCancelled)
        {
            yield break;
        }

        HttpRequestResult result = BuildResult(request);
        request.Dispose();
        slot.Request = null;
        CompleteSlot(slot, result);
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
