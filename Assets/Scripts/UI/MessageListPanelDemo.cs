using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="MessageListPanel"/> 调用测试：快捷键或 ContextMenu 刷新面板内容。
/// 建议挂在 Canvas/CarPanel/CarImg 同级或子物体，并拖入 MessageListPanel 引用。
/// </summary>
[DisallowMultipleComponent]
public class MessageListPanelDemo : MonoBehaviour
{
    public const string DefaultTitle = "TBOX-安全事态";

    private static readonly string[] SampleProtectedEvents =
    {
        "系统资源信息报警",
        "业务log异常",
        "进程异常事件",
        "网络连接信息报警",
        "网卡资源异常",
        "多余事件（不应显示）",
    };

    private static readonly string[] SampleUnprotectedEvents =
    {
        "防火墙未启用",
        "入侵检测告警",
        "异常外联请求",
    };

    [Header("目标面板")]
    [SerializeField] private MessageListPanel _messageListPanel;

    [Header("测试数据")]
    [SerializeField] private string _title = DefaultTitle;
    [SerializeField] private ProtectionStateType _protectionState = ProtectionStateType.Protected;

    [Header("触发")]
    [SerializeField] private bool _runOnStart = true;
    [SerializeField] private KeyCode _applyKey = KeyCode.M;
    [SerializeField] private KeyCode _toggleStateKey = KeyCode.N;

    private void Awake()
    {
        if (_messageListPanel == null)
        {
            _messageListPanel = GetComponent<MessageListPanel>();
        }

        if (_messageListPanel == null)
        {
            _messageListPanel = GetComponentInChildren<MessageListPanel>(true);
        }
    }

    private void Start()
    {
        if (_runOnStart)
        {
            ApplySample();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_applyKey))
        {
            ApplySample();
        }

        if (Input.GetKeyDown(_toggleStateKey))
        {
            ToggleProtectionState();
        }
    }

    [ContextMenu("应用当前样本到 MessageListPanel")]
    public void ApplySample()
    {
        MessageListPanel panel = ResolvePanel();
        if (panel == null)
        {
            Debug.LogWarning("[MessageListPanelDemo] 未找到 MessageListPanel，请拖入引用或挂到 CarImg 上。");
            return;
        }

        IList<string> events = _protectionState == ProtectionStateType.Protected
            ? SampleProtectedEvents
            : SampleUnprotectedEvents;

        panel.SetMessageList(_title, _protectionState, events);
        Debug.Log($"[MessageListPanelDemo] 已刷新：{_title} / {_protectionState} / 事件数 {events.Count}（最多显示 {MessageListPanel.MaxMessageCount} 条）。");
    }

    [ContextMenu("切换防护状态并刷新")]
    public void ToggleProtectionState()
    {
        _protectionState = _protectionState == ProtectionStateType.Protected
            ? ProtectionStateType.Unprotected
            : ProtectionStateType.Protected;
        ApplySample();
    }

    private MessageListPanel ResolvePanel()
    {
        if (_messageListPanel != null)
        {
            return _messageListPanel;
        }

        _messageListPanel = GetComponent<MessageListPanel>();
        if (_messageListPanel != null)
        {
            return _messageListPanel;
        }

        _messageListPanel = GetComponentInChildren<MessageListPanel>(true);
        return _messageListPanel;
    }
}
