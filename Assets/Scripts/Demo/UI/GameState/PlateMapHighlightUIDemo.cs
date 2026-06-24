using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 省级板块高亮 Demo UI：选择省市名并调用 PlateMapHighlightController。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapHighlightUIDemo : MonoBehaviour
{
    public const string DefaultHighlightName = "山东";

    [SerializeField] private Dropdown _provinceNameDropdown;
    [SerializeField] private Button _highlightButton;
    [SerializeField] private Button _clearHighlightButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        RefreshProvinceDropdown();

        if (_highlightButton != null)
        {
            _highlightButton.onClick.AddListener(OnHighlightButtonClicked);
        }

        if (_clearHighlightButton != null)
        {
            _clearHighlightButton.onClick.AddListener(OnClearHighlightButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_highlightButton != null)
        {
            _highlightButton.onClick.RemoveListener(OnHighlightButtonClicked);
        }

        if (_clearHighlightButton != null)
        {
            _clearHighlightButton.onClick.RemoveListener(OnClearHighlightButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    public void RefreshProvinceDropdown()
    {
        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceNameDropdown,
            ControlStateStartUIOptionProvider.CollectPlateHighlightNames(),
            DefaultHighlightName);
    }

    private void OnHighlightButtonClicked()
    {
        PlateMapHighlightController controller = PlateMapHighlightController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[PlateMapHighlightUIDemo] 未找到 PlateMapHighlightController。");
            return;
        }

        string moduleName = ControlStateStartUIOptionProvider.GetSelectedText(_provinceNameDropdown);
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Debug.LogWarning("[PlateMapHighlightUIDemo] 未选择省市名。");
            return;
        }

        if (!controller.HighlightModule(moduleName))
        {
            Debug.LogWarning($"[PlateMapHighlightUIDemo] 高亮失败：{moduleName}");
        }
    }

    private void OnClearHighlightButtonClicked()
    {
        PlateMapHighlightController controller = PlateMapHighlightController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[PlateMapHighlightUIDemo] 未找到 PlateMapHighlightController。");
            return;
        }

        controller.ClearHighlight();
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[PlateMapHighlightUIDemo] 未找到 DemoGameStateUINavigator。");
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
