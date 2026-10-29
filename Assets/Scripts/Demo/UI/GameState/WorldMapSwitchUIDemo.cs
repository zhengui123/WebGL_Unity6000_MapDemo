using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界地图切换 Demo：国内/国外类型；国外固定六大板块；切换后瞬时全国视角。
/// </summary>
[DisallowMultipleComponent]
public class WorldMapSwitchUIDemo : MonoBehaviour
{
    public const string RegionTypeDomestic = "国内";
    public const string RegionTypeForeign = "国外";
    public const string DefaultForeignPlateName = "东亚";

    /// <summary>固定可选国外大板块（显示名 → firstClassCode）。</summary>
    private static readonly string[] FixedForeignPlateNames =
    {
        "东亚",
        "西欧",
        "北非",
        "北美",
        "南美",
        "澳新",
    };

    private static readonly string[] FixedForeignPlateCodes =
    {
        "EAST_ASIA",
        "WESTERN_EUROPE",
        "NORTH_AFRICA",
        "NORTH_AMERICA_REGION",
        "SOUTH_AMERICA_REGION",
        "AUSTRALASIA",
    };

    [SerializeField] private Dropdown _regionTypeDropdown;
    [SerializeField] private Dropdown _foreignPlateDropdown;
    [SerializeField] private GameObject _foreignPlateRow;
    [SerializeField] private Button _switchButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Text _statusLabel;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_regionTypeDropdown != null)
        {
            _regionTypeDropdown.ClearOptions();
            _regionTypeDropdown.AddOptions(new List<string> { RegionTypeDomestic, RegionTypeForeign });
            _regionTypeDropdown.value = 0;
            _regionTypeDropdown.RefreshShownValue();
            _regionTypeDropdown.onValueChanged.AddListener(OnRegionTypeChanged);
        }

        if (_foreignPlateDropdown != null)
        {
            _foreignPlateDropdown.ClearOptions();
            _foreignPlateDropdown.AddOptions(new List<string>(FixedForeignPlateNames));
            int defaultIndex = 0;
            for (int i = 0; i < FixedForeignPlateNames.Length; i++)
            {
                if (FixedForeignPlateNames[i] == DefaultForeignPlateName)
                {
                    defaultIndex = i;
                    break;
                }
            }

            _foreignPlateDropdown.value = defaultIndex;
            _foreignPlateDropdown.RefreshShownValue();
        }

        if (_switchButton != null)
        {
            _switchButton.onClick.AddListener(OnSwitchButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        RefreshForeignPlateRowVisible();
        RefreshStatus("就绪");
    }

    private void OnDestroy()
    {
        if (_regionTypeDropdown != null)
        {
            _regionTypeDropdown.onValueChanged.RemoveListener(OnRegionTypeChanged);
        }

        if (_switchButton != null)
        {
            _switchButton.onClick.RemoveListener(OnSwitchButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnRegionTypeChanged(int _)
    {
        RefreshForeignPlateRowVisible();
    }

    private void RefreshForeignPlateRowVisible()
    {
        bool isForeign = IsForeignSelected();
        if (_foreignPlateRow != null)
        {
            _foreignPlateRow.SetActive(isForeign);
        }
        else if (_foreignPlateDropdown != null)
        {
            _foreignPlateDropdown.gameObject.SetActive(isForeign);
        }
    }

    private void OnSwitchButtonClicked()
    {
        WorldMapRegionController region = WorldMapRegionController.Instance;
        if (region == null)
        {
            RefreshStatus("切换失败：未找到 WorldMapRegionController");
            LogManager.LogFeatureWarning("[WorldMapSwitchUIDemo] 未找到 WorldMapRegionController。");
            return;
        }

        if (!IsForeignSelected())
        {
            region.SwitchToDomestic();
            RefreshStatus("已切换：国内 · 全国");
            LogManager.LogFeature("[WorldMapSwitchUIDemo] 已切换国内全国。");
            return;
        }

        int plateIndex = _foreignPlateDropdown != null ? _foreignPlateDropdown.value : 0;
        if (plateIndex < 0 || plateIndex >= FixedForeignPlateCodes.Length)
        {
            RefreshStatus("切换失败：未选择世界板块");
            return;
        }

        string plateCode = FixedForeignPlateCodes[plateIndex];
        string plateName = FixedForeignPlateNames[plateIndex];
        if (!region.SwitchToForeignPlate(plateCode))
        {
            // 绑定可能用中文名，再试一次
            if (!region.SwitchToForeignPlateByName(plateName))
            {
                RefreshStatus($"切换失败：{plateName}（{plateCode}）未绑定");
                LogManager.LogFeatureWarning(
                    $"[WorldMapSwitchUIDemo] SwitchToForeignPlate 失败：{plateName}/{plateCode}");
                return;
            }
        }

        RefreshStatus($"已切换：国外 · {plateName} · 全国");
        LogManager.LogFeature($"[WorldMapSwitchUIDemo] 已切换国外板块 {plateName}（{plateCode}）。");
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            LogManager.LogFeatureWarning("[WorldMapSwitchUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private bool IsForeignSelected()
    {
        if (_regionTypeDropdown == null || _regionTypeDropdown.options.Count == 0)
        {
            return false;
        }

        int index = Mathf.Clamp(_regionTypeDropdown.value, 0, _regionTypeDropdown.options.Count - 1);
        return _regionTypeDropdown.options[index].text == RegionTypeForeign;
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
