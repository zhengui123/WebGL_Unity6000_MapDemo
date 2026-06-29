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
        SetPanelActive(_menuPanel, true);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowControlStateJumpPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, true);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowPlateMapHighlightPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, true);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowVehicleHeatmapUpdatePanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, true);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowCarPanelUiPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, true);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowPreviousLevelPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, true);
        SetPanelActive(_bigScreenCarouselPanel, false);
    }

    public void ShowBigScreenCarouselPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
        SetPanelActive(_vehicleHeatmapUpdatePanel, false);
        SetPanelActive(_carPanelUiPanel, false);
        SetPanelActive(_previousLevelPanel, false);
        SetPanelActive(_bigScreenCarouselPanel, true);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
