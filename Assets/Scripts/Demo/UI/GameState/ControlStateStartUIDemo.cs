using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 操控状态跳转 UI：绑定下拉、瞬时开关、过渡参数与跳转按钮，调用 <see cref="ControlStateHierarchyTransitionController"/>。
/// </summary>
[DisallowMultipleComponent]
public class ControlStateStartUIDemo : MonoBehaviour
{
    public const string DefaultProvinceName = "山东";
    public const string DefaultProvinceModuleName = "polySurface3";
    public const bool DefaultUseInstantTransition = true;
    /// <summary>默认开启：不填省 code/名，走 ResolveUnitCode(null)（国内默认省 / 国外 DefaultForeignCountryCode）。</summary>
    public const bool DefaultUseDefaultProvince = true;
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

    [SerializeField] private Dropdown _targetStateDropdown;
    [SerializeField] private Toggle _instantTransitionToggle;
    [SerializeField] private Toggle _useDefaultProvinceToggle;
    [SerializeField] private Dropdown _provinceNameDropdown;
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

        if (_useDefaultProvinceToggle != null)
        {
            _useDefaultProvinceToggle.onValueChanged.AddListener(OnUseDefaultProvinceChanged);
        }

        RefreshProvinceInputsInteractable();
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

        if (_useDefaultProvinceToggle != null)
        {
            _useDefaultProvinceToggle.onValueChanged.RemoveListener(OnUseDefaultProvinceChanged);
        }
    }

    /// <summary>刷新下拉选项（省份名、板块模块名、零件名）。</summary>
    public void RefreshAllDropdownOptions()
    {
        EnsureTargetStateDropdownOptions();
        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceNames(),
            DefaultProvinceName);
        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceModuleNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceModuleNames(),
            DefaultProvinceModuleName);
        ControlStateStartUIOptionProvider.ApplyOptions(
            _partNameDropdown,
            ControlStateStartUIOptionProvider.CollectPartNames(),
            null);
        RefreshProvinceInputsInteractable();
    }

    /// <summary>将 UI 控件恢复为与层级跳转控制器一致的默认值。</summary>
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

        if (_useDefaultProvinceToggle != null)
        {
            _useDefaultProvinceToggle.isOn = DefaultUseDefaultProvince;
        }

        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceNames(),
            DefaultProvinceName);

        ControlStateStartUIOptionProvider.ApplyOptions(
            _provinceModuleNameDropdown,
            ControlStateStartUIOptionProvider.CollectProvinceModuleNames(),
            DefaultProvinceModuleName);

        ControlStateStartUIOptionProvider.ApplyOptions(
            _partNameDropdown,
            ControlStateStartUIOptionProvider.CollectPartNames(),
            null);

        RefreshProvinceInputsInteractable();
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[ControlStateStartUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void OnJumpButtonClicked()
    {
        if (MapApi.Instance == null)
        {
            LogManager.LogFeatureWarning("[ControlStateStartUIDemo] 未找到 MapApi。");
            return;
        }

        if (_targetStateDropdown == null)
        {
            LogManager.LogFeatureWarning("[ControlStateStartUIDemo] 未绑定目标状态下拉列表。");
            return;
        }

        bool useInstant = _instantTransitionToggle != null && _instantTransitionToggle.isOn;
        bool useDefaultProvince = _useDefaultProvinceToggle == null || _useDefaultProvinceToggle.isOn;
        GameManager.ControlState targetState = (GameManager.ControlState)_targetStateDropdown.value;
        string selectedPartId = ControlStateStartUIOptionProvider.GetSelectedPartId(_partNameDropdown);

        // 开启默认省级：不填 code/名，走 WorldMapPlateResolver（国内默认省 / 国外 DefaultForeignCountryCode）
        string provinceCode = null;
        if (!useDefaultProvince)
        {
            string provinceName = ControlStateStartUIOptionProvider.GetSelectedText(_provinceNameDropdown);
            if (!string.IsNullOrWhiteSpace(provinceName) &&
                GaodeProvinceAdcodeConverter.TryProvinceNameToAdcode(provinceName, out string adcode))
            {
                provinceCode = adcode;
            }
            else if (!string.IsNullOrWhiteSpace(provinceName))
            {
                LogManager.LogFeatureWarning(
                    $"[ControlStateStartUIDemo] 无法将「{provinceName}」解析为省级 code，将按空 code 走默认逻辑。");
            }
        }

        bool started = MapApi.Instance.TransitionToControlState(
            (int)targetState,
            provinceCode,
            selectedPartId,
            useInstant);

        if (!started)
        {
            LogManager.LogFeatureWarning("[ControlStateStartUIDemo] 跳转未能启动。");
            return;
        }

        LogManager.LogFeature(
            $"[ControlStateStartUIDemo] 已请求跳转 → {targetState} | useDefaultProvince={useDefaultProvince} | " +
            $"provinceCode={(provinceCode ?? "(null/默认)")} | instant={useInstant}");
    }

    private void OnUseDefaultProvinceChanged(bool _)
    {
        RefreshProvinceInputsInteractable();
    }

    private void RefreshProvinceInputsInteractable()
    {
        bool useDefault = _useDefaultProvinceToggle == null || _useDefaultProvinceToggle.isOn;
        bool enableManual = !useDefault;

        if (_provinceNameDropdown != null)
        {
            _provinceNameDropdown.interactable = enableManual;
        }

        if (_provinceModuleNameDropdown != null)
        {
            _provinceModuleNameDropdown.interactable = enableManual;
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

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
