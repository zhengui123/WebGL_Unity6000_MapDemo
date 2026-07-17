using UnityEngine;

public class CarPanelManager : UnitySingle<CarPanelManager>
{
    public GameObject CarPanel;
    public GridLine gridLine;

    [SerializeField] private string _defaultStart3DObjectName;

    [Header("消息面板")]
    [Tooltip("车辆弹窗消息列表；留空则从 GridLine 当前 endUI / CarPanel 子级查找")]
    [SerializeField] private MessageListPanel _messageListPanel;

    private string _currentStart3DObjectName;

    /// <summary>绑定的消息列表面板。</summary>
    public MessageListPanel MessageListPanel => ResolveMessageListPanel();

    public override void Awake()
    {
        base.Awake();

        if (CarPanel != null)
        {
            CarPanel.SetActive(false);
        }

        if (gridLine != null)
        {
            gridLine.enabled = false;
        }
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionCompleted += HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionStarted += HandleVehicleToPlateViewTransitionStarted;
        em.OnVehicleToPartTransitionStarted += HandleVehicleToPartTransitionStarted;
        em.OnVehicleToAttackPathTransitionStarted += HandleVehicleToAttackPathTransitionStarted;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionCompleted -= HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionStarted -= HandleVehicleToPlateViewTransitionStarted;
        em.OnVehicleToPartTransitionStarted -= HandleVehicleToPartTransitionStarted;
        em.OnVehicleToAttackPathTransitionStarted -= HandleVehicleToAttackPathTransitionStarted;
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            OpenCarUI(_defaultStart3DObjectName);
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            CloseCarUI();
        }
    }

    public void OpenCarPanel()
    {
        if (CarPanel == null)
        {
            Debug.LogError("[CarPanelManager] CarPanel 未赋值。");
            return;
        }

        CarPanel.SetActive(true);
    }

    public void CloseCarPanel()
    {
        if (CarPanel == null)
        {
            return;
        }

        CarPanel.SetActive(false);
    }

    public bool OpenCarUI(string start3DObjectName)
    {
        if (!IsVehicleLevel())
        {
            Debug.LogWarning("[CarPanelManager] 当前非 VehicleLevel，无法打开车辆 UI。");
            return false;
        }

        if (string.IsNullOrEmpty(start3DObjectName))
        {
            start3DObjectName = _defaultStart3DObjectName;
        }

        if (CarPanel == null)
        {
            Debug.LogError("[CarPanelManager] CarPanel 未赋值，请使用场景中已配置的 CarPanelManager（如 UI_CarPanelManager）。");
            return false;
        }

        if (gridLine == null)
        {
            Debug.LogError("[CarPanelManager] GridLine 未赋值，请在 Inspector 中绑定 gridLine。");
            return false;
        }

        CarPanel.SetActive(true);
        gridLine.enabled = true;
        _currentStart3DObjectName = start3DObjectName;
        gridLine.PlayDrawAnimation(start3DObjectName);
        return true;
    }

    /// <summary>
    /// 打开车辆 UI，并用防护状态数据刷新消息面板。
    /// start3DObjectName 建议使用首个 unprotectedParts.partTypeName（如 IDC）。
    /// </summary>
    public bool OpenCarUIWithMessageList(
        string start3DObjectName,
        string title,
        ProtectionStateType protectionState,
        System.Collections.Generic.IList<string> abnormalEvents)
    {
        bool opened = OpenCarUI(start3DObjectName);
        if (!opened)
        {
            return false;
        }

        MessageListPanel panel = ResolveMessageListPanel(start3DObjectName);
        if (panel == null)
        {
            Debug.LogWarning("[CarPanelManager] 未找到 MessageListPanel，跳过消息列表刷新。");
            return true;
        }

        panel.SetMessageList(title, protectionState, abnormalEvents);
        return true;
    }

    public void CloseCarUI()
    {
        if (!IsVehicleLevel())
        {
            Debug.LogWarning("[CarPanelManager] 当前非 VehicleLevel，无法关闭车辆 UI。");
            return;
        }

        if (gridLine == null || !gridLine.enabled)
        {
            SetPanelInactive();
            return;
        }

        string targetName = string.IsNullOrEmpty(_currentStart3DObjectName)
            ? gridLine.ActiveStart3DName
            : _currentStart3DObjectName;

        if (string.IsNullOrEmpty(targetName))
        {
            SetPanelInactive();
            return;
        }

        gridLine.PlayReverseAnimation(targetName, OnGridLineReverseCompleted);
    }

    private void HandlePlateToVehicleViewTransitionCompleted(string provinceName)
    {
        OpenCarPanel();
    }

    private void HandleVehicleToPlateViewTransitionStarted(string provinceName)
    {
        CloseCarUI();
    }

    private void HandleVehicleToPartTransitionStarted(string partName)
    {
        CloseCarUI();
    }

    private void HandleVehicleToAttackPathTransitionStarted()
    {
        CloseCarUI();
    }

    private static bool IsVehicleLevel()
    {
        GameManager manager = GameManager.Instance;
        return manager != null && manager.CurrentState == GameManager.ControlState.VehicleLevel;
    }

    private void OnGridLineReverseCompleted()
    {
        _currentStart3DObjectName = null;
        SetPanelInactive();
    }

    private void SetPanelInactive()
    {
        if (CarPanel != null)
        {
            CarPanel.SetActive(false);
        }

        if (gridLine != null)
        {
            gridLine.enabled = false;
        }
    }

    private MessageListPanel ResolveMessageListPanel(string start3DObjectName = null)
    {
        if (_messageListPanel != null)
        {
            return _messageListPanel;
        }

        if (gridLine != null)
        {
            if (!string.IsNullOrEmpty(start3DObjectName))
            {
                MessageListPanel fromBinding = gridLine.GetEndMessageListPanel(start3DObjectName);
                if (fromBinding != null)
                {
                    return fromBinding;
                }
            }

            MessageListPanel active = gridLine.ActiveEndMessageListPanel;
            if (active != null)
            {
                return active;
            }
        }

        if (CarPanel != null)
        {
            return CarPanel.GetComponentInChildren<MessageListPanel>(true);
        }

        return FindFirstObjectByType<MessageListPanel>(FindObjectsInactive.Include);
    }
}
