using System.Collections;
using UnityEngine;

/// <summary>
/// 高危安全事件（威胁）HTTP 定时轮询：默认间隔 60s；默认开局自动轮询（Inspector `_autoStart` 可关）。
/// 威胁打断冷却期内暂停请求；冷却结束后若仍处于「期望轮询」则先拉一次接口再恢复间隔循环。
/// </summary>
[DisallowMultipleComponent]
public class HighRiskSecurityEventApiController : UnitySingle<HighRiskSecurityEventApiController>
{
    [Header("轮询")]
    [Tooltip("开局自动 StartPolling；可在 Inspector 关闭。")]
    [SerializeField] private bool _autoStart = true;
    [SerializeField] private float _intervalSeconds = 60f;
    [Tooltip("开启轮询后是否立即请求一次，再进入间隔等待。")]
    [SerializeField] private bool _requestImmediatelyOnStart = true;

    private Coroutine _pollCoroutine;
    private bool _wantPolling;
    private bool _pausedByCooldown;
    private bool _isRequesting;

    /// <summary>宿主是否已开启轮询意图（冷却暂停时仍为 true）。</summary>
    public bool IsPollingEnabled => _wantPolling;

    /// <summary>协程是否正在跑（冷却暂停时为 false）。</summary>
    public bool IsPollCoroutineRunning => _pollCoroutine != null;

    /// <summary>是否因威胁打断冷却而暂停请求。</summary>
    public bool IsPausedByCooldown => _pausedByCooldown;

    /// <summary>轮询间隔（秒）。</summary>
    public float IntervalSeconds
    {
        get => _intervalSeconds;
        set => _intervalSeconds = Mathf.Max(1f, value);
    }

    private void Start()
    {
        if (_autoStart)
        {
            StartPolling();
        }
    }

    /// <summary>进 Play / 场景加载后确保实例存在，从而由 Start 按 `_autoStart` 决定是否开局轮询。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceAfterSceneLoad()
    {
        _ = Instance;
    }

    private void OnDisable()
    {
        StopPollCoroutineOnly();
    }

    /// <summary>
    /// 开启威胁高危事件定时轮询。冷却中仅记录意图，冷却结束后再真正请求。
    /// </summary>
    public bool StartPolling()
    {
        _wantPolling = true;

        if (_pausedByCooldown || IsThreatInterruptCooldownActive())
        {
            _pausedByCooldown = true;
            StopPollCoroutineOnly();
            Debug.Log(
                $"[HighRiskSecurityEventApiController] 已记录开启轮询，但威胁冷却中，暂不请求 | 间隔={IntervalSeconds}s");
            return true;
        }

        StartPollCoroutine();
        Debug.Log(
            $"[HighRiskSecurityEventApiController] 已开启轮询，间隔={IntervalSeconds}s");
        return true;
    }

    /// <summary>停止威胁高危事件定时轮询（清除意图，冷却结束后也不会自动恢复）。</summary>
    public bool StopPolling()
    {
        _wantPolling = false;
        StopPollCoroutineOnly();
        Debug.Log("[HighRiskSecurityEventApiController] 已停止轮询。");
        return true;
    }

    /// <summary>威胁打断冷却开始：暂停请求，保留轮询意图。</summary>
    public void OnThreatInterruptCooldownStarted()
    {
        _pausedByCooldown = true;
        StopPollCoroutineOnly();
        Debug.Log(
            $"[HighRiskSecurityEventApiController] 威胁冷却开始，暂停轮询请求 | wantPolling={_wantPolling}");
    }

    /// <summary>
    /// 威胁打断冷却结束（或调试取消冷却）：若仍期望轮询则先请求一次再恢复间隔循环。
    /// 不直接用本地缓存启动威胁检测。
    /// </summary>
    public void OnThreatInterruptCooldownEnded()
    {
        _pausedByCooldown = false;

        if (!_wantPolling)
        {
            Debug.Log(
                "[HighRiskSecurityEventApiController] 威胁冷却结束，当前未开启轮询，不请求、不评估本地缓存。");
            return;
        }

        Debug.Log(
            "[HighRiskSecurityEventApiController] 威胁冷却结束，先请求接口更新数据后再恢复轮询。");
        StartPollCoroutine();
    }

    /// <summary>立即请求一次（不影响启停意图；冷却中跳过）。</summary>
    public void RequestOnce()
    {
        if (_pausedByCooldown || IsThreatInterruptCooldownActive())
        {
            Debug.Log("[HighRiskSecurityEventApiController] 冷却中，跳过单次请求。");
            return;
        }

        BeginRequest();
    }

    private void StartPollCoroutine()
    {
        StopPollCoroutineOnly();
        _pollCoroutine = StartCoroutine(PollRoutine());
    }

    private void StopPollCoroutineOnly()
    {
        if (_pollCoroutine == null)
        {
            return;
        }

        StopCoroutine(_pollCoroutine);
        _pollCoroutine = null;
    }

    private IEnumerator PollRoutine()
    {
        if (_requestImmediatelyOnStart)
        {
            BeginRequest();
        }

        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(IntervalSeconds);
        while (_wantPolling && !_pausedByCooldown)
        {
            yield return wait;
            if (!_wantPolling || _pausedByCooldown)
            {
                yield break;
            }

            BeginRequest();
        }
    }

    private bool BeginRequest()
    {
        if (_isRequesting || HighRiskSecurityEventApi.IsBatchRequesting)
        {
            Debug.Log("[HighRiskSecurityEventApiController] 跳过：上一次高危事件请求尚未结束。");
            return false;
        }

        if (HttpService.Instance != null && HttpService.Instance.IsRequestInProgress)
        {
            Debug.LogWarning("[HighRiskSecurityEventApiController] 跳过：其他 HTTP 请求进行中。");
            return false;
        }

        _isRequesting = true;
        HighRiskSecurityEventApi.RequestAllDomesticProvinces(
            startTime: null,
            endTime: null,
            onCompleted: (_, __) => { _isRequesting = false; });
        return true;
    }

    private static bool IsThreatInterruptCooldownActive()
    {
        return ThreatAlertFlowRunner.Instance != null &&
               ThreatAlertFlowRunner.Instance.IsInInterruptCooldown;
    }
}
