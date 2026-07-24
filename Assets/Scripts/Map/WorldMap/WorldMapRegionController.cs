using System;
using UnityEngine;

/// <summary>
/// 默认世界地图板块控制器：在国内 / 国外指定大板块间切换。
/// 切换后瞬时回到该模式「全国」视角并清除省级/国家聚焦；显示仅保留当前板块根。
/// </summary>
[DisallowMultipleComponent]
public class WorldMapRegionController : MonoBehaviour
{
    [Serializable]
    public class ForeignPlateBinding
    {
        [Tooltip("大板块 firstClassCode，如 EAST_ASIA")]
        public string plateCode = "EAST_ASIA";

        [Tooltip("须与 Hierarchy 根物体名一致，如 东亚")]
        public string plateName = "东亚";

        [Tooltip("该大板块根 Transform（其子物体为国家）")]
        public Transform plateRoot;

        [Tooltip("切入该板块时的默认国家 secondClassCode（如 392=日本）")]
        public string defaultCountryCode = "392";
    }

    private static WorldMapRegionController _instance;

    [Header("启动")]
    [SerializeField] private WorldMapRegionMode _startMode = WorldMapRegionMode.Domestic;

    [Tooltip("启动为国外时使用的板块 firstClassCode，如 EAST_ASIA")]
    [SerializeField] private string _startForeignPlateCode = "EAST_ASIA";

    [Header("国内")]
    [Tooltip("国内地图根（如 中国地图）；全国视图显示此根")]
    [SerializeField] private Transform _domesticPlateRoot;

    [Header("国外大板块")]
    [SerializeField] private ForeignPlateBinding[] _foreignPlates = Array.Empty<ForeignPlateBinding>();

    [Header("显示")]
    [Tooltip("场景中的板块显示控制器；切换时会重绑 plateRoot 并清聚焦")]
    [SerializeField] private PlateMapDisplayController _displayController;

    [Tooltip("切换板块后是否立即还原到全国相机")]
    [SerializeField] private bool _restoreNationalViewOnSwitch = true;

