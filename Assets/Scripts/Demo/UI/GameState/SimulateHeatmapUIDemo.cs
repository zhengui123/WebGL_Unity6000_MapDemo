using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 模拟热力图 Demo：开启=全板块回灌 Inspector 默认点；关闭=清空；可创建大量随机模拟数据。
/// </summary>
[DisallowMultipleComponent]
public class SimulateHeatmapUIDemo : MonoBehaviour
{
    public const string DefaultPointCountText = "100";

    [SerializeField] private Text _statusLabel;
    [SerializeField] private Button _enableButton;
    [SerializeField] private Button _disableButton;
    [SerializeField] private InputField _pointCountInput;
    [SerializeField] private Button _createBulkDataButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private PlateMapShandongRandomPointsDemo _pointsDemo;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_pointsDemo == null)
        {
            _pointsDemo = Object.FindFirstObjectByType<PlateMapShandongRandomPointsDemo>();
        }

        if (_enableButton != null)
        {
            _enableButton.onClick.AddListener(OnEnableSimulateClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.AddListener(OnDisableSimulateClicked);
        }

        if (_createBulkDataButton != null)
        {
            _createBulkDataButton.onClick.AddListener(OnCreateBulkDataClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        RefreshStatus("就绪");
    }

    private void OnDestroy()
    {
        if (_enableButton != null)
        {
            _enableButton.onClick.RemoveListener(OnEnableSimulateClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.RemoveListener(OnDisableSimulateClicked);
        }

        if (_createBulkDataButton != null)
        {
            _createBulkDataButton.onClick.RemoveListener(OnCreateBulkDataClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnEnableSimulateClicked()
    {
        // 停轮询，避免接口数据覆盖默认点
        VehicleHeatmapApiController api = VehicleHeatmapApiController.Instance;
        if (api != null && api.IsPolling)
        {
            api.StopPolling();
        }

        PlateMapVehiclePointController.NotifyCountryHeatmapActive();

        CarHotManager hotManager = CarHotManager.Instance;
        if (hotManager != null)
        {
            hotManager.ApplyControllerEnableState(PlateMapBoundaryDatabase.NationalProvinceCode);
        }

        int applied = PlateMapVehiclePointController.ApplyDefaultVehiclePointsTestDataToAllPlates();
        RefreshStatus($"已开启模拟：回灌 {applied} 个板块默认点");
        LogManager.LogFeature($"[SimulateHeatmapUIDemo] 已对 {applied} 个板块应用默认热力点。");
    }

    private void OnDisableSimulateClicked()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            RefreshStatus("关闭失败：未找到事件总线");
            LogManager.LogFeatureWarning("[SimulateHeatmapUIDemo] 未找到 PlateMapVehiclePointEvents。");
            return;
        }

        int cleared = hub.ClearAllVehiclePoints(syncNow: true);
        RefreshStatus($"已关闭模拟：清空 {cleared} 个板块热力点");
        LogManager.LogFeature($"[SimulateHeatmapUIDemo] 已清空 {cleared} 个板块热力点。");
    }

    private void OnCreateBulkDataClicked()
    {
        if (_pointsDemo == null)
        {
            RefreshStatus("创建失败：未找到随机点 Demo");
            LogManager.LogFeatureWarning("[SimulateHeatmapUIDemo] 未找到 PlateMapShandongRandomPointsDemo。");
            return;
        }

        if (_pointCountInput == null || string.IsNullOrWhiteSpace(_pointCountInput.text))
        {
            RefreshStatus("请输入点位数量");
            LogManager.LogFeatureWarning("[SimulateHeatmapUIDemo] 请输入点位数量。");
            return;
        }

        // 大量数据写入前停轮询，避免被接口覆盖
        VehicleHeatmapApiController api = VehicleHeatmapApiController.Instance;
        if (api != null && api.IsPolling)
        {
            api.StopPolling();
        }

        _pointsDemo.UpdateVehicleHeatmapFromInput(_pointCountInput.text);
        RefreshStatus($"已请求创建大量模拟数据：{_pointCountInput.text}");
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[SimulateHeatmapUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void RefreshStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = message;
        }
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
