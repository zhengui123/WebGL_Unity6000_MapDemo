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

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