    /// <summary>场景中的默认区域控制器。</summary>
    public static WorldMapRegionController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<WorldMapRegionController>(FindObjectsInactive.Include);
            }

            return _instance;
        }
    }

    /// <summary>当前国外板块下的默认国家 SOC（国内模式为空）。</summary>
    public string DefaultForeignCountryCode { get; private set; } = string.Empty;

    /// <summary>当前激活的板块根。</summary>
    public Transform ActivePlateRoot { get; private set; }

    private void Awake()
    {
        _instance = this;
        WorldMapRegionCodeTable.EnsureLoaded();
        ApplyStartRegion(instantNationalView: false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>切到国内：显示国内根，隐藏国外板块，全国 code="0"。</summary>
    public void SwitchToDomestic()
    {
        ApplyDomestic(instantNationalView: true);
    }

    /// <summary>切到指定国外大板块（plateCode 如 EAST_ASIA）。</summary>
    public bool SwitchToForeignPlate(string plateCode)
    {
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            Debug.LogWarning("[WorldMapRegionController] SwitchToForeignPlate: plateCode 为空。");
            return false;
        }

        if (!TryFindForeignBinding(plateCode.Trim(), out ForeignPlateBinding binding))
        {
            Debug.LogWarning(
                $"[WorldMapRegionController] 未配置国外板块 code={plateCode}。请在 Inspector 填写 firstClassCode（与 WorldMapRegionCodes.json 一致）。");
            return false;
        }

        ApplyForeign(binding, instantNationalView: true);
        return true;
    }

    /// <summary>按板块中文名切换（如「东亚」）。</summary>
    public bool SwitchToForeignPlateByName(string plateName)
    {
        if (string.IsNullOrWhiteSpace(plateName))
        {
            return false;
        }

        string name = plateName.Trim();
        if (_foreignPlates != null)
        {
            for (int i = 0; i < _foreignPlates.Length; i++)
            {
                ForeignPlateBinding binding = _foreignPlates[i];
                if (binding != null &&
                    !string.IsNullOrWhiteSpace(binding.plateName) &&
                    string.Equals(binding.plateName.Trim(), name, StringComparison.Ordinal))
                {
                    ApplyForeign(binding, instantNationalView: true);
                    return true;
                }
            }
        }

        if (WorldMapRegionCodeTable.TryGetPlateByName(name, out WorldMapPlateCodeEntry plate) &&
            !string.IsNullOrWhiteSpace(plate.plateCode))
        {
            return SwitchToForeignPlate(plate.plateCode);
        }

        Debug.LogWarning($"[WorldMapRegionController] 未找到国外板块名={name}");
        return false;
    }

    private void ApplyStartRegion(bool instantNationalView)
    {
        if (_startMode == WorldMapRegionMode.Foreign &&
            TryFindForeignBinding(_startForeignPlateCode, out ForeignPlateBinding binding))
        {
            ApplyForeign(binding, instantNationalView);
            return;
        }

        ApplyDomestic(instantNationalView);
    }

    private void ApplyDomestic(bool instantNationalView)
    {
        if (instantNationalView)
        {
            ClearFocusBeforeRebind();
        }

        DefaultForeignCountryCode = string.Empty;
        ActivePlateRoot = _domesticPlateRoot;
        SetRootsVisibility(activeForeign: null);
        WorldMapRegionContext.ApplyDomestic();
        RebindDisplayController(_domesticPlateRoot);
        GameManager.Instance?.NotifyRegionSwitchedToNational();
        Debug.Log($"[WorldMapRegionController] 已切换 | {WorldMapRegionContext.Describe()}");
    }

    private void ApplyForeign(ForeignPlateBinding binding, bool instantNationalView)
    {
        if (instantNationalView)
        {
            ClearFocusBeforeRebind();
        }

        string plateCode = binding.plateCode != null ? binding.plateCode.Trim() : string.Empty;
        string plateName = binding.plateName;
        if (string.IsNullOrWhiteSpace(plateName) &&
            WorldMapRegionCodeTable.TryGetPlateByCode(plateCode, out WorldMapPlateCodeEntry tablePlate))
        {
            plateName = tablePlate.plateName;
        }

        if (string.IsNullOrWhiteSpace(plateCode))
        {
            Debug.LogWarning(
                "[WorldMapRegionController] 国外板块缺少 plateCode。" + WorldMapRegionCodeTable.PlateCodeNote);
        }

        DefaultForeignCountryCode = string.IsNullOrWhiteSpace(binding.defaultCountryCode)
            ? string.Empty
            : binding.defaultCountryCode.Trim();
        ActivePlateRoot = binding.plateRoot;
        SetRootsVisibility(activeForeign: binding);
        WorldMapRegionContext.ApplyForeignPlate(plateCode, plateName);
        RebindDisplayController(binding.plateRoot);
        GameManager.Instance?.NotifyRegionSwitchedToNational();
        Debug.Log($"[WorldMapRegionController] 已切换 | {WorldMapRegionContext.Describe()}");
    }

    private void SetRootsVisibility(ForeignPlateBinding activeForeign)
    {
        if (_domesticPlateRoot != null)
        {
            _domesticPlateRoot.gameObject.SetActive(activeForeign == null);
        }

        if (_foreignPlates == null)
        {
            return;
        }

        for (int i = 0; i < _foreignPlates.Length; i++)
        {
            ForeignPlateBinding binding = _foreignPlates[i];
            if (binding?.plateRoot == null)
            {
                continue;
            }

            bool active = activeForeign != null && ReferenceEquals(binding, activeForeign);
            binding.plateRoot.gameObject.SetActive(active);
        }
    }

    private void RebindDisplayController(Transform plateRoot)
    {
        PlateMapDisplayController controller = _displayController != null
            ? _displayController
            : PlateMapDisplayController.Instance;
        if (controller == null)
        {
            return;
        }

        controller.BindPlateMapRoot(plateRoot);
    }

    /// <summary>切换前清聚焦并还原全国相机（须在重绑 root 之前调用，以免丢失 pre-focus 位姿）。</summary>
    private void ClearFocusBeforeRebind()
    {
        if (!_restoreNationalViewOnSwitch)
        {
            return;
        }

        PlateMapDisplayController controller = _displayController != null
            ? _displayController
            : PlateMapDisplayController.Instance;
        if (controller == null)
        {
            return;
        }

        if (controller.CanRestoreCamera)
        {
            controller.RestoreCameraPosition();
        }
        else
        {
            controller.ClearFocusState();
            CountryMapZoomController.Instance?.ResetToCountryHomeAfterProvinceRestore();
        }
    }

    private bool TryFindForeignBinding(string plateCode, out ForeignPlateBinding binding)
    {
        binding = null;
        if (_foreignPlates == null || string.IsNullOrWhiteSpace(plateCode))
        {
            return false;
        }

        for (int i = 0; i < _foreignPlates.Length; i++)
        {
            ForeignPlateBinding item = _foreignPlates[i];
            if (item != null &&
                !string.IsNullOrWhiteSpace(item.plateCode) &&
                string.Equals(item.plateCode.Trim(), plateCode, StringComparison.OrdinalIgnoreCase))
            {
                binding = item;
                return true;
            }
        }

        return false;
    }
}
