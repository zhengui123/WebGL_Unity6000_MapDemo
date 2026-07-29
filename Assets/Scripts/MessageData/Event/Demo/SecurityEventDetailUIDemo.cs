using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 事件溯源详情 Demo：本地测试 JSON / 按文档参数请求接口。
/// </summary>
[DisallowMultipleComponent]
public class SecurityEventDetailUIDemo : MonoBehaviour
{
    [Header("请求参数")]
    [SerializeField] private InputField _eventIdInput;
    [SerializeField] private InputField _processStartTimeInput;
    [SerializeField] private InputField _processEndTimeInput;
    [SerializeField] private InputField _tenantIdInput;

    [Header("操作")]
    [SerializeField] private Button _loadLocalJsonButton;
    [SerializeField] private Button _requestApiButton;
    [SerializeField] private Button _applyToGjPanelButton;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Button _backButton;

    [Header("展示")]
    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _resultListText;

    [SerializeField] private DemoGameStateUINavigator _navigator;

    private SecurityEventDetailResponse _lastResponse;
    private bool _isRequesting;

    private void Awake()
    {
        EnsureReferences();
        BindButtons(true);
        ApplyDefaultInputs();
    }

    private void OnEnable()
    {
        EnsureReferences();
        SecurityEventDetailApi.RequestCompleted += HandleRequestCompleted;
        ApplyDefaultInputs();
        ApplyRuntimeTextStyle();
        RefreshStatus("就绪：可加载本地 JSON，或按图中参数请求接口。");
        RefreshResultList();
    }

    private void OnDisable()
    {
        SecurityEventDetailApi.RequestCompleted -= HandleRequestCompleted;
    }

    private void OnDestroy()
    {
        BindButtons(false);
    }

