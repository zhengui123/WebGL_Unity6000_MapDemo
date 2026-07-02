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

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
