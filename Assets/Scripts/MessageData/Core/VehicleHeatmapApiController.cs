using System.Collections;
using UnityEngine;

/// <summary>
/// 车辆热力图 HTTP 接口定时调用控制器：仅在国家级/省级由 <see cref="CarHotManager"/> 驱动启停。
/// <para>默认轮询：startTime 空、endTime 当前时间、isReplay=false。</para>
/// <para>指定时段轮询：固定起止时间、isReplay=true；关闭后回到默认轮询。</para>
/// </summary>
[DisallowMultipleComponent]
public class VehicleHeatmapApiController : UnitySingle<VehicleHeatmapApiController>
{
    [Header("轮询")]
    [Tooltip("默认关闭；由 CarHotManager 在进入国家/省级时 StartPolling。")]
    [SerializeField] private bool _autoStart;
    [SerializeField] private float _intervalSeconds = 60f;
    [Tooltip("开启轮询后是否立即请求一次，再进入间隔等待。")]
    [SerializeField] private bool _requestImmediatelyOnStart = true;

    [Header("查询参数")]
    [Tooltip("省级 adcode；空或 \"0\" 表示全国默认请求。")]
    [SerializeField] private string _provinceCode = "";
    [SerializeField] private string _region = string.Empty;
    [SerializeField] private string _country = string.Empty;

    [Header("运行时状态（只读调试）")]
    [SerializeField] private bool _isSpecifiedTimePolling;
    [SerializeField] private string _specifiedStartTime = string.Empty;
    [SerializeField] private string _specifiedEndTime = string.Empty;
    [SerializeField] private bool _isReplay;

    private Coroutine _pollCoroutine;
    private bool _isPolling;
    private bool _isRequesting;

    /// <summary>是否正在定时轮询。</summary>
    public bool IsPolling => _isPolling;

    /// <summary>是否处于指定时段轮询（isReplay=true）。</summary>
    public bool IsSpecifiedTimePolling => _isSpecifiedTimePolling;

    /// <summary>当前请求省份 code（空=全国默认参数）。</summary>
    public string ProvinceCode => _provinceCode;

    /// <summary>当前 isReplay（默认 false；指定时段 true）。</summary>
    public bool IsReplay => _isReplay;

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

    private void OnDisable()
    {
        StopPolling();
    }

    /// <summary>
    /// 设置查询省份。全国级传空或 "0"；省级传对应 adcode。
    /// 会触发下一次 Request 使用新参数；若正在轮询且 needImmediateRequest，则立刻请求一次。
    /// </summary>
    public void SetProvinceCode(string provinceCode, bool requestImmediately = true)
    {
        if (string.IsNullOrWhiteSpace(provinceCode) ||
            provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode)
        {
            _provinceCode = string.Empty;
        }
        else
        {
            _provinceCode = provinceCode.Trim();
        }

        if (requestImmediately && _isPolling)
        {
            RequestOnce();
        }
    }

    /// <summary>
    /// 开启指定时段轮询：固定起止时间，isReplay=true。
    /// 若尚未轮询则启动轮询；已在轮询则立即请求一次。
    /// </summary>
    public bool StartSpecifiedTimePolling(string startTime, string endTime)
    {
        if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
        {
            Debug.LogWarning(
                "[VehicleHeatmapApiController] StartSpecifiedTimePolling 失败：起止时间不能为空。");
            return false;
        }

        _isSpecifiedTimePolling = true;
        _specifiedStartTime = startTime.Trim();
        _specifiedEndTime = endTime.Trim();
        _isReplay = true;

        if (!_isPolling)
        {
            StartPolling();
        }
        else
        {
            RequestOnce();
        }

        Debug.Log(
            $"[VehicleHeatmapApiController] 已开启指定时段轮询：{_specifiedStartTime} ~ {_specifiedEndTime}，isReplay=true。");
        return true;
    }

    /// <summary>
    /// 关闭指定时段轮询：isReplay=false，恢复默认轮询参数（start 空、end 当前时间）。
    /// </summary>
    public bool StopSpecifiedTimePolling()
    {
        ApplyDefaultPollingParameters();

        if (_isPolling)
        {
            RequestOnce();
        }

        Debug.Log("[VehicleHeatmapApiController] 已关闭指定时段轮询，恢复默认轮询（isReplay=false）。");
        return true;
    }

