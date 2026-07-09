using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 告警面板（UI/Canvas/GJ_Panel）：显示首条告警事件字段，支持显隐与接口联动。
/// </summary>
[DisallowMultipleComponent]
public class GJPanel : MonoBehaviour
{
    public const string DefaultTitle = "告警事件";

    private static GJPanel _instance;

    [Header("标题")]
    [SerializeField] private Text _titleText;

    [Header("字段值（留空则按子节点名自动查找）")]
    [SerializeField] private Text _eventNameText;
    [SerializeField] private Text _riskNameText;
    [SerializeField] private Text _happenTimeText;
    [SerializeField] private Text _vinText;
    [SerializeField] private Text _partTypeText;
    [SerializeField] private Text _vehicleInfoText;

    [Header("启动行为")]
    [SerializeField] private bool _hideOnAwake = true;
    [SerializeField] private bool _requestOnStart;

    private BasicEventItem _currentEvent;

    /// <summary>场景内告警面板实例（含未激活对象）。</summary>
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

    /// <summary>当前展示的事件数据。</summary>
    public BasicEventItem CurrentEvent => _currentEvent;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[GJPanel] 场景中存在多个 GJPanel，仅首个实例作为 Instance。");
        }

        EnsureReferences();
        if (_hideOnAwake)
        {
            HidePanel();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Start()
    {
        if (_requestOnStart)
        {
            RequestAndShowFirstEvent();
        }
    }

    /// <summary>显示告警面板。</summary>
    public void ShowPanel()
    {
        gameObject.SetActive(true);
    }

    /// <summary>隐藏告警面板。</summary>
    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>面板是否处于激活显示状态。</summary>
    public bool IsVisible => gameObject.activeSelf;

    /// <summary>设置标题文本。</summary>
    public void SetTitle(string title)
    {
        EnsureReferences();
        SetText(_titleText, string.IsNullOrEmpty(title) ? DefaultTitle : title);
    }

    /// <summary>按字段名动态修改面板内容。</summary>
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

    /// <summary>使用告警事件对象刷新全部字段。</summary>
    public void SetEventData(BasicEventItem item)
    {
        _currentEvent = item;
        if (item == null)
        {
            SetFieldValues("-", "-", "-", "-", "-", "-");
            return;
        }

        SetFieldValues(
            item.BuildEventNameDisplay(),
            item.riskTypeName,
            item.happenTime,
            item.vin,
            item.partTypeName,
            item.BuildVehicleInfoDisplay());
    }

    /// <summary>请求告警事件列表并展示首条数据。</summary>
    public void RequestAndShowFirstEvent(
        int pageNo = 1,
        int pageSize = 10,
        string startTime = null,
        string endTime = null,
        Dictionary<string, string> additionalHeaders = null,
        bool showPanelOnSuccess = true)
    {
        BasicEventPageApi.RequestFirstEvent(
            pageNo,
            pageSize,
            startTime,
            endTime,
            (result, item) => OnFirstEventLoaded(result, item, showPanelOnSuccess),
            additionalHeaders);
    }

    /// <summary>使用接口响应刷新面板（取 list 第一条）。</summary>
    public bool ApplyResponse(BasicEventPageResponse response, bool showPanelOnSuccess = true)
    {
        if (response == null || !response.IsSuccess)
        {
            Debug.LogWarning("[GJPanel] 响应为空或业务失败，未刷新面板。");
            return false;
        }

        BasicEventItem first = response.GetFirstEvent();
        if (first == null)
        {
            Debug.LogWarning("[GJPanel] 告警列表为空，未刷新面板。");
            return false;
        }

        SetEventData(first);
        if (showPanelOnSuccess)
        {
            ShowPanel();
        }

        return true;
    }

    private void OnFirstEventLoaded(HttpRequestResult result, BasicEventItem item, bool showPanelOnSuccess)
    {
        if (result == null)
        {
            Debug.LogWarning("[GJPanel] 告警请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            Debug.Log("[GJPanel] 告警请求已取消。");
            return;
        }

        if (!result.IsSuccess)
        {
            Debug.LogWarning($"[GJPanel] 告警请求失败：{result.Error}");
            return;
        }

        if (item == null)
        {
            Debug.LogWarning("[GJPanel] 告警列表无数据。");
            return;
        }

        SetEventData(item);
        if (showPanelOnSuccess)
        {
            ShowPanel();
        }

        Debug.Log($"[GJPanel] 已展示首条告警：{item.eventName} / {item.vin}");
    }

    private void EnsureReferences()
    {
        if (_titleText == null)
        {
            _titleText = FindText("TitleText");
        }

        if (_eventNameText == null)
        {
            _eventNameText = FindText("MessageList/EventName/EventText");
        }

        if (_riskNameText == null)
        {
            _riskNameText = FindText("MessageList/FXName/FXText");
        }

        if (_happenTimeText == null)
        {
            _happenTimeText = FindText("MessageList/TimeName/TimeText");
        }

        if (_vinText == null)
        {
            _vinText = FindText("MessageList/VinName/VinText");
        }

        if (_partTypeText == null)
        {
            _partTypeText = FindText("MessageList/LJName/LJText");
        }

        if (_vehicleInfoText == null)
        {
            _vehicleInfoText = FindText("MessageList/PPName/PPText");
        }
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
