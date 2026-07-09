using UnityEngine;

/// <summary>
/// GJ_Panel 告警面板测试：快捷键或 ContextMenu 拉取首条告警并展示。
/// </summary>
[DisallowMultipleComponent]
public class GJPanelDemo : MonoBehaviour
{
    [SerializeField] private GJPanel _panel;
    [SerializeField] private bool _runOnStart;
    [SerializeField] private KeyCode _requestKey = KeyCode.G;

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
            RequestAndShow();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_requestKey))
        {
            RequestAndShow();
        }
    }

    [ContextMenu("请求告警首条并显示 GJ_Panel")]
    public void RequestAndShow()
    {
        GJPanel panel = ResolvePanel();
        if (panel == null)
        {
            Debug.LogWarning("[GJPanelDemo] 未找到 GJPanel。");
            return;
        }

        panel.RequestAndShowFirstEvent();
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
