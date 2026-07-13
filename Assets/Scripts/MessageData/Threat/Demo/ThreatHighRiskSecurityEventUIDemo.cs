using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 威胁态势高危安全事件接口 Demo：全国单次请求，列表展示各省事件条数。
/// </summary>
[DisallowMultipleComponent]
public class ThreatHighRiskSecurityEventUIDemo : MonoBehaviour
{
    [Header("查询参数")]
    [SerializeField] private InputField _startTimeInput;
    [SerializeField] private InputField _endTimeInput;

    [Header("操作")]
    [SerializeField] private Button _requestNationalButton;
    [SerializeField] private Button _refreshListButton;
    [SerializeField] private Button _completeAlertButton;
    [SerializeField] private Button _backButton;

    [Header("展示")]
    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _resultListText;
    [SerializeField] private ScrollRect _resultScroll;

    [SerializeField] private DemoGameStateUINavigator _navigator;

    private bool _isRequesting;

    private void Awake()
    {
        EnsureReferences();

        if (_requestNationalButton != null)
        {
            _requestNationalButton.onClick.AddListener(OnRequestNationalClicked);
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
        HighRiskSecurityEventApi.BatchCompleted += HandleBatchCompleted;
        HighRiskSecurityEventDataStore.Instance.DataChanged += HandleDataStoreChanged;
        ThreatProvinceAlertController.ProvinceAlertStarted += HandleProvinceAlertStarted;
        ThreatProvinceAlertController.ProvinceAlertCompleted += HandleProvinceAlertCompleted;
        ThreatProvinceAlertController.AllProvinceAlertsCompleted += HandleAllProvinceAlertsCompleted;

        ApplyDefaultInputs();
        ApplyRuntimeTextStyle();
        RefreshEffectiveTimeHint();
        RefreshStatusLabel("就绪：点击「请求全国」拉取数据。");
        RefreshResultList();
        RefreshRequestButtons();
    }

    private void OnDisable()
    {
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
        if (_requestNationalButton != null)
        {
            _requestNationalButton.onClick.RemoveListener(OnRequestNationalClicked);
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
    }

    private void RefreshEffectiveTimeHint()
    {
        if (_startTimeInput != null && string.IsNullOrWhiteSpace(_startTimeInput.text))
        {
            SetInputText(_startTimeInput, ResolveStartTimeFromInput());
        }

        if (_endTimeInput != null && string.IsNullOrWhiteSpace(_endTimeInput.text))
        {
            SetInputText(_endTimeInput, ResolveEndTimeFromInput());
        }
    }

    private void ApplyRuntimeTextStyle()
    {
        ThreatDemoUiStyle.ApplyPanelLabel(_statusLabel);
        ThreatDemoUiStyle.ApplyResultText(_resultListText);
        ThreatDemoUiStyle.ApplyInputField(_startTimeInput);
        ThreatDemoUiStyle.ApplyInputField(_endTimeInput);
    }

    private void OnRequestNationalClicked()
    {
        if (_isRequesting || HighRiskSecurityEventApi.IsBatchRequesting)
        {
            RefreshStatusLabel("已有请求进行中，请等待完成。");
            return;
        }

        _isRequesting = true;
        RefreshRequestButtons();
        RefreshStatusLabel("全国请求中…");
        RefreshResultList();

        HighRiskSecurityEventApi.RequestAllDomesticProvinces(
            ResolveStartTimeFromInput(),
            ResolveEndTimeFromInput(),
            OnNationalRequestCompleted);
    }

    private void OnNationalRequestCompleted(HttpRequestResult result, HighRiskSecurityEventBatchResult batchResult)
    {
        _isRequesting = false;
        RefreshRequestButtons();

        if (batchResult == null)
        {
            RefreshStatusLabel("全国请求结束：无汇总结果。");
            RefreshResultList();
            return;
        }

        if (batchResult.FailedRegionCount > 0 || batchResult.SuccessRegionCount == 0)
        {
            string error = result != null && !string.IsNullOrWhiteSpace(result.Error) ? result.Error : "请求失败";
            RefreshStatusLabel($"全国请求失败：{error}");
        }
        else
        {
            RefreshStatusLabel(
                $"全国请求成功：共 {batchResult.TotalEventCount} 条，覆盖 {HighRiskSecurityEventDataStore.Instance.ProvinceGroupCount} 个省。");
        }

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
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("【汇总】");
        builder.AppendLine($"总事件数：{store.Count}");
        builder.AppendLine($"覆盖省份数：{store.ProvinceGroupCount}");
        builder.AppendLine($"告警阈值：>={ThreatAlertSettings.EventsPerProvinceThreshold} 条/省");
        builder.AppendLine(
            $"告警状态：{(ThreatProvinceAlertController.IsProcessing ? "处理中" : "空闲")}，" +
            $"当前省={ThreatProvinceAlertController.CurrentProvinceCode ?? "-"}");
        builder.AppendLine();

        IReadOnlyList<string> provinceCodes = store.GetProvincesMeetingThreshold(1);
        builder.AppendLine("【各省数据】（按条数降序）");
        if (provinceCodes == null || provinceCodes.Count == 0)
        {
            builder.AppendLine("(暂无数据，请先请求全国)");
            return builder.ToString();
        }

        List<string> sortedProvinces = new List<string>(provinceCodes);
        sortedProvinces.Sort((a, b) =>
        {
            int countCompare = store.GetProvinceEventCount(b).CompareTo(store.GetProvinceEventCount(a));
            return countCompare != 0 ? countCompare : string.CompareOrdinal(a, b);
        });

        for (int i = 0; i < sortedProvinces.Count; i++)
        {
            string provinceCode = sortedProvinces[i];
            int count = store.GetProvinceEventCount(provinceCode);
            builder.AppendLine(FormatProvinceLine(provinceCode, count));
        }

        return builder.ToString();
    }

    private static string FormatProvinceLine(string provinceCode, int count)
    {
        if (GaodeProvinceAdcodeConverter.TryAdcodeToProvinceName(provinceCode, out string name))
        {
            return $"{provinceCode}  {name}  {count} 条";
        }

        return $"{provinceCode}  {count} 条";
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

        if (_requestNationalButton != null)
        {
            _requestNationalButton.interactable = !busy;
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

        if (_requestNationalButton == null)
        {
            _requestNationalButton =
                transform.Find("RequestNationalButton")?.GetComponent<Button>()
                ?? transform.Find("RequestAllProvincesButton")?.GetComponent<Button>();
        }

        if (_refreshListButton == null)
        {
            _refreshListButton = transform.Find("RefreshListButton")?.GetComponent<Button>();
        }

        if (_completeAlertButton == null)
        {
            _completeAlertButton = transform.Find("CompleteAlertButton")?.GetComponent<Button>();
        }

        if (_backButton == null)
        {
            _backButton = transform.Find("BackButton")?.GetComponent<Button>();
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

    private string ResolveStartTimeFromInput()
    {
        return ThreatQueryDefaults.ResolveStartTime(_startTimeInput != null ? _startTimeInput.text : null);
    }

    private string ResolveEndTimeFromInput()
    {
        return ThreatQueryDefaults.ResolveEndTime(_endTimeInput != null ? _endTimeInput.text : null);
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