    /// <summary>开始定时轮询车辆热力图接口（沿用当前模式参数）。</summary>
    public void StartPolling()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        _pollCoroutine = StartCoroutine(PollRoutine());
        Debug.Log(
            $"[VehicleHeatmapApiController] 已开启轮询，间隔={IntervalSeconds}s，" +
            $"mode={(_isSpecifiedTimePolling ? "指定时段" : "默认")}，" +
            $"province={(string.IsNullOrEmpty(_provinceCode) ? "(全国默认)" : _provinceCode)}。");
    }

    /// <summary>停止定时轮询。</summary>
    public void StopPolling()
    {
        if (!_isPolling)
        {
            return;
        }

        _isPolling = false;
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }

        Debug.Log("[VehicleHeatmapApiController] 已停止轮询。");
    }

    /// <summary>立即请求一次（不影响轮询状态；参数由当前模式决定）。</summary>
    public void RequestOnce()
    {
        ResolveRequestTimes(out string startTime, out string endTime, out bool isReplay);
        BeginRequest(startTime, endTime, isReplay);
    }

    /// <summary>
    /// 主动请求一次热力图（不启停、不改轮询模式）。
    /// 起止时间为空时：start 空、end 当前时间；isReplay 由调用方指定。
    /// </summary>
    public bool RequestOnceWithParams(string startTime, string endTime, bool isReplay)
    {
        string resolvedStart = string.IsNullOrWhiteSpace(startTime) ? string.Empty : startTime.Trim();
        string resolvedEnd = string.IsNullOrWhiteSpace(endTime)
            ? BackendDateTimeTool.GetCurrentTimeString()
            : endTime.Trim();

        if (!BeginRequest(resolvedStart, resolvedEnd, isReplay))
        {
            return false;
        }

        Debug.Log(
            $"[VehicleHeatmapApiController] 单次请求 | start={resolvedStart} | end={resolvedEnd} | isReplay={isReplay}");
        return true;
    }

    /// <summary>发起一次 HTTP 请求；忙碌时返回 false。</summary>
    private bool BeginRequest(string startTime, string endTime, bool isReplay)
    {
        if (_isRequesting)
        {
            Debug.Log("[VehicleHeatmapApiController] 跳过：上一次车辆热力图请求尚未结束。");
            return false;
        }

        if (HttpService.Instance != null && HttpService.Instance.IsRequestInProgress)
        {
            Debug.LogWarning("[VehicleHeatmapApiController] 跳过：其他 HTTP 请求进行中。");
            return false;
        }

        _isRequesting = true;
        VehicleHeatmapApi.Request(
            _provinceCode,
            _region,
            _country,
            startTime,
            endTime,
            OnRequestCompleted,
            additionalHeaders: null,
            isReplay: isReplay);
        return true;
    }

    private void ApplyDefaultPollingParameters()
    {
        _isSpecifiedTimePolling = false;
        _specifiedStartTime = string.Empty;
        _specifiedEndTime = string.Empty;
        _isReplay = false;
    }

    private void ResolveRequestTimes(out string startTime, out string endTime, out bool isReplay)
    {
        if (_isSpecifiedTimePolling)
        {
            startTime = _specifiedStartTime;
            endTime = _specifiedEndTime;
            isReplay = true;
            return;
        }

        // 默认轮询：起始不传，结束为当前时间，isReplay=false
        startTime = string.Empty;
        endTime = BackendDateTimeTool.GetCurrentTimeString();
        isReplay = false;
    }

    private IEnumerator PollRoutine()
    {
        if (_requestImmediatelyOnStart)
        {
            RequestOnce();
        }

        WaitForSeconds wait = new WaitForSeconds(IntervalSeconds);
        while (_isPolling)
        {
            yield return wait;
            if (!_isPolling)
            {
                yield break;
            }

            RequestOnce();
        }
    }

    private void OnRequestCompleted(HttpRequestResult result, LatestVinLocationResponse response)
    {
        _isRequesting = false;
    }
}
