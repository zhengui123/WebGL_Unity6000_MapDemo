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


    public void OpenCarUI(string start3DObjectName)
    {
        if (string.IsNullOrEmpty(start3DObjectName))
        {
            start3DObjectName = _defaultStart3DObjectName;
        }

        CarPanel.SetActive(true);
        gridLine.enabled = true;
        _currentStart3DObjectName = start3DObjectName;
        gridLine.PlayDrawAnimation(start3DObjectName);
    }

    public void CloseCarUI()
    {
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
