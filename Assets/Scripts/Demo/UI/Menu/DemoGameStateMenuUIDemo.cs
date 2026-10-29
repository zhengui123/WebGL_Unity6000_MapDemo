using UnityEngine;
using UnityEngine.Serialization;
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
    [FormerlySerializedAs("_clearConsumedMarkEntryButton")]
    [SerializeField] private Button _toggleUiLanguageEntryButton;
    [SerializeField] private Button _controlStateJumpEntryButton;
    [SerializeField] private Button _plateMapHighlightEntryButton;
    [SerializeField] private Button _simulateHeatmapEntryButton;
    [SerializeField] private Button _worldMapSwitchEntryButton;
    [SerializeField] private Button _carPanelUiEntryButton;
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

        if (_toggleUiLanguageEntryButton != null)
        {
            _toggleUiLanguageEntryButton.onClick.AddListener(OnToggleUiLanguageEntryClicked);
            RefreshToggleUiLanguageButtonLabel();
        }

        LanguageManager.OnLanguageChanged += HandleLanguageChanged;

        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.AddListener(OnControlStateJumpEntryClicked);
        }

        if (_plateMapHighlightEntryButton != null)
        {
            _plateMapHighlightEntryButton.onClick.AddListener(OnPlateMapHighlightEntryClicked);
        }

        if (_simulateHeatmapEntryButton != null)
        {
            _simulateHeatmapEntryButton.onClick.AddListener(OnSimulateHeatmapEntryClicked);
        }

        if (_worldMapSwitchEntryButton != null)
        {
            _worldMapSwitchEntryButton.onClick.AddListener(OnWorldMapSwitchEntryClicked);
        }

        if (_carPanelUiEntryButton != null)
        {
            _carPanelUiEntryButton.onClick.AddListener(OnCarPanelUiEntryClicked);
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

        LanguageManager.OnLanguageChanged -= HandleLanguageChanged;

        if (_toggleUiLanguageEntryButton != null)
        {
            _toggleUiLanguageEntryButton.onClick.RemoveListener(OnToggleUiLanguageEntryClicked);
        }

        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.RemoveListener(OnControlStateJumpEntryClicked);
        }

        if (_plateMapHighlightEntryButton != null)
        {
            _plateMapHighlightEntryButton.onClick.RemoveListener(OnPlateMapHighlightEntryClicked);
        }

        if (_simulateHeatmapEntryButton != null)
        {
            _simulateHeatmapEntryButton.onClick.RemoveListener(OnSimulateHeatmapEntryClicked);
        }

        if (_worldMapSwitchEntryButton != null)
        {
            _worldMapSwitchEntryButton.onClick.RemoveListener(OnWorldMapSwitchEntryClicked);
        }

        if (_carPanelUiEntryButton != null)
        {
            _carPanelUiEntryButton.onClick.RemoveListener(OnCarPanelUiEntryClicked);
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
        // 子面板中勾选「显示菜单」→ 返回主菜单并展开入口
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

    private void OnToggleUiLanguageEntryClicked()
    {
        UiLanguage current = MapApi.Instance != null
            ? MapApi.Instance.GetUiLanguage()
            : LanguageManager.Instance.CurrentLanguage;
        string nextCode = current == UiLanguage.English ? "zh-CN" : "en-US";
        bool ok = MapApi.Instance != null && MapApi.Instance.SetUiLanguage(nextCode);
        if (!ok)
        {
            LogManager.LogFeatureWarning(
                $"[DemoGameStateMenuUIDemo] 切换 UI 语言失败：{nextCode}");
            return;
        }

        LogManager.LogFeature($"[DemoGameStateMenuUIDemo] 已切换 UI 语言 → {nextCode}");
        RefreshToggleUiLanguageButtonLabel();
    }

    private void HandleLanguageChanged(UiLanguage _)
    {
        RefreshToggleUiLanguageButtonLabel();
    }

    private void RefreshToggleUiLanguageButtonLabel()
    {
        if (_toggleUiLanguageEntryButton == null)
        {
            return;
        }

        Text label = _toggleUiLanguageEntryButton.GetComponentInChildren<Text>();
        if (label == null)
        {
            return;
        }

        UiLanguage current = LanguageManager.Instance != null
            ? LanguageManager.Instance.CurrentLanguage
            : UiLanguage.Chinese;
        // 按钮文案表示「点击后将切到」的目标语言
        label.text = current == UiLanguage.English
            ? "切换中文 (zh-CN)"
            : "切换英文 (en-US)";
    }

    private void OnControlStateJumpEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowControlStateJumpPanel();
    }

    private void OnPlateMapHighlightEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowPlateMapHighlightPanel();
    }

    private void OnSimulateHeatmapEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowSimulateHeatmapPanel();
    }

    private void OnWorldMapSwitchEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowWorldMapSwitchPanel();
    }

    private void OnCarPanelUiEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowCarPanelUiPanel();
    }

    private void OnHttpApiTestEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowHttpApiTestPanel();
    }

    private void OnAndroidBridgeApiEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowAndroidBridgeApiPanel();
    }

    private void OnThreatHighRiskSecurityEventEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowThreatHighRiskSecurityEventPanel();
    }

    private void OnThreatLocalAlertTestEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowThreatLocalAlertTestPanel();
    }

    private void OnCarVehicleDataEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowCarVehicleDataPanel();
    }

    private void OnSecurityEventDetailEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
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