    private void BindButtons(bool bind)
    {
        Bind(_loadLocalJsonButton, OnLoadLocalJsonClicked, bind);
        Bind(_requestApiButton, OnRequestApiClicked, bind);
        Bind(_applyToGjPanelButton, OnApplyToGjPanelClicked, bind);
        Bind(_refreshButton, RefreshResultList, bind);
        Bind(_backButton, OnBackClicked, bind);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action, bool bind)
    {
        if (button == null)
        {
            return;
        }

        if (bind)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            button.onClick.RemoveListener(action);
        }
    }

    private void ApplyDefaultInputs()
    {
        SetInput(_eventIdInput, SecurityEventDetailRequest.DefaultEventId);
        SetInput(_processStartTimeInput, SecurityEventDetailRequest.DefaultProcessStartTime);
        SetInput(_processEndTimeInput, SecurityEventDetailRequest.DefaultProcessEndTime);
        SetInput(_tenantIdInput, SecurityEventDetailRequest.DefaultTenantId.ToString());
    }

    private void OnLoadLocalJsonClicked()
    {
        if (!SecurityEventDetailApi.TryApplySuccessfulResponseFromJson(
                SecurityEventDetailMockJson.SuccessResponseJson,
                out string error))
        {
            RefreshStatus($"本地 JSON 应用失败：{error}");
            return;
        }

        _lastResponse = SecurityEventDetailApi.LastResponse;
        RefreshStatus("已加载本地 JSON，并应用到 GJ_Panel / POI。");
        RefreshResultList();
    }

    private void OnRequestApiClicked()
    {
        if (_isRequesting)
        {
            RefreshStatus("请求进行中，请稍候。");
            return;
        }

        string eventId = ReadInput(_eventIdInput, SecurityEventDetailRequest.DefaultEventId);
        string start = ReadInput(_processStartTimeInput, SecurityEventDetailRequest.DefaultProcessStartTime);
        string end = ReadInput(_processEndTimeInput, SecurityEventDetailRequest.DefaultProcessEndTime);
        int tenantId = SecurityEventDetailRequest.DefaultTenantId;
        if (_tenantIdInput != null &&
            !string.IsNullOrWhiteSpace(_tenantIdInput.text) &&
            !int.TryParse(_tenantIdInput.text.Trim(), out tenantId))
        {
            RefreshStatus("tenantId 解析失败，请输入整数。");
            return;
        }

        _isRequesting = true;
        RefreshStatus(
            $"请求中… url={SecurityEventDetailApi.BuildRequestUrl()} | eventId={eventId}");
        SecurityEventDetailApi.Request(eventId, start, end, System.Array.Empty<string>(), tenantId);
    }

    private void OnApplyToGjPanelClicked()
    {
        SecurityEventDetailResponse cached = _lastResponse ?? SecurityEventDetailApi.LastResponse;
        if (cached?.data == null)
        {
            RefreshStatus("无可用数据：请先加载本地 JSON 或请求接口。");
            return;
        }

        if (!SecurityEventDetailApi.ApplySuccessfulResponse(cached, showPanel: true))
        {
            RefreshStatus("重新应用失败。");
            return;
        }

        _lastResponse = SecurityEventDetailApi.LastResponse;
        RefreshStatus($"已重新应用到 GJ_Panel / POI：{cached.data.event_name} / {cached.data.vin}");
        RefreshResultList();
    }

    private void HandleRequestCompleted(HttpRequestResult result, SecurityEventDetailResponse response)
    {
        _isRequesting = false;
        if (result == null)
        {
            RefreshStatus("请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            RefreshStatus("请求已取消。");
            return;
        }

        if (!result.IsSuccess)
        {
            RefreshStatus($"请求失败：{result.Error}");
            RefreshResultListRaw(result.RawBody);
            return;
        }

        if (response == null || !response.IsSuccess || response.data == null)
        {
            RefreshStatus($"业务失败：code={response?.code} msg={response?.msg}");
            RefreshResultListRaw(result.RawBody);
            return;
        }

        // 成功时 Api 已缓存并应用 GJ_Panel / POI
        _lastResponse = SecurityEventDetailApi.LastResponse ?? response;
        RefreshStatus($"请求成功并已应用：{response.data.event_name} / {response.data.vin}");
        RefreshResultList();
    }

    private void RefreshResultList()
    {
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"URL={SecurityEventDetailApi.BuildRequestUrl()}");
        builder.AppendLine($"请求体默认={SecurityEventDetailRequest.DefaultJson.Replace("\n", " ")}");
        builder.AppendLine("--- 最近数据 ---");

        if (_lastResponse?.data == null)
        {
            builder.AppendLine("(无)");
            if (_resultListText != null)
            {
                _resultListText.text = builder.ToString();
            }

            return;
        }

        SecurityEventDetailData data = _lastResponse.data;
        builder.AppendLine($"event_id={data.event_id}");
        builder.AppendLine($"event_name={data.event_name}");
        builder.AppendLine($"event_level={data.event_level} → {data.BuildEventNameDisplay()}");
        builder.AppendLine($"happen_time={data.happen_time}");
        builder.AppendLine($"vin={data.vin}");
        builder.AppendLine($"risk={data.risk_type_name}/{data.risk_subtype_name}");
        builder.AppendLine($"saasInnerEventType={data.saasInnerEventType}");
        builder.AppendLine($"metri_tag_pk_id={data.metri_tag_pk_id}");
        if (data.originalMap != null)
        {
            builder.AppendLine(
                $"originalMap={data.originalMap.province_name}/{data.originalMap.city_name}/" +
                $"{data.originalMap.district_name} ({data.originalMap.longitude},{data.originalMap.latitude})");
        }

        if (data.TryGetRecordLongitudeLatitude(out double lon, out double lat))
        {
            builder.AppendLine($"经纬度={lon},{lat}");
        }

        if (_resultListText != null)
        {
            _resultListText.text = builder.ToString();
        }
    }

    private void RefreshResultListRaw(string rawBody)
    {
        if (_resultListText == null)
        {
            return;
        }

        _resultListText.text = string.IsNullOrEmpty(rawBody) ? "(空响应)" : rawBody;
    }

    private void RefreshStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = message ?? string.Empty;
        }
    }

    private void OnBackClicked()
    {
        DemoGameStateUINavigator navigator = _navigator != null
            ? _navigator
            : GetComponentInParent<DemoGameStateUINavigator>();
        navigator?.ShowMenu();
    }

    private void ApplyRuntimeTextStyle()
    {
        ThreatDemoUiStyle.ApplyPanelLabel(_statusLabel);
        ThreatDemoUiStyle.ApplyResultText(_resultListText);
    }

    private void EnsureReferences()
    {
        if (_eventIdInput == null)
        {
            _eventIdInput = transform.Find("EventIdInput")?.GetComponent<InputField>();
        }

        if (_processStartTimeInput == null)
        {
            _processStartTimeInput = transform.Find("ProcessStartTimeInput")?.GetComponent<InputField>();
        }

        if (_processEndTimeInput == null)
        {
            _processEndTimeInput = transform.Find("ProcessEndTimeInput")?.GetComponent<InputField>();
        }

        if (_tenantIdInput == null)
        {
            _tenantIdInput = transform.Find("TenantIdInput")?.GetComponent<InputField>();
        }

        if (_loadLocalJsonButton == null)
        {
            _loadLocalJsonButton = transform.Find("LoadLocalJsonButton")?.GetComponent<Button>();
        }

        if (_requestApiButton == null)
        {
            _requestApiButton = transform.Find("RequestApiButton")?.GetComponent<Button>();
        }

        if (_applyToGjPanelButton == null)
        {
            _applyToGjPanelButton = transform.Find("ApplyToGjPanelButton")?.GetComponent<Button>();
        }

        if (_refreshButton == null)
        {
            _refreshButton = transform.Find("RefreshButton")?.GetComponent<Button>();
        }

        if (_backButton == null)
        {
            _backButton = transform.Find("BackButton")?.GetComponent<Button>();
        }

        if (_statusLabel == null)
        {
            _statusLabel = transform.Find("StatusLabel")?.GetComponent<Text>();
        }

        if (_resultListText == null)
        {
            _resultListText = transform.Find("ResultScrollView/Viewport/Content/ResultListText")?.GetComponent<Text>();
        }
    }

    private static void SetInput(InputField field, string value)
    {
        if (field != null && string.IsNullOrWhiteSpace(field.text))
        {
            field.text = value ?? string.Empty;
        }
    }

    private static string ReadInput(InputField field, string fallback)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.text))
        {
            return fallback;
        }

        return field.text.Trim();
    }
}
