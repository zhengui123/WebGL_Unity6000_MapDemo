using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 操控状态跳转 UI：绑定下拉、瞬时开关、过渡参数与跳转按钮，调用 <see cref="ControlStateStartDemo"/>。
/// </summary>
[DisallowMultipleComponent]
public class ControlStateStartUIDemo : MonoBehaviour
{
    public const string DefaultProvinceName = "山东";
    public const string DefaultProvinceModuleName = "polySurface3";
    public const bool DefaultUseInstantTransition = true;
    public const int DefaultTargetStateIndex = (int)GameManager.ControlState.EarthLevel;

    private static readonly string[] TargetStateLabels =
    {
        "地球级 (0)",
        "国家级 (1)",
        "省级 (2)",
        "车辆级 (3)",
        "零件级 (4)",
        "攻击路径级 (5)",
    };

    [SerializeField] private ControlStateStartDemo _controlStateStartDemo;
    [SerializeField] private Dropdown _targetStateDropdown;
    [SerializeField] private Toggle _instantTransitionToggle;
    [SerializeField] private InputField _provinceNameInput;
    [SerializeField] private Dropdown _provinceModuleNameDropdown;
    [SerializeField] private Dropdown _partNameDropdown;
    [SerializeField] private Button _jumpButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        RefreshAllDropdownOptions();
        ApplyDefaultValues();

        if (_jumpButton != null)
        {
            _jumpButton.onClick.AddListener(OnJumpButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_jumpButton != null)
        {
            _jumpButton.onClick.RemoveListener(OnJumpButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void Update()
    {
        if (_jumpButton == null)
        {
            return;
        }

        ControlStateStartDemo demo = ResolveDemo();
        _jumpButton.interactable = demo == null || !demo.IsBootstrapping;
    }

    /// <summary>刷新下拉选项（板块模块名、零件名）。</summary>
    public void RefreshAllDropdownOptions()
    {
        EnsureTargetStateDropdownOptions();
        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceModuleNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceModuleNames(),
            DefaultProvinceModuleName);
        ControlStateStartUIOptionProvider.ApplyOptions(
            _partNameDropdown,
            ControlStateStartUIOptionProvider.CollectPartNames(),
            null);
    }

    /// <summary>将 UI 控件恢复为与 <see cref="ControlStateStartDemo"/> 一致的默认值。</summary>
    public void ApplyDefaultValues()
    {
        if (_targetStateDropdown != null)
        {
            _targetStateDropdown.value = Mathf.Clamp(DefaultTargetStateIndex, 0, TargetStateLabels.Length - 1);
            _targetStateDropdown.RefreshShownValue();
        }

        if (_instantTransitionToggle != null)
        {
            _instantTransitionToggle.isOn = DefaultUseInstantTransition;
        }

        if (_provinceNameInput != null)
        {
            _provinceNameInput.text = DefaultProvinceName;
        }

        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceModuleNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceModuleNames(),
            DefaultProvinceModuleName);

        ControlStateStartUIOptionProvider.ApplyOptions(
            _partNameDropdown,
            ControlStateStartUIOptionProvider.CollectPartNames(),
            null);
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[ControlStateStartUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void OnJumpButtonClicked()
    {
        ControlStateStartDemo demo = ResolveDemo();
        if (demo == null)
        {
            Debug.LogWarning("[ControlStateStartUIDemo] 未找到 ControlStateStartDemo。");
            return;
        }

        if (demo.IsBootstrapping)
        {
            Debug.LogWarning("[ControlStateStartUIDemo] 正在跳转中，请稍候。");
            return;
        }

        if (_targetStateDropdown == null)
        {
            Debug.LogWarning("[ControlStateStartUIDemo] 未绑定目标状态下拉列表。");
            return;
        }

        bool useInstant = _instantTransitionToggle != null && _instantTransitionToggle.isOn;
        GameManager.ControlState targetState = (GameManager.ControlState)_targetStateDropdown.value;
        string provinceName = _provinceNameInput != null ? _provinceNameInput.text : null;
        string provinceModuleName = ControlStateStartUIOptionProvider.GetSelectedText(_provinceModuleNameDropdown);
        string partName = ControlStateStartUIOptionProvider.GetSelectedText(_partNameDropdown);

        bool started = demo.TransitionToState(
            useInstant,
            targetState,
            provinceName,
            provinceModuleName,
            partName);

        if (!started)
        {
            Debug.LogWarning("[ControlStateStartUIDemo] 跳转未能启动。");
        }
    }

    private void EnsureTargetStateDropdownOptions()
    {
        if (_targetStateDropdown == null)
        {
            return;
        }

        if (_targetStateDropdown.options == null || _targetStateDropdown.options.Count != TargetStateLabels.Length)
        {
            _targetStateDropdown.ClearOptions();
            _targetStateDropdown.AddOptions(new List<string>(TargetStateLabels));
        }
    }

    private ControlStateStartDemo ResolveDemo()
    {
        if (_controlStateStartDemo != null)
        {
            return _controlStateStartDemo;
        }

        return FindFirstObjectByType<ControlStateStartDemo>();
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
