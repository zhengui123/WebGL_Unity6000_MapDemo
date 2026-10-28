using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 默认世界地图板块控制器：在国内 / 国外指定大板块间切换。
/// 切换后瞬时回到该模式「全国」视角并清除省级/国家聚焦；显示仅保留当前板块根。
/// 国外板块初始位置设置在EarthTransition中
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

    [Tooltip("启动为国外时使用的大板块；Inspector 下拉仅显示已绑定板块名，内部存 plateCode")]
    [SerializeField] private string _startForeignPlateCode = "EAST_ASIA";

    [Header("国内")]
    [Tooltip("国内地图根（如 中国地图）；全国视图显示此根")]
    [SerializeField] private Transform _domesticPlateRoot;

    [Header("国外大板块")]
    [SerializeField] private ForeignPlateBinding[] _foreignPlates = Array.Empty<ForeignPlateBinding>();

    [Header("显示")]
    [Tooltip("可选回退：各大板根上找不到 PlateMapDisplayController 时使用。正常应挂在各国/中国地图根上，由 ActivePlateRoot 自动解析")]
    [SerializeField] private PlateMapDisplayController _displayController;

    [Tooltip("世界模式背景线（如「世界地图边界线」）；国内关闭，国外打开")]
    [SerializeField] private GameObject _worldModeBackgroundLine;

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

    /// <summary>
    /// 当前激活大板根上的显示控制器（点击聚焦 / 还原 / MapApi 必须用此实例）。
    /// 各大板（中国地图、澳新等）各自挂有 PlateMapDisplayController；勿固定绑国内根，否则切国外后还原失效。
    /// </summary>
    public PlateMapDisplayController DisplayController =>
        ResolveDisplayControllerForPlate(ActivePlateRoot);

    /// <summary>Inspector 绑定的国外大板块列表（只读视图）。</summary>
    public IReadOnlyList<ForeignPlateBinding> ForeignPlates =>
        _foreignPlates ?? Array.Empty<ForeignPlateBinding>();

    /// <summary>
    /// 当前激活国外板块 code；国内或未激活时为空。
    /// </summary>
    public string ActiveForeignPlateCode
    {
        get
        {
            if (WorldMapRegionContext.Mode != WorldMapRegionMode.Foreign)
            {
                return string.Empty;
            }

            return WorldMapRegionContext.PlateCode ?? string.Empty;
        }
    }

    /// <summary>按 code 查找绑定（忽略大小写）。</summary>
    public bool TryGetForeignBinding(string plateCode, out ForeignPlateBinding binding)
    {
        return TryFindForeignBinding(plateCode, out binding);
    }

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

    /// <summary>
    /// 设置国内外默认并立刻切换（同面板 SwitchToDomestic / SwitchToForeignPlate）。
    /// </summary>
    /// <param name="isForeign">true=国外，false=国内。</param>
    /// <param name="foreignPlateCode">国外大板块 code；国内可空（忽略）。</param>
    /// <param name="defaultUnitCode">
    /// 国内=省级 adcode（空则保留现有默认省）；国外=国家 SOC（空则用绑定 defaultCountryCode）。
    /// </param>
    public bool ApplyRegionDefaults(bool isForeign, string foreignPlateCode = null, string defaultUnitCode = null)
    {
        if (isForeign)
        {
            if (!SwitchToForeignPlate(foreignPlateCode))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(defaultUnitCode))
            {
                DefaultForeignCountryCode = defaultUnitCode.Trim();
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(defaultUnitCode))
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.SetDefaultProvinceCode(defaultUnitCode.Trim()))
            {
                LogManager.LogFeatureWarning(
                    $"[WorldMapRegionController] 国内默认省 code 无效：{defaultUnitCode}，仍切换国内。");
            }
        }

        SwitchToDomestic();
        return true;
    }

    /// <summary>切到指定国外大板块（plateCode 如 EAST_ASIA）。</summary>
    public bool SwitchToForeignPlate(string plateCode)
    {
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            LogManager.LogFeatureWarning("[WorldMapRegionController] SwitchToForeignPlate: plateCode 为空。");
            return false;
        }

        if (!TryFindForeignBinding(plateCode.Trim(), out ForeignPlateBinding binding))
        {
            LogManager.LogFeatureWarning(
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

        LogManager.LogFeatureWarning($"[WorldMapRegionController] 未找到国外板块名={name}");
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
        SetWorldModeBackgroundLineVisible(false);
        WorldMapRegionContext.ApplyDomestic();
        PlateMapDisplayController display = RebindDisplayController(_domesticPlateRoot);
        // 国内不居中板块；瞬时归位相机与国家级缩放
        display?.SnapCameraToCountryHomeImmediate();
        RestorePlateRootOriginalPose(_domesticPlateRoot);
        EarthTransition.Instance?.ApplyPlateMapInitialPosition(WorldMapRegionCodeTable.DomesticNationalCode);
        GameManager.Instance?.NotifyRegionSwitchedToNational();
        LogManager.LogFeature(
            $"[WorldMapRegionController] 已切换 | {WorldMapRegionContext.Describe()} | " +
            $"display={(display != null ? display.name : "(null)")}");
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
            LogManager.LogFeatureWarning(
                "[WorldMapRegionController] 国外板块缺少 plateCode。" + WorldMapRegionCodeTable.PlateCodeNote);
        }

        DefaultForeignCountryCode = string.IsNullOrWhiteSpace(binding.defaultCountryCode)
            ? string.Empty
            : binding.defaultCountryCode.Trim();
        ActivePlateRoot = binding.plateRoot;
        SetRootsVisibility(activeForeign: binding);
        SetWorldModeBackgroundLineVisible(true);
        WorldMapRegionContext.ApplyForeignPlate(plateCode, plateName);
        PlateMapDisplayController display = RebindDisplayController(binding.plateRoot);
        // 国外整体位置只走 EarthTransition（AllPlateMap），不改各大板块自身 transform
        display?.SnapCameraToCountryHomeImmediate();
        RestorePlateRootOriginalPose(binding.plateRoot);
        EarthTransition.Instance?.ApplyPlateMapInitialPosition(plateCode);
        GameManager.Instance?.NotifyRegionSwitchedToNational();
        LogManager.LogFeature(
            $"[WorldMapRegionController] 已切换 | {WorldMapRegionContext.Describe()} | " +
            $"display={(display != null ? display.name : "(null)")}");
    }

    private void SetWorldModeBackgroundLineVisible(bool visible)
    {
        if (_worldModeBackgroundLine == null)
        {
            return;
        }

        if (_worldModeBackgroundLine.activeSelf != visible)
        {
            _worldModeBackgroundLine.SetActive(visible);
        }
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

    /// <summary>
    /// 解析并重绑当前大板根上的 DisplayController（只取根节点组件，避免国内省板子级误命中）。
    /// </summary>
    private PlateMapDisplayController RebindDisplayController(Transform plateRoot)
    {
        PlateMapDisplayController controller = ResolveDisplayControllerForPlate(plateRoot);
        if (controller == null)
        {
            LogManager.LogFeatureWarning(
                $"[WorldMapRegionController] 板块根缺少 PlateMapDisplayController | root={(plateRoot != null ? plateRoot.name : "(null)")}");
            return null;
        }

        // 运行时记住当前板，供序列化回退字段与 Inspector 调试
        _displayController = controller;
        controller.BindPlateMapRoot(plateRoot);
        return controller;
    }

    /// <summary>
    /// 大板根上的 DisplayController：仅 GetComponent（各大板根自带一份；勿 InChildren，国内会误取省级）。
    /// </summary>
    private PlateMapDisplayController ResolveDisplayControllerForPlate(Transform plateRoot)
    {
        if (plateRoot != null)
        {
            PlateMapDisplayController onRoot = plateRoot.GetComponent<PlateMapDisplayController>();
            if (onRoot != null)
            {
                return onRoot;
            }
        }

        if (_displayController != null)
        {
            return _displayController;
        }

        return PlateMapDisplayController.Instance;
    }

    /// <summary>恢复板块根缓存的原始本地位姿（国内回退用）。</summary>
    private void RestorePlateRootOriginalPose(Transform plateRoot)
    {
        if (plateRoot == null)
        {
            return;
        }

        ResolveDisplayControllerForPlate(plateRoot)?.RestorePlateRootOriginalLocalPose(plateRoot);
    }

    /// <summary>切换前清聚焦并瞬时归位全国相机（不播 Tween，避免国外居中用错机位）。</summary>
    private void ClearFocusBeforeRebind()
    {
        if (!_restoreNationalViewOnSwitch)
        {
            return;
        }

        // 切板前仍指向旧 ActivePlateRoot 上的 Controller
        PlateMapDisplayController controller = ResolveDisplayControllerForPlate(ActivePlateRoot);
        controller?.SnapCameraToCountryHomeImmediate();
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
