using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo GameState 上层菜单：入口按钮绑定到 <see cref="DemoGameStateUINavigator"/>。
/// 与 FPS 同行的「显示菜单」勾选框控制入口列表显隐；进子面板时仍保留第一行开关。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateMenuUIDemo : MonoBehaviour
{
    [SerializeField] private DemoGameStateUINavigator _navigator;
    [SerializeField] private Toggle _menuVisibleToggle;
    [SerializeField] private GameObject _menuContent;
    [SerializeField] private RectTransform _menuPanelRect;
    [SerializeField] private float _expandedPanelHeight = 640f;
    [SerializeField] private float _collapsedPanelHeight = 96f;
    [SerializeField] private Button _controlStateJumpEntryButton;
    [SerializeField] private Button _plateMapHighlightEntryButton;
    [SerializeField] private Button _vehicleHeatmapUpdateEntryButton;
    [SerializeField] private Button _carPanelUiEntryButton;
    [SerializeField] private Button _previousLevelEntryButton;
    [SerializeField] private Button _bigScreenCarouselEntryButton;
    [SerializeField] private Button _httpApiTestEntryButton;
    [SerializeField] private Button _androidBridgeApiEntryButton;
    [SerializeField] private Button _threatHighRiskSecurityEventEntryButton;
    [SerializeField] private Button _threatLocalAlertTestEntryButton;
    [SerializeField] private Button _carVehicleDataEntryButton;
    [SerializeField] private Button _securityEventDetailEntryButton;

    /// <summary>处于子功能面板时为 true，此时强制收起入口列表，但保留 FPS/显示菜单行。</summary>
    private bool _inSubMenuMode;

    private void Awake()
    {
        if (_menuVisibleToggle != null)
        {
            _menuVisibleToggle.onValueChanged.AddListener(OnMenuVisibleToggleChanged);
            RefreshMenuContentVisible();
        }

        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.AddListener(OnControlStateJumpEntryClicked);
        }

        if (_plateMapHighlightEntryButton != null)
        {
            _plateMapHighlightEntryButton.onClick.AddListener(OnPlateMapHighlightEntryClicked);
        }

        if (_vehicleHeatmapUpdateEntryButton != null)
        {
            _vehicleHeatmapUpdateEntryButton.onClick.AddListener(OnVehicleHeatmapUpdateEntryClicked);
        }

        if (_carPanelUiEntryButton != null)
        {
            _carPanelUiEntryButton.onClick.AddListener(OnCarPanelUiEntryClicked);
        }

        if (_previousLevelEntryButton != null)
        {
            _previousLevelEntryButton.onClick.AddListener(OnPreviousLevelEntryClicked);
        }

        if (_bigScreenCarouselEntryButton != null)
        {
            _bigScreenCarouselEntryButton.onClick.AddListener(OnBigScreenCarouselEntryClicked);
        }

        if (_httpApiTestEntryButton != null)
        {
            _httpApiTestEntryButton.onClick.AddListener(OnHttpApiTestEntryClicked);
        }

        if (_androidBridgeApiEntryButton != null)
        {
            _androidBridgeApiEntryButton.onClick.AddListener(OnAndroidBridgeApiEntryClicked);
        }

        if (_threatHighRiskSecurityEventEntryButton != null)
        {
            _threatHighRiskSecurityEventEntryButton.onClick.AddListener(OnThreatHighRiskSecurityEventEntryClicked);
        }

        if (_threatLocalAlertTestEntryButton != null)
        {
            _threatLocalAlertTestEntryButton.onClick.AddListener(OnThreatLocalAlertTestEntryClicked);
        }

        if (_carVehicleDataEntryButton != null)
        {
            _carVehicleDataEntryButton.onClick.AddListener(OnCarVehicleDataEntryClicked);
        }

        if (_securityEventDetailEntryButton != null)
        {
            _securityEventDetailEntryButton.onClick.AddListener(OnSecurityEventDetailEntryClicked);
        }
    }

    private void OnDestroy()
    {
        if (_menuVisibleToggle != null)
        {
            _menuVisibleToggle.onValueChanged.RemoveListener(OnMenuVisibleToggleChanged);
        }

        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.RemoveListener(OnControlStateJumpEntryClicked);
        }

        if (_plateMapHighlightEntryButton != null)
        {
            _plateMapHighlightEntryButton.onClick.RemoveListener(OnPlateMapHighlightEntryClicked);
        }

        if (_vehicleHeatmapUpdateEntryButton != null)
        {
            _vehicleHeatmapUpdateEntryButton.onClick.RemoveListener(OnVehicleHeatmapUpdateEntryClicked);
        }

        if (_carPanelUiEntryButton != null)
        {
            _carPanelUiEntryButton.onClick.RemoveListener(OnCarPanelUiEntryClicked);
        }

        if (_previousLevelEntryButton != null)
        {
            _previousLevelEntryButton.onClick.RemoveListener(OnPreviousLevelEntryClicked);
        }

        if (_bigScreenCarouselEntryButton != null)
        {
            _bigScreenCarouselEntryButton.onClick.RemoveListener(OnBigScreenCarouselEntryClicked);
        }

        if (_httpApiTestEntryButton != null)
        {
            _httpApiTestEntryButton.onClick.RemoveListener(OnHttpApiTestEntryClicked);
        }

        if (_androidBridgeApiEntryButton != null)
        {
            _androidBridgeApiEntryButton.onClick.RemoveListener(OnAndroidBridgeApiEntryClicked);
        }

        if (_threatHighRiskSecurityEventEntryButton != null)
        {
            _threatHighRiskSecurityEventEntryButton.onClick.RemoveListener(OnThreatHighRiskSecurityEventEntryClicked);
        }

        if (_threatLocalAlertTestEntryButton != null)
        {
            _threatLocalAlertTestEntryButton.onClick.RemoveListener(OnThreatLocalAlertTestEntryClicked);
        }

        if (_carVehicleDataEntryButton != null)
        {
            _carVehicleDataEntryButton.onClick.RemoveListener(OnCarVehicleDataEntryClicked);
        }

        if (_securityEventDetailEntryButton != null)
        {
            _securityEventDetailEntryButton.onClick.RemoveListener(OnSecurityEventDetailEntryClicked);
        }
    }

    /// <summary>
    /// 由导航器调用：进子面板时收起入口列表并保留第一行；回主菜单时按勾选恢复。
    /// </summary>
    public void SetSubMenuMode(bool inSubMenu)
    {
        _inSubMenuMode = inSubMenu;
        RefreshMenuContentVisible();
    }

    private void OnMenuVisibleToggleChanged(bool visible)
    {
        // 子面板中勾选「显示菜单」→ 回到主菜单并展开入口
        if (_inSubMenuMode && visible)
        {
            DemoGameStateUINavigator navigator = ResolveNavigator();
            if (navigator != null)
            {
                navigator.ShowMenu();
                return;
            }
        }

        RefreshMenuContentVisible();
    }

    private void RefreshMenuContentVisible()
    {
        ApplyMenuContentVisible(ResolveContentVisible());
    }

    private bool ResolveContentVisible()
    {
        if (_inSubMenuMode)
        {
            return false;
        }

        return _menuVisibleToggle == null || _menuVisibleToggle.isOn;
    }

    private void ApplyMenuContentVisible(bool visible)
    {
        if (_menuContent != null)
        {
            _menuContent.SetActive(visible);
        }

        if (_menuPanelRect == null)
        {
            return;
        }

        Vector2 size = _menuPanelRect.sizeDelta;
        size.y = visible ? _expandedPanelHeight : _collapsedPanelHeight;
        _menuPanelRect.sizeDelta = size;
    }

    private void OnControlStateJumpEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowControlStateJumpPanel();
    }

    private void OnPlateMapHighlightEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowPlateMapHighlightPanel();
    }

    private void OnVehicleHeatmapUpdateEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowVehicleHeatmapUpdatePanel();
    }

    private void OnCarPanelUiEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowCarPanelUiPanel();
    }

    private void OnPreviousLevelEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowPreviousLevelPanel();
    }

    private void OnBigScreenCarouselEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowBigScreenCarouselPanel();
    }

    private void OnHttpApiTestEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowHttpApiTestPanel();
    }

    private void OnAndroidBridgeApiEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowAndroidBridgeApiPanel();
    }

    private void OnThreatHighRiskSecurityEventEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowThreatHighRiskSecurityEventPanel();
    }

    private void OnThreatLocalAlertTestEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowThreatLocalAlertTestPanel();
    }

    private void OnCarVehicleDataEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowCarVehicleDataPanel();
    }

    private void OnSecurityEventDetailEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowSecurityEventDetailPanel();
    }

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
