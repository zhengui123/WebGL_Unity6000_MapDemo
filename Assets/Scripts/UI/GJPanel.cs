using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 该面板数据使用外网数据，后面需调整内网
/// 告警面板（UI/Canvas/GJ_Panel）：仅负责显隐与字段赋值，不发起 HTTP 请求。
/// 固定标签随 <see cref="LanguageManager"/> 切换；字段值（后端数据）不翻译。
/// </summary>
[DisallowMultipleComponent]
public class GJPanel : MonoBehaviour
{
    private static GJPanel _instance;

    [Header("标题")]
    [SerializeField] private Text _titleText;

    [Header("字段标签（随语言切换；留空则按节点名自动查找）")]
    [SerializeField] private Text _eventNameLabel;
    [SerializeField] private Text _riskNameLabel;
    [SerializeField] private Text _happenTimeLabel;
    [SerializeField] private Text _vinLabel;
    [SerializeField] private Text _partTypeLabel;
    [SerializeField] private Text _vehicleInfoLabel;

    [Header("字段值（留空则按子节点名自动查找；后端数据不翻译）")]
    [SerializeField] private Text _eventNameText;
    [SerializeField] private Text _riskNameText;
    [SerializeField] private Text _happenTimeText;
    [SerializeField] private Text _vinText;
    [SerializeField] private Text _partTypeText;
    [SerializeField] private Text _vehicleInfoText;

    [Header("启动行为")]
    [SerializeField] private bool _hideOnAwake = true;

    private SecurityEventDetailData _currentEvent;
    private bool _hasCustomTitle;

    public static GJPanel Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            GJPanel[] panels = FindObjectsByType<GJPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return panels.Length > 0 ? panels[0] : null;
        }
    }

    public SecurityEventDetailData CurrentEvent => _currentEvent;

    /// <summary>当前语言下的默认标题。</summary>
    public static string LocalizedDefaultTitle => LanguageManager.Get(UiTextKeys.GjTitle);

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            LogManager.LogFeatureWarning("[GJPanel] 场景中存在多个 GJPanel，仅首个实例作为 Instance。");
        }

        EnsureReferences();
        if (_hideOnAwake)
        {
            HidePanel();
        }
    }

    private void OnEnable()
    {
        LanguageManager.OnLanguageChanged += HandleLanguageChanged;
        ApplyLocalizedLabels();
    }

    private void OnDisable()
    {
        LanguageManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    public bool IsVisible => gameObject.activeSelf;

    public void SetTitle(string title)
    {
        EnsureReferences();
        if (string.IsNullOrEmpty(title))
        {
            _hasCustomTitle = false;
            SetText(_titleText, LocalizedDefaultTitle);
            return;
        }

        _hasCustomTitle = true;
        SetText(_titleText, title);
    }

    public void SetFieldValues(
        string eventName = null,
        string riskName = null,
        string happenTime = null,
        string vin = null,
        string partType = null,
        string vehicleInfo = null)
    {
        EnsureReferences();
        if (eventName != null) SetText(_eventNameText, eventName);
        if (riskName != null) SetText(_riskNameText, riskName);
        if (happenTime != null) SetText(_happenTimeText, happenTime);
        if (vin != null) SetText(_vinText, vin);
        if (partType != null) SetText(_partTypeText, partType);
        if (vehicleInfo != null) SetText(_vehicleInfoText, vehicleInfo);
    }

    public void SetEventData(SecurityEventDetailData data)
    {
        _currentEvent = data;
        if (data == null)
        {
            SetFieldValues("-", "-", "-", "-", "-", "-");
            return;
        }

        SetFieldValues(
            data.BuildEventNameDisplay(),
            data.risk_type_name,
            data.happen_time,
            data.vin,
            data.part_type_name,
            data.BuildVehicleInfoDisplay());
    }

    /// <summary>使用响应 data 赋值并显示面板。</summary>
    public bool ApplyDetailData(SecurityEventDetailData data, bool showPanel = true)
    {
        if (data == null)
        {
            LogManager.LogFeatureWarning("[GJPanel] data 为空，未刷新面板。");
            return false;
        }

        SetEventData(data);
        if (!_hasCustomTitle)
        {
            SetTitle(null);
        }

        if (showPanel)
        {
            ShowPanel();
        }

        return true;
    }

    /// <summary>使用完整响应赋值并显示面板。</summary>
    public bool ApplyResponse(SecurityEventDetailResponse response, bool showPanel = true)
    {
        if (response == null || !response.IsSuccess || response.data == null)
        {
            LogManager.LogFeatureWarning("[GJPanel] 响应为空或业务失败，未刷新面板。");
            return false;
        }

        return ApplyDetailData(response.data, showPanel);
    }

    private void HandleLanguageChanged(UiLanguage _)
    {
        ApplyLocalizedLabels();
    }

    private void ApplyLocalizedLabels()
    {
        EnsureReferences();
        SetText(_eventNameLabel, LanguageManager.Get(UiTextKeys.GjLabelEventName));
        SetText(_riskNameLabel, LanguageManager.Get(UiTextKeys.GjLabelRiskName));
        SetText(_happenTimeLabel, LanguageManager.Get(UiTextKeys.GjLabelHappenTime));
        SetText(_vinLabel, LanguageManager.Get(UiTextKeys.GjLabelVin));
        SetText(_partTypeLabel, LanguageManager.Get(UiTextKeys.GjLabelPartType));
        SetText(_vehicleInfoLabel, LanguageManager.Get(UiTextKeys.GjLabelVehicleInfo));

        if (!_hasCustomTitle)
        {
            SetText(_titleText, LocalizedDefaultTitle);
        }
    }

    private void EnsureReferences()
    {
        if (_titleText == null) _titleText = FindText("TitleText");

        if (_eventNameLabel == null) _eventNameLabel = FindText("MessageList/EventName");
        if (_riskNameLabel == null) _riskNameLabel = FindText("MessageList/FXName");
        if (_happenTimeLabel == null) _happenTimeLabel = FindText("MessageList/TimeName");
        if (_vinLabel == null) _vinLabel = FindText("MessageList/VinName");
        if (_partTypeLabel == null) _partTypeLabel = FindText("MessageList/LJName");
        if (_vehicleInfoLabel == null) _vehicleInfoLabel = FindText("MessageList/PPName");

        if (_eventNameText == null) _eventNameText = FindText("MessageList/EventName/EventText");
        if (_riskNameText == null) _riskNameText = FindText("MessageList/FXName/FXText");
        if (_happenTimeText == null) _happenTimeText = FindText("MessageList/TimeName/TimeText");
        if (_vinText == null) _vinText = FindText("MessageList/VinName/VinText");
        if (_partTypeText == null) _partTypeText = FindText("MessageList/LJName/LJText");
        if (_vehicleInfoText == null) _vehicleInfoText = FindText("MessageList/PPName/PPText");
    }

    private Text FindText(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }
}
