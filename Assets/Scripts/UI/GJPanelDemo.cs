using UnityEngine;

/// <summary>
/// GJ_Panel 本地快捷测试（按键/ContextMenu）。正式落地由 <see cref="SecurityEventDetailApi"/> 统一处理。
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
    [SerializeField] private int _tenantId = SecurityEventDetailRequest.DefaultTenantId;

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

    /// <summary>
    /// 本地快捷请求；成功后的缓存 / GJ_Panel / POI 由 <see cref="SecurityEventDetailApi"/> 统一处理。
    /// 正式入口请用 Demo 菜单或 WebGL/Android 的 RequestSecurityEventDetail。
    /// </summary>
    [ContextMenu("请求告警详情")]
    public void RequestDetail()
    {
        SecurityEventDetailApi.Request(_eventId, _processStartTime, _processEndTime, null, _tenantId);
    }

    [ContextMenu("隐藏 GJ_Panel")]
    public void HidePanel()
    {
        ResolvePanel()?.HidePanel();
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
