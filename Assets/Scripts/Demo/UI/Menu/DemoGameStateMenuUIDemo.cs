using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo GameState 上层菜单：入口按钮绑定到 <see cref="DemoGameStateUINavigator"/>。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateMenuUIDemo : MonoBehaviour
{
    [SerializeField] private DemoGameStateUINavigator _navigator;
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

    private void Awake()
    {
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
