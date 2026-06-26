using UnityEngine;

/// <summary>
/// Demo GameState UI 导航：在上层菜单与子功能面板之间切换显隐。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateUINavigator : MonoBehaviour
{
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _controlStateJumpPanel;
    [SerializeField] private GameObject _plateMapHighlightPanel;
    [SerializeField] private GameObject _vehicleHeatmapUpdatePanel;
    [SerializeField] private GameObject _carPanelUiPanel;

    private void Awake()
    {
        ShowMenu();
    }

    public void ShowMenu()
    {
        SetPanelActive(_menuPanel, true);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
    }

    public void ShowControlStateJumpPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, true);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
    }

    public void ShowPlateMapHighlightPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, true);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
    }

    public void ShowVehicleHeatmapUpdatePanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, true);
        SetPanelActive(_carPanelUiPanel, false);
    }

    public void ShowCarPanelUiPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, true);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
