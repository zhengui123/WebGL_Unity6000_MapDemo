using UnityEngine;

/// <summary>
/// GJ_Panel 告警面板 Demo：调用 <see cref="SecurityEventDetailApi"/>，通过事件刷新面板。
/// </summary>
[DisallowMultipleComponent]
public class GJPanelDemo : MonoBehaviour
{
    [SerializeField] private GJPanel _panel;
    [SerializeField] private bool _runOnStart;
    [SerializeField] private KeyCode _requestKey = KeyCode.G;

    [Header("请求参数（留空则使用 SecurityEventDetailRequest 默认值）")]
    [SerializeField] private string _eventId = SecurityEventDetailRequest.DefaultEventId;
    [SerializeField] private string _processStartTime = SecurityEventDetailRequest.DefaultProcessStartTime;
    [SerializeField] private string _processEndTime = SecurityEventDetailRequest.DefaultProcessEndTime;
    [SerializeField] private bool _passwd;

    private void Awake()
    {
        if (_panel == null)
        {
            _panel = GetComponent<GJPanel>();
        }

        if (_panel == null)
        {
            _panel = GJPanel.Instance;
        }
    }

    private void OnEnable()
    {
        SecurityEventDetailApi.RequestCompleted += OnSecurityEventDetailRequestCompleted;
    }

    private void OnDisable()
    {
        SecurityEventDetailApi.RequestCompleted -= OnSecurityEventDetailRequestCompleted;
    }

    private void Start()
    {
        if (_runOnStart)
        {
            RequestDetail();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_requestKey))
        {
            RequestDetail();
        }
    }

    [ContextMenu("请求告警详情")]
    public void RequestDetail()
    {
        SecurityEventDetailApi.Request(_eventId, _processStartTime, _processEndTime, _passwd);
    }

    [ContextMenu("隐藏 GJ_Panel")]
    public void HidePanel()
    {
        ResolvePanel()?.HidePanel();
    }

    private void OnSecurityEventDetailRequestCompleted(
        HttpRequestResult result,
        SecurityEventDetailResponse response)
    {
        if (result == null)
        {
            Debug.LogWarning("[GJPanelDemo] 请求结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            Debug.Log("[GJPanelDemo] 请求已取消。");
            return;
        }

        if (!result.IsSuccess)
        {
            Debug.LogWarning($"[GJPanelDemo] 请求失败：{result.Error}");
            return;
        }

        GJPanel panel = ResolvePanel();
        if (panel == null)
        {
            Debug.LogWarning("[GJPanelDemo] 未找到 GJPanel，无法刷新面板。");
            return;
        }

        if (response == null || !response.IsSuccess || response.data == null)
        {
            Debug.LogWarning("[GJPanelDemo] 业务响应无效，未刷新面板。");
            return;
        }

        panel.ApplyDetailData(response.data, showPanel: true);
        Debug.Log($"[GJPanelDemo] 已刷新面板：{response.data.event_name} / {response.data.vin}");
    }

    private GJPanel ResolvePanel()
    {
        if (_panel != null)
        {
            return _panel;
        }

        _panel = GJPanel.Instance;
        return _panel;
    }
}
