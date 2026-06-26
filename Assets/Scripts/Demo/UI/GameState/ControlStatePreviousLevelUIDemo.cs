using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 返回上个操控层级 Demo：读取 <see cref="GameManager.CurrentState"/> 并调用上一级跳转。
/// </summary>
[DisallowMultipleComponent]
public class ControlStatePreviousLevelUIDemo : MonoBehaviour
{
    public const bool DefaultUseInstantTransition = false;

    [SerializeField] private Text _currentStateLabel;
    [SerializeField] private Toggle _instantTransitionToggle;
    [SerializeField] private Button _previousLevelButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_previousLevelButton != null)
        {
            _previousLevelButton.onClick.AddListener(OnPreviousLevelButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_previousLevelButton != null)
        {
            _previousLevelButton.onClick.RemoveListener(OnPreviousLevelButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        RefreshCurrentStateLabel();
    }

    private void Update()
    {
        RefreshCurrentStateLabel();

        if (_previousLevelButton == null)
        {
            return;
        }

        ControlStateHierarchyTransitionController controller =
            ControlStateHierarchyTransitionController.Instance;
        _previousLevelButton.interactable = controller == null || !controller.IsBootstrapping;
    }

    private void OnPreviousLevelButtonClicked()
    {
        ControlStateHierarchyInputNavigation navigation =
            ControlStateHierarchyInputNavigation.FindFromTransitionController();
        if (navigation == null)
        {
            Debug.LogWarning("[ControlStatePreviousLevelUIDemo] 未找到 ControlStateHierarchyInputNavigation。");
            return;
        }

        bool useInstant = _instantTransitionToggle != null && _instantTransitionToggle.isOn;
        if (!navigation.TryTransitionToPreviousLevel(useInstant))
        {
            Debug.LogWarning("[ControlStatePreviousLevelUIDemo] 返回上一级未能启动。");
        }
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[ControlStatePreviousLevelUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void RefreshCurrentStateLabel()
    {
        if (_currentStateLabel == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            _currentStateLabel.text = "当前层级：未知";
            return;
        }

        _currentStateLabel.text = $"当前层级：{FormatControlState(manager.CurrentState)}";
    }

    private static string FormatControlState(GameManager.ControlState state)
    {
        switch (state)
        {
            case GameManager.ControlState.EarthLevel:
                return "地球级 (0)";
            case GameManager.ControlState.CountryLevel:
                return "国家级 (1)";
            case GameManager.ControlState.ProvinceLevel:
                return "省级 (2)";
            case GameManager.ControlState.VehicleLevel:
                return "车辆级 (3)";
            case GameManager.ControlState.PartLevel:
                return "零件级 (4)";
            case GameManager.ControlState.AttackPathLevel:
                return "攻击路径级 (5)";
            default:
                return state.ToString();
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
