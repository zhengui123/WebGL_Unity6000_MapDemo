using System.Collections;
using UnityEngine;

/// <summary>
/// 车辆热力图 HTTP 接口定时调用控制器：仅在国家级/省级由 <see cref="CarHotManager"/> 驱动启停。
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
    [SerializeField] private string _startTime = string.Empty;
    [SerializeField] private bool _useCurrentTimeAsEndTime = true;
    [SerializeField] private string _fixedEndTime = string.Empty;

    private Coroutine _pollCoroutine;
    private bool _isPolling;
    private bool _isRequesting;

    /// <summary>是否正在定时轮询。</summary>
    public bool IsPolling => _isPolling;

    /// <summary>当前请求省份 code（空=全国默认参数）。</summary>
    public string ProvinceCode => _provinceCode;

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

    /// <summary>开始定时轮询车辆热力图接口。</summary>
    public void StartPolling()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        _pollCoroutine = StartCoroutine(PollRoutine());
        Debug.Log(
            $"[VehicleHeatmapApiController] 已开启轮询，间隔={IntervalSeconds}s，province={(string.IsNullOrEmpty(_provinceCode) ? "(全国默认)" : _provinceCode)}。");
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

    /// <summary>立即请求一次（不影响轮询状态）。</summary>
    public void RequestOnce()
    {
        if (_isRequesting)
        {
            Debug.Log("[VehicleHeatmapApiController] 跳过：上一次车辆热力图请求尚未结束。");
            return;
        }

        if (HttpService.Instance != null && HttpService.Instance.IsRequestInProgress)
        {
            Debug.LogWarning("[VehicleHeatmapApiController] 跳过：其他 HTTP 请求进行中。");
            return;
        }

        _isRequesting = true;
        string endTime = ResolveEndTime();

        VehicleHeatmapApi.Request(
            _provinceCode,
            _region,
            _country,
            _startTime,
            endTime,
            OnRequestCompleted);
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

    private string ResolveEndTime()
    {
        if (_useCurrentTimeAsEndTime)
        {
            return BackendDateTimeTool.GetCurrentTimeString();
        }

        return string.IsNullOrEmpty(_fixedEndTime)
            ? HttpProjectConfig.DefaultQueryEndTime
            : _fixedEndTime;
    }
}
