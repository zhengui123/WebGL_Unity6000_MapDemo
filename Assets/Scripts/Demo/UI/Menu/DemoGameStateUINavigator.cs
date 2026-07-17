using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo GameState UI 导航：在上层菜单与子功能面板之间切换显隐。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateUINavigator : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _controlStateJumpPanel;
    [SerializeField] private GameObject _plateMapHighlightPanel;
    [SerializeField] private GameObject _vehicleHeatmapUpdatePanel;
    [SerializeField] private GameObject _carPanelUiPanel;
    [SerializeField] private GameObject _previousLevelPanel;
    [SerializeField] private GameObject _bigScreenCarouselPanel;
    [SerializeField] private GameObject _httpApiTestPanel;
    [SerializeField] private GameObject _androidBridgeApiPanel;
    [SerializeField] private GameObject _threatHighRiskSecurityEventPanel;
    [SerializeField] private GameObject _threatLocalAlertTestPanel;
    [SerializeField] private GameObject _carVehicleDataPanel;

    private void Awake()
    {
        EnsureParentCanvasScaler();
        ShowMenu();
    }

    /// <summary>确保父 Canvas 按屏幕分辨率缩放（WebGL 窗口变化时生效）。</summary>
    private void EnsureParentCanvasScaler()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize
            || scaler.referenceResolution != ReferenceResolution
            || Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) > 0.001f)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    public void ShowMenu()
    {
        ShowOnly(_menuPanel);
    }

    public void ShowControlStateJumpPanel()
    {
        ShowOnly(_controlStateJumpPanel);
    }

    public void ShowPlateMapHighlightPanel()
    {
        ShowOnly(_plateMapHighlightPanel);
    }

    public void ShowVehicleHeatmapUpdatePanel()
    {
        ShowOnly(_vehicleHeatmapUpdatePanel);
    }

    public void ShowCarPanelUiPanel()
    {
        ShowOnly(_carPanelUiPanel);
    }

    public void ShowPreviousLevelPanel()
    {
        ShowOnly(_previousLevelPanel);
    }

    public void ShowBigScreenCarouselPanel()
    {
        ShowOnly(_bigScreenCarouselPanel);
    }

    public void ShowHttpApiTestPanel()
    {
        ShowOnly(_httpApiTestPanel);
    }

    public void ShowAndroidBridgeApiPanel()
    {
        ShowOnly(_androidBridgeApiPanel);
    }

    public void ShowThreatHighRiskSecurityEventPanel()
    {
        ShowOnly(_threatHighRiskSecurityEventPanel);
    }

    public void ShowThreatLocalAlertTestPanel()
    {
        ShowOnly(_threatLocalAlertTestPanel);
    }

    public void ShowCarVehicleDataPanel()
    {
        ShowOnly(_carVehicleDataPanel);
    }

    private void ShowOnly(GameObject activePanel)
    {
        SetPanelActive(_menuPanel, activePanel == _menuPanel);
        SetPanelActive(_controlStateJumpPanel, activePanel == _controlStateJumpPanel);
        SetPanelActive(_plateMapHighlightPanel, activePanel == _plateMapHighlightPanel);
        SetPanelActive(_vehicleHeatmapUpdatePanel, activePanel == _vehicleHeatmapUpdatePanel);
        SetPanelActive(_carPanelUiPanel, activePanel == _carPanelUiPanel);
        SetPanelActive(_previousLevelPanel, activePanel == _previousLevelPanel);
        SetPanelActive(_bigScreenCarouselPanel, activePanel == _bigScreenCarouselPanel);
        SetPanelActive(_httpApiTestPanel, activePanel == _httpApiTestPanel);
        SetPanelActive(_androidBridgeApiPanel, activePanel == _androidBridgeApiPanel);
        SetPanelActive(_threatHighRiskSecurityEventPanel, activePanel == _threatHighRiskSecurityEventPanel);
        SetPanelActive(_threatLocalAlertTestPanel, activePanel == _threatLocalAlertTestPanel);
        SetPanelActive(_carVehicleDataPanel, activePanel == _carVehicleDataPanel);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
