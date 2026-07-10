using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 威胁态势高危安全事件接口 Demo：分批按省请求并展示结果列表。
/// </summary>
[DisallowMultipleComponent]
public class ThreatHighRiskSecurityEventUIDemo : MonoBehaviour
{
    public const string DefaultProvinceCode = "370000";

    [Header("查询参数")]
    [SerializeField] private InputField _startTimeInput;
    [SerializeField] private InputField _endTimeInput;
    [SerializeField] private InputField _provinceCodeInput;

    [Header("操作")]
    [SerializeField] private Button _requestAllProvincesButton;
    [SerializeField] private Button _requestSingleProvinceButton;
    [SerializeField] private Button _refreshListButton;
    [SerializeField] private Button _completeAlertButton;
    [SerializeField] private Button _backButton;

    [Header("展示")]
    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _resultListText;
    [SerializeField] private ScrollRect _resultScroll;

    [SerializeField] private DemoGameStateUINavigator _navigator;

    private int _batchCompletedRegionCount;
    private int _batchTotalRegionCount;
    private bool _isRequesting;

    private void Awake()
    {
        EnsureReferences();

        if (_requestAllProvincesButton != null)
        {
            _requestAllProvincesButton.onClick.AddListener(OnRequestAllProvincesClicked);
        }

        if (_requestSingleProvinceButton != null)
        {
            _requestSingleProvinceButton.onClick.AddListener(OnRequestSingleProvinceClicked);
        }

        if (_refreshListButton != null)
        {
            _refreshListButton.onClick.AddListener(RefreshResultList);
        }

        if (_completeAlertButton != null)
        {
            _completeAlertButton.onClick.AddListener(OnCompleteAlertClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        ApplyDefaultInputs();
    }

    private void Start()
    {
        EnsureReferences();
        ApplyDefaultInputs();
        ApplyRuntimeTextStyle();
        RefreshEffectiveTimeHint();
    }

    private void OnEnable()
    {
        HighRiskSecurityEventApi.RequestCompleted += HandleRegionRequestCompleted;
        HighRiskSecurityEventApi.BatchCompleted += HandleBatchCompleted;
        HighRiskSecurityEventDataStore.Instance.DataChanged += HandleDataStoreChanged;
        ThreatProvinceAlertController.ProvinceAlertStarted += HandleProvinceAlertStarted;
        ThreatProvinceAlertController.ProvinceAlertCompleted += HandleProvinceAlertCompleted;
        ThreatProvinceAlertController.AllProvinceAlertsCompleted += HandleAllProvinceAlertsCompleted;

        ApplyDefaultInputs();
        ApplyRuntimeTextStyle();
        RefreshEffectiveTimeHint();
        RefreshStatusLabel("就绪：可请求国内全部省份或单省。");
        RefreshResultList();
        RefreshRequestButtons();
    }

    private void OnDisable()
    {
        HighRiskSecurityEventApi.RequestCompleted -= HandleRegionRequestCompleted;
        HighRiskSecurityEventApi.BatchCompleted -= HandleBatchCompleted;

        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        if (store != null)
        {
            store.DataChanged -= HandleDataStoreChanged;
        }

        ThreatProvinceAlertController.ProvinceAlertStarted -= HandleProvinceAlertStarted;
        ThreatProvinceAlertController.ProvinceAlertCompleted -= HandleProvinceAlertCompleted;
        ThreatProvinceAlertController.AllProvinceAlertsCompleted -= HandleAllProvinceAlertsCompleted;
    }

    private void OnDestroy()
    {
        if (_requestAllProvincesButton != null)
        {
            _requestAllProvincesButton.onClick.RemoveListener(OnRequestAllProvincesClicked);
        }

        if (_requestSingleProvinceButton != null)
        {
            _requestSingleProvinceButton.onClick.RemoveListener(OnRequestSingleProvinceClicked);
        }

        if (_refreshListButton != null)
        {
            _refreshListButton.onClick.RemoveListener(RefreshResultList);
        }

        if (_completeAlertButton != null)
        {
            _completeAlertButton.onClick.RemoveListener(OnCompleteAlertClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void ApplyDefaultInputs()
    {
        EnsureReferences();
        SetInputText(_startTimeInput, ThreatQueryDefaults.StartTime);
        SetInputText(_endTimeInput, ThreatQueryDefaults.EndTime);
        SetInputText(_provinceCodeInput, DefaultProvinceCode);
    }

    private void RefreshEffectiveTimeHint()
    {
        string startTime = ResolveStartTimeFromInput();
        string endTime = ResolveEndTimeFromInput();
        if (_startTimeInput != null && string.IsNullOrWhiteSpace(_startTimeInput.text))
        {
            SetInputText(_startTimeInput, startTime);
        }

        if (_endTimeInput != null && string.IsNullOrWhiteSpace(_endTimeInput.text))
        {
            SetInputText(_endTimeInput, endTime);
        }
    }

    private void ApplyRuntimeTextStyle()
    {
        ThreatDemoUiStyle.ApplyPanelLabel(_statusLabel);
        ThreatDemoUiStyle.ApplyResultText(_resultListText);
        ThreatDemoUiStyle.ApplyInputField(_startTimeInput);
        ThreatDemoUiStyle.ApplyInputField(_endTimeInput);
        ThreatDemoUiStyle.ApplyInputField(_provinceCodeInput);
    }

    private void OnRequestAllProvincesClicked()
    {
        if (_isRequesting || HighRiskSecurityEventApi.IsBatchRequesting)
        {
            RefreshStatusLabel("已有请求进行中，请等待完成。");
            return;
        }

        string startTime = ResolveStartTimeFromInput();
        string endTime = ResolveEndTimeFromInput();
        int regionCount = HighRiskSecurityEventApi.GetDomesticProvinceCount();
        if (regionCount <= 0)
        {
            RefreshStatusLabel("请求失败：未获取到国内省级 code 列表。");
            return;
        }

        _batchCompletedRegionCount = 0;
        _batchTotalRegionCount = regionCount;
        _isRequesting = true;
        RefreshRequestButtons();
        RefreshStatusLabel($"分批请求中：0/{regionCount} 省…");
        RefreshResultList();

        HighRiskSecurityEventApi.RequestAllDomesticProvinces(
            startTime,
            endTime,
            OnAllProvincesBatchCompleted);
    }

    private void OnRequestSingleProvinceClicked()
    {
        if (_isRequesting || HighRiskSecurityEventApi.IsBatchRequesting)
        {
            RefreshStatusLabel("已有请求进行中，请等待完成。");
            return;
        }

        string provinceCode = GetInputText(_provinceCodeInput, DefaultProvinceCode);
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            RefreshStatusLabel("单省请求失败：province code 为空。");
            return;
        }

        string startTime = ResolveStartTimeFromInput();
        string endTime = ResolveEndTimeFromInput();
        ThreatRegionRequestCodes regionCodes = new ThreatRegionRequestCodes(provinceCode.Trim(), string.Empty);

        _isRequesting = true;
        RefreshRequestButtons();
        RefreshStatusLabel($"单省请求中：{provinceCode}…");

        HighRiskSecurityEventApi.RequestSingleRegion(
            regionCodes,
            startTime,
            endTime,
            OnSingleProvinceRequestCompleted,
            evaluateAlerts: true);
    }

    private void OnSingleProvinceRequestCompleted(HttpRequestResult result, HighRiskSecurityEventResponse response)
    {
        _isRequesting = false;
        RefreshRequestButtons();

        if (result == null)
        {
            RefreshStatusLabel("单省请求失败：结果为空。");
            return;
        }

        if (!result.IsSuccess || response == null || !response.IsSuccess)
        {
            string error = result.Error;
            if (response != null && !response.IsSuccess)
            {
                error = $"code={response.code}, msg={response.msg}";
            }

            RefreshStatusLabel($"单省请求失败：{error}");
            RefreshResultList();
            return;
        }

        int count = response.data != null ? response.data.Length : 0;
        RefreshStatusLabel($"单省请求成功：province={GetInputText(_provinceCodeInput, DefaultProvinceCode)}，事件数={count}");
        RefreshResultList();
    }

    private void OnAllProvincesBatchCompleted(HttpRequestResult result, HighRiskSecurityEventBatchResult batchResult)
    {
        _isRequesting = false;
        RefreshRequestButtons();

        if (batchResult == null)
        {
            RefreshStatusLabel("分批请求结束：无汇总结果。");
            RefreshResultList();
            return;
        }

        RefreshStatusLabel(
            $"分批请求完成：成功 {batchResult.SuccessRegionCount}/{batchResult.TotalRegionCount} 省，" +
            $"失败 {batchResult.FailedRegionCount}，总事件 {batchResult.TotalEventCount} 条。");
        RefreshResultList();
    }

    private void HandleRegionRequestCompleted(
        HttpRequestResult result,
        HighRiskSecurityEventResponse response,
        ThreatRegionRequestCodes regionCodes)
    {
        if (!_isRequesting)
        {
            return;
        }

        _batchCompletedRegionCount++;
        int eventCount = response?.data != null ? response.data.Length : 0;
        bool success = result != null && result.IsSuccess && response != null && response.IsSuccess;
        string status = success ? "成功" : "失败";
        RefreshStatusLabel(
            $"分批请求进度 {_batchCompletedRegionCount}/{Mathf.Max(_batchTotalRegionCount, 1)}：" +
            $"{regionCodes.FirstClassCode} {status}，本批 {eventCount} 条");
        RefreshResultList();
    }

    private void HandleBatchCompleted(HighRiskSecurityEventBatchResult batchResult)
    {
        RefreshResultList();
    }

    private void HandleDataStoreChanged()
    {
        RefreshResultList();
    }

    private void HandleProvinceAlertStarted(ThreatProvinceAlertContext context)
    {
        string province = context?.ProvinceCode ?? "(未知)";
        int count = context?.Events?.Count ?? 0;
        RefreshStatusLabel($"告警处理中：province={province}，事件数={count}，播放状态=Threat");
        RefreshResultList();
    }

    private void HandleProvinceAlertCompleted(ThreatProvinceAlertContext context)
    {
        string province = context?.ProvinceCode ?? "(未知)";
        RefreshStatusLabel($"省级告警已结束：province={province}，可继续处理队列。");
        RefreshResultList();
    }

    private void HandleAllProvinceAlertsCompleted()
    {
        RefreshStatusLabel("全部达标省份告警流程已结束。");
        RefreshResultList();
    }

    private void OnCompleteAlertClicked()
    {
        ThreatProvinceAlertController.CompleteCurrentProvinceAlert();
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[ThreatHighRiskSecurityEventUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void RefreshResultList()
    {
        if (_resultListText == null)
        {
            return;
        }

        _resultListText.text = BuildResultListText();
        RefreshResultScrollLayout();
    }

    private string BuildResultListText()
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("【汇总】");
        builder.AppendLine($"总事件数：{store.Count}");
        builder.AppendLine($"覆盖省份数：{store.ProvinceGroupCount}");
        builder.AppendLine($"告警阈值：>={ThreatAlertSettings.EventsPerProvinceThreshold} 条/省");
        builder.AppendLine(
            $"告警状态：{(ThreatProvinceAlertController.IsProcessing ? "处理中" : "空闲")}，" +
            $"当前省={ThreatProvinceAlertController.CurrentProvinceCode ?? "-"}");
        builder.AppendLine();

        IReadOnlyList<string> qualifiedProvinces =
            store.GetProvincesMeetingThreshold(ThreatAlertSettings.EventsPerProvinceThreshold);
        if (qualifiedProvinces.Count > 0)
        {
            builder.AppendLine("【达标省份】");
            for (int i = 0; i < qualifiedProvinces.Count; i++)
            {
                string provinceCode = qualifiedProvinces[i];
                builder.AppendLine($"  {FormatProvinceHeader(provinceCode, store.GetProvinceEventCount(provinceCode))}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("【按省明细】");
        IReadOnlyList<string> allProvinceCodes = store.GetProvincesMeetingThreshold(1);
        if (allProvinceCodes.Count == 0)
        {
            builder.AppendLine("(暂无数据，请先请求接口)");
            return builder.ToString();
        }

        const int maxEventsPerProvince = 15;
        for (int i = 0; i < allProvinceCodes.Count; i++)
        {
            string provinceCode = allProvinceCodes[i];
            IReadOnlyList<HighRiskSecurityEventItem> events = store.GetEventsByProvince(provinceCode);
            builder.AppendLine(FormatProvinceHeader(provinceCode, events.Count));

            int displayCount = Mathf.Min(events.Count, maxEventsPerProvince);
            for (int j = 0; j < displayCount; j++)
            {
                builder.AppendLine("  " + FormatEventLine(events[j]));
            }

            if (events.Count > maxEventsPerProvince)
            {
                builder.AppendLine($"  … 另有 {events.Count - maxEventsPerProvince} 条未显示");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatProvinceHeader(string provinceCode, int count)
    {
        string provinceName = provinceCode;
        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string name))
        {
            provinceName = $"{provinceCode} {name}";
        }

        return $"[{provinceName}] {count} 条";
    }

    private static string FormatEventLine(HighRiskSecurityEventItem item)
    {
        if (item == null)
        {
            return "(空事件)";
        }

        return
            $"eventId={item.eventId} vin={item.vin} level={item.eventLevel} " +
            $"time={item.processTime} lon={item.longitude} lat={item.latitude}";
    }

    private void RefreshStatusLabel(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = text;
        }
    }

    private void RefreshRequestButtons()
    {
        bool busy = _isRequesting || HighRiskSecurityEventApi.IsBatchRequesting;

        if (_requestAllProvincesButton != null)
        {
            _requestAllProvincesButton.interactable = !busy;
        }

        if (_requestSingleProvinceButton != null)
        {
            _requestSingleProvinceButton.interactable = !busy;
        }

        if (_completeAlertButton != null)
        {
            _completeAlertButton.interactable = ThreatProvinceAlertController.IsProcessing;
        }
    }

    private void RefreshResultScrollLayout()
    {
        if (_resultListText == null)
        {
            return;
        }

        RectTransform textRect = _resultListText.rectTransform;
        float width = textRect.parent is RectTransform parentRect
            ? parentRect.rect.width
            : 320f;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(100f, width - 16f));

        float preferredHeight = LayoutUtility.GetPreferredHeight(textRect);
        if (preferredHeight < 32f)
        {
            preferredHeight = _resultListText.preferredHeight;
        }

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(32f, preferredHeight));

        if (_resultScroll != null && _resultScroll.content != null)
        {
            _resultScroll.content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(32f, preferredHeight + 8f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_resultScroll.content);
            _resultScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void EnsureReferences()
    {
        if (_startTimeInput == null)
        {
            _startTimeInput = transform.Find("StartTimeInputRow/InputField")?.GetComponent<InputField>()
                ?? transform.Find("StartTimeInputRow/Input")?.GetComponent<InputField>()
                ?? transform.Find("StartTimeInputRow")?.GetComponentInChildren<InputField>(true);
        }

        if (_endTimeInput == null)
        {
            _endTimeInput = transform.Find("EndTimeInputRow/InputField")?.GetComponent<InputField>()
                ?? transform.Find("EndTimeInputRow/Input")?.GetComponent<InputField>()
                ?? transform.Find("EndTimeInputRow")?.GetComponentInChildren<InputField>(true);
        }

        if (_provinceCodeInput == null)
        {
            _provinceCodeInput = transform.Find("ProvinceCodeInputRow/InputField")?.GetComponent<InputField>()
                ?? transform.Find("ProvinceCodeInputRow/Input")?.GetComponent<InputField>()
                ?? transform.Find("ProvinceCodeInputRow")?.GetComponentInChildren<InputField>(true);
        }

        if (_statusLabel == null)
        {
            _statusLabel = transform.Find("StatusLabel")?.GetComponent<Text>();
        }

        if (_resultScroll == null)
        {
            _resultScroll = transform.Find("ResultScrollView")?.GetComponent<ScrollRect>();
        }

        if (_resultListText == null && _resultScroll != null)
        {
            _resultListText = _resultScroll.content?.Find("ResultListText")?.GetComponent<Text>();
        }
    }

    private static string GetInputText(InputField input, string fallback)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.text))
        {
            return fallback;
        }

        return input.text.Trim();
    }

    private static string ResolveStartTimeFromInput(InputField input)
    {
        return ThreatQueryDefaults.ResolveStartTime(input != null ? input.text : null);
    }

    private static string ResolveEndTimeFromInput(InputField input)
    {
        return ThreatQueryDefaults.ResolveEndTime(input != null ? input.text : null);
    }

    private string ResolveStartTimeFromInput()
    {
        return ResolveStartTimeFromInput(_startTimeInput);
    }

    private string ResolveEndTimeFromInput()
    {
        return ResolveEndTimeFromInput(_endTimeInput);
    }

    private static void SetInputText(InputField input, string text)
    {
        if (input == null)
        {
            return;
        }

        input.text = text ?? string.Empty;
        if (input.textComponent != null)
        {
            input.textComponent.text = input.text;
        }

        ThreatDemoUiStyle.ApplyInputField(input);
    }

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
