using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图高亮控制器：高亮指定板块并恢复其余板块默认发光。
/// 通过 <see cref="PlateMapDisplayModule"/> 的 MaterialPropertyBlock 写入
/// Shader <c>Custom/PlateMapProvinceTech</c> 的 <c>_EmissionIntensity</c>。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapHighlightController : UnitySingle<PlateMapHighlightController>
{
    [Header("板块根（留空则使用当前物体）")]
    [SerializeField] private Transform _plateMapRoot;

    [Header("可高亮模块（留空则收集子级 PlateMapDisplayModule）")]
    [SerializeField] private PlateMapDisplayModule[] _modules;

    [Header("高亮动画")]
    [SerializeField] private float _highlightDuration = 0.5f;
    [SerializeField] private Ease _highlightEase = Ease.InOutQuad;

    [Header("发光强度")]
    [Tooltip("高亮板块的 _EmissionIntensity 目标值")]
    [SerializeField] private float _highlightedEmissionIntensity = 4.5f;

    private PlateMapDisplayModule _highlightedModule;

    private static PlateMapHighlightController _instance;

    /// <summary>当前高亮的模块；无则为 null。</summary>
    public PlateMapDisplayModule HighlightedModule => _highlightedModule;

    /// <summary>当前高亮模块名（ModuleKey）；无则为 null。</summary>
    public string HighlightedModuleName => _highlightedModule != null ? _highlightedModule.ModuleKey : null;

    public static PlateMapHighlightController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlateMapHighlightController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;

        if (_plateMapRoot == null)
        {
            _plateMapRoot = transform;
        }

        RefreshModuleList();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnEnable()
    {
        RefreshModuleList();
        SubscribePlateModeExitEvents();
    }

    private void OnDisable()
    {
        UnsubscribePlateModeExitEvents();
        KillAllModuleEmissionTweens();
    }

    private void SubscribePlateModeExitEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionStarted += HandlePlateModeExit;
        em.OnTransitionToEarthStarted += HandlePlateModeExit;
    }

    private void UnsubscribePlateModeExitEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionStarted -= HandlePlateModeExit;
        em.OnTransitionToEarthStarted -= HandlePlateModeExit;
    }

    private void HandlePlateModeExit(string _)
    {
        ClearHighlight();
    }

    private void HandlePlateModeExit()
    {
        ClearHighlight();
    }

    /// <summary>重新收集子级模块。</summary>
    public void RefreshModuleList()
    {
        if (_modules == null || _modules.Length == 0)
        {
            _modules = _plateMapRoot != null
                ? _plateMapRoot.GetComponentsInChildren<PlateMapDisplayModule>(true)
                : GetComponentsInChildren<PlateMapDisplayModule>(true);
        }
    }

    /// <summary>
    /// 高亮指定板块，并取消其他板块高亮（其余板块发光强度回到材质默认值）。
    /// </summary>
    /// <param name="moduleName">模块 GameObject 名或 DisplayName。</param>
    public bool HighlightModule(string moduleName)
    {
        if (!TryGetModuleByName(moduleName, out PlateMapDisplayModule module))
        {
            Debug.LogWarning($"[PlateMapHighlightController] 未找到模块：{moduleName}");
            return false;
        }

        HighlightModule(module);
        return true;
    }

    /// <summary>高亮指定板块，并取消其他板块高亮。</summary>
    public void HighlightModule(PlateMapDisplayModule module)
    {
        if (module == null)
        {
            return;
        }

        if (_highlightedModule == module && IsModuleFullyHighlighted(module))
        {
            return;
        }

        KillAllModuleEmissionTweens();
        _highlightedModule = module;
        ApplyHighlightState(module, _highlightDuration, _highlightEase);

        Debug.Log($"[PlateMapHighlightController] 高亮模块：{module.ModuleKey}");
    }

    /// <summary>取消全部板块高亮，所有模块发光强度回到材质默认值。</summary>
    public void ClearHighlight()
    {
        ClearHighlight(_highlightDuration, _highlightEase);
    }

    /// <summary>取消全部板块高亮。</summary>
    public void ClearHighlight(float duration, Ease ease)
    {
        KillAllModuleEmissionTweens();
        _highlightedModule = null;
        RestoreAllModulesEmission(duration, ease);
    }

    /// <summary>指定模块是否处于高亮态。</summary>
    public bool IsModuleHighlighted(string moduleName)
    {
        if (_highlightedModule == null || string.IsNullOrWhiteSpace(moduleName))
        {
            return false;
        }

        return _highlightedModule.ModuleKey == moduleName || _highlightedModule.DisplayName == moduleName;
    }

    private bool IsModuleFullyHighlighted(PlateMapDisplayModule module)
    {
        return module != null && Mathf.Approximately(module.CurrentEmissionIntensity, _highlightedEmissionIntensity);
    }

    private void ApplyHighlightState(PlateMapDisplayModule focusedModule, float duration, Ease ease)
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            PlateMapDisplayModule module = _modules[i];
            if (module == null)
            {
                continue;
            }

            if (module == focusedModule)
            {
                module.TweenEmissionIntensity(_highlightedEmissionIntensity, duration, ease);
            }
            else
            {
                module.RestoreEmissionIntensity(duration, ease);
            }
        }
    }

    private void RestoreAllModulesEmission(float duration, Ease ease)
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            _modules[i]?.RestoreEmissionIntensity(duration, ease);
        }
    }

    private void KillAllModuleEmissionTweens()
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            _modules[i]?.KillEmissionTween();
        }
    }

    private bool TryGetModuleByName(string moduleName, out PlateMapDisplayModule module)
    {
        module = null;
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return false;
        }

        RefreshModuleList();
        if (_modules == null)
        {
            return false;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            PlateMapDisplayModule candidate = _modules[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.ModuleKey == moduleName || candidate.DisplayName == moduleName)
            {
                module = candidate;
                return true;
            }
        }

        return false;
    }
}
