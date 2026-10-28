using System.Collections;
using UnityEngine;

/// <summary>
/// 高危安全事件（威胁）HTTP 定时轮询：默认间隔 60s；默认开局自动轮询（Inspector `_autoStart` 可关）。
/// 开局首次请求延迟 <see cref="_initialDelaySeconds"/>；之后按间隔循环。
/// 威胁打断冷却期内暂停请求；冷却结束后若仍期望轮询则立即拉一次再恢复间隔循环。
/// Android（含 Editor 切 Android）不创建实例、不查询威胁数据。
/// </summary>
[DisallowMultipleComponent]
public class HighRiskSecurityEventApiController : UnitySingle<HighRiskSecurityEventApiController>
{
    [Header("轮询")]
    [Tooltip("开局自动 StartPolling；可在 Inspector 关闭。")]
    [SerializeField] private bool _autoStart = true;
    [SerializeField] private float _intervalSeconds = 60f;
    [Tooltip("进 Play 后首次威胁请求前等待秒数；不影响冷却恢复与后续间隔轮询。")]
    [SerializeField] private float _initialDelaySeconds = 20f;

    private Coroutine _pollCoroutine;
    private bool _wantPolling;
    private bool _pausedByCooldown;
    private bool _isRequesting;
    /// <summary>开局延迟是否已用过（冷却恢复不再走开局延迟）。</summary>
    private bool _startupDelayDone;

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

    /// <summary>Android 等平台关闭威胁功能时跳过查询/轮询。</summary>
    private static bool ShouldSkipThreatQuery =>
        !ThreatAlertSettings.IsThreatFeatureEnabledOnCurrentPlatform;

    private void Start()
    {
        if (ShouldSkipThreatQuery)
        {
            LogManager.LogBackend(
                "[HighRiskSecurityEventApiController] 当前平台关闭威胁功能，跳过自动 StartPolling。");
            return;
        }

        if (_autoStart)
        {
            StartPolling();
        }
    }

    /// <summary>进 Play / 场景加载后确保实例存在，从而由 Start 按 `_autoStart` 决定是否开局轮询。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceAfterSceneLoad()
    {
        if (ShouldSkipThreatQuery)
        {
            return;
        }

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
        if (ShouldSkipThreatQuery)
        {
            LogManager.LogBackend(
                "[HighRiskSecurityEventApiController] 当前平台关闭威胁功能，不开启威胁轮询。");
            return false;
        }

        _wantPolling = true;

        if (_pausedByCooldown || IsThreatInterruptCooldownActive())
        {
            _pausedByCooldown = true;
            StopPollCoroutineOnly();
            LogManager.LogBackend(
                $"[HighRiskSecurityEventApiController] 已记录开启轮询，但威胁冷却中，暂不请求 | 间隔={IntervalSeconds}s");
            return true;
        }

        StartPollCoroutine();
        LogManager.LogBackend(
            $"[HighRiskSecurityEventApiController] 已开启轮询 | 开局延迟={Mathf.Max(0f, _initialDelaySeconds):0.#}s | 间隔={IntervalSeconds}s");
        return true;
    }

    /// <summary>停止威胁高危事件定时轮询（清除意图，冷却结束后也不会自动恢复）。</summary>
    public bool StopPolling()
    {
        _wantPolling = false;
        StopPollCoroutineOnly();
        LogManager.LogBackend("[HighRiskSecurityEventApiController] 已停止轮询。");
        return true;
    }

    /// <summary>威胁打断冷却开始：暂停请求，保留轮询意图。</summary>
    public void OnThreatInterruptCooldownStarted()
    {
        if (ShouldSkipThreatQuery)
        {
            return;
        }

        _pausedByCooldown = true;
        StopPollCoroutineOnly();
        LogManager.LogBackend(
            $"[HighRiskSecurityEventApiController] 威胁冷却开始，暂停轮询请求 | wantPolling={_wantPolling}");
    }

    /// <summary>
    /// 威胁打断冷却结束（或调试取消冷却）：若仍期望轮询则先请求一次再恢复间隔循环。
    /// 不直接用本地缓存启动威胁检测。
    /// </summary>
    public void OnThreatInterruptCooldownEnded()
    {
        if (ShouldSkipThreatQuery)
        {
            return;
        }

        _pausedByCooldown = false;

        if (!_wantPolling)
        {
            LogManager.LogBackend(
                "[HighRiskSecurityEventApiController] 威胁冷却结束，当前未开启轮询，不请求、不评估本地缓存。");
            return;
        }

        LogManager.LogBackend(
            "[HighRiskSecurityEventApiController] 威胁冷却结束，先请求接口更新数据后再恢复轮询。");
        RequestOnce();
        StartPollCoroutine();
    }

    /// <summary>立即请求一次（不影响启停意图；冷却中跳过）。</summary>
    public void RequestOnce()
    {
        if (ShouldSkipThreatQuery)
        {
            LogManager.LogBackend(
                "[HighRiskSecurityEventApiController] Android 平台跳过单次威胁请求。");
            return;
        }

        if (_pausedByCooldown || IsThreatInterruptCooldownActive())
        {
            LogManager.LogBackend("[HighRiskSecurityEventApiController] 冷却中，跳过单次请求。");
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
        // 仅开局第一次：延迟后再首请求；冷却恢复已 RequestOnce，此处不再走开局延迟
        if (!_startupDelayDone)
        {
            _startupDelayDone = true;
            float delay = Mathf.Max(0f, _initialDelaySeconds);
            if (delay > 0f)
            {
                LogManager.LogBackend(
                    $"[HighRiskSecurityEventApiController] 开局延迟 {delay:0.#}s 后首次请求威胁数据。");
                yield return new WaitForSecondsRealtime(delay);
                if (!_wantPolling || _pausedByCooldown)
                {
                    yield break;
                }

                BeginRequest();
            }
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
        if (ShouldSkipThreatQuery)
        {
            return false;
        }

        if (_isRequesting || HighRiskSecurityEventApi.IsBatchRequesting)
        {
            HttpService http = HttpService.Instance;
            int active = http != null ? http.ActiveRequestCount : 0;
            int pending = http != null ? http.PendingRequestCount : 0;
            LogManager.LogBackend(
                "[HighRiskSecurityEventApiController] 同业务跳过：上一次高危事件尚未结束，未入 HttpService 队 | " +
                $"Http在途={active} | Http排队={pending}");
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
