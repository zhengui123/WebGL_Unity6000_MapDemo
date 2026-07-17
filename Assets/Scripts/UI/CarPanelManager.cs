using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarPanelManager : UnitySingle<CarPanelManager>
{
    public GameObject CarPanel;
    public GridLine gridLine;

    [SerializeField] private string _defaultStart3DObjectName;

    [Header("消息面板")]
    [Tooltip("车辆弹窗消息列表；留空则从 GridLine 当前 endUI / CarPanel 子级查找")]
    [SerializeField] private MessageListPanel _messageListPanel;

    [Header("零部件轮播")]
    [Tooltip("每个零部件在面板上停留的秒数")]
    [SerializeField] private float _partDisplaySeconds = 5f;

    private string _currentStart3DObjectName;
    private Coroutine _partCarouselCoroutine;
    private List<CarVehiclePartSlide> _activeSlides;
    private int _carouselIndex;
    private System.Action _pendingCloseCallback;

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
        StopPartMessageCarousel();

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
    /// 从缓存读取零部件列表，打开车辆 UI 并开始轮播。
    /// 每次切换：先关闭当前 UI/连线，再用新 partTypeName 重新打开。
    /// </summary>
    public bool StartPartMessageCarouselFromCache()
    {
        List<CarVehiclePartSlide> slides = CarVehicleDataStore.Instance.BuildPartSlides();
        if (slides.Count == 0)
        {
            Debug.LogWarning("[CarPanelManager] 无零部件可轮播。");
            return false;
        }

        return StartPartMessageCarousel(slides);
    }

    /// <summary>
    /// 打开首个零部件 UI，并按顺序轮播；切换时先关再开。
    /// </summary>
    public bool StartPartMessageCarousel(IList<CarVehiclePartSlide> slides)
    {
        if (slides == null || slides.Count == 0)
        {
            Debug.LogWarning("[CarPanelManager] 轮播数据为空。");
            return false;
        }

        CarVehiclePartSlide first = slides[0];
        bool opened = OpenCarUIWithMessageList(
            first.PartTypeName,
            first.PartTypeName,
            first.ProtectionState,
            new List<string>(first.EventNames));
        if (!opened)
        {
            return false;
        }

        StopPartMessageCarousel();
        _activeSlides = new List<CarVehiclePartSlide>(slides);
        _carouselIndex = 0;

        if (_activeSlides.Count > 1)
        {
            _partCarouselCoroutine = StartCoroutine(PartMessageCarouselLoop());
        }

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

    /// <summary>仅刷新消息面板文字，不改动连线。</summary>
    public void RefreshMessageList(
        string title,
        ProtectionStateType protectionState,
        System.Collections.Generic.IList<string> abnormalEvents)
    {
        MessageListPanel panel = ResolveMessageListPanel();
        if (panel == null)
        {
            Debug.LogWarning("[CarPanelManager] 未找到 MessageListPanel，跳过消息列表刷新。");
            return;
        }

        panel.SetMessageList(title, protectionState, abnormalEvents);
    }

    public void StopPartMessageCarousel()
    {
        if (_partCarouselCoroutine != null)
        {
            StopCoroutine(_partCarouselCoroutine);
            _partCarouselCoroutine = null;
        }

        _activeSlides = null;
        _carouselIndex = 0;
        _pendingCloseCallback = null;
    }

    public void CloseCarUI()
    {
        StopPartMessageCarousel();
        CloseCarUIPanel(null);
    }

    /// <summary>收起车辆 UI 与连线；轮播切换时传入 onComplete，不停止轮播协程。</summary>
    private void CloseCarUIPanel(System.Action onComplete)
    {
        _pendingCloseCallback = onComplete;

        if (!IsVehicleLevel())
        {
            Debug.LogWarning("[CarPanelManager] 当前非 VehicleLevel，无法关闭车辆 UI。");
            InvokePendingCloseCallback();
            return;
        }

        if (gridLine == null || !gridLine.enabled)
        {
            SetPanelInactive();
            InvokePendingCloseCallback();
            return;
        }

        string targetName = string.IsNullOrEmpty(_currentStart3DObjectName)
            ? gridLine.ActiveStart3DName
            : _currentStart3DObjectName;

        if (string.IsNullOrEmpty(targetName))
        {
            SetPanelInactive();
            InvokePendingCloseCallback();
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
        InvokePendingCloseCallback();
    }

    private void InvokePendingCloseCallback()
    {
        System.Action callback = _pendingCloseCallback;
        _pendingCloseCallback = null;
        callback?.Invoke();
    }

    private IEnumerator PartMessageCarouselLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, _partDisplaySeconds));
        while (_activeSlides != null && _activeSlides.Count > 1)
        {
            yield return wait;
            if (_activeSlides == null || _activeSlides.Count <= 1)
            {
                break;
            }

            _carouselIndex = (_carouselIndex + 1) % _activeSlides.Count;
            CarVehiclePartSlide slide = _activeSlides[_carouselIndex];
            yield return SwitchCarouselSlide(slide);
        }

        _partCarouselCoroutine = null;
    }

    /// <summary>轮播切换：先关闭当前 UI，再用新部件名重新打开。</summary>
    private IEnumerator SwitchCarouselSlide(CarVehiclePartSlide slide)
    {
        bool closed = false;
        CloseCarUIPanel(() => closed = true);
        yield return new WaitUntil(() => closed);

        if (_activeSlides == null)
        {
            yield break;
        }

        OpenCarUIWithMessageList(
            slide.PartTypeName,
            slide.PartTypeName,
            slide.ProtectionState,
            new List<string>(slide.EventNames));
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
