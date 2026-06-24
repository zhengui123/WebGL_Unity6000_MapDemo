using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 车辆热力图更新 Demo UI：输入点位数量并调用 PlateMapShandongRandomPointsDemo 生成逻辑。
/// </summary>
[DisallowMultipleComponent]
public class VehicleHeatmapUpdateUIDemo : MonoBehaviour
{
    public const string DefaultPointCountText = "100";

    [SerializeField] private InputField _pointCountInput;
    [SerializeField] private Button _updateButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private PlateMapShandongRandomPointsDemo _pointsDemo;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_pointsDemo == null)
        {
            _pointsDemo = Object.FindFirstObjectByType<PlateMapShandongRandomPointsDemo>();
        }

        if (_updateButton != null)
        {
            _updateButton.onClick.AddListener(OnUpdateButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_updateButton != null)
        {
            _updateButton.onClick.RemoveListener(OnUpdateButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnUpdateButtonClicked()
    {
        if (_pointsDemo == null)
        {
            Debug.LogWarning("[VehicleHeatmapUpdateUIDemo] 未找到 PlateMapShandongRandomPointsDemo。");
            return;
        }

        if (_pointCountInput == null || string.IsNullOrWhiteSpace(_pointCountInput.text))
        {
            Debug.LogWarning("[VehicleHeatmapUpdateUIDemo] 请输入点位数量。");
            return;
        }

        _pointsDemo.UpdateVehicleHeatmapFromInput(_pointCountInput.text);
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[VehicleHeatmapUpdateUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
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
