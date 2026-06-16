using UnityEngine;

public class CarPanelManager : UnitySingle<CarPanelManager>
{
    public GameObject CarPanel;
    public GridLine gridLine;

    [SerializeField] private string _defaultStart3DObjectName;

    private string _currentStart3DObjectName;

    public void Awake()
    {
        CarPanel.SetActive(false);
        gridLine.enabled = false;
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
        CarPanel.SetActive(true);
    }

    public void CloseCarPanel()
    {
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

        CarPanel.SetActive(true);
        gridLine.enabled = true;
        _currentStart3DObjectName = start3DObjectName;
        gridLine.PlayDrawAnimation(start3DObjectName);
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
        CarPanel.SetActive(false);
        if (gridLine != null)
        {
            gridLine.enabled = false;
        }
    }
}
