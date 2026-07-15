using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图高亮控制器：支持单模块或多模块同时高亮。
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

    private readonly HashSet<PlateMapDisplayModule> _highlightedModules = new HashSet<PlateMapDisplayModule>();

    private static PlateMapHighlightController _instance;

    /// <summary>当前高亮模块之一（兼容旧调用；多高亮时取任意一个）。</summary>
    public PlateMapDisplayModule HighlightedModule
    {
        get
        {
            foreach (PlateMapDisplayModule module in _highlightedModules)
            {
                if (module != null)
                {
                    return module;
                }
            }

            return null;
        }
    }

    /// <summary>当前高亮模块名（ModuleKey）；无则为 null。</summary>
    public string HighlightedModuleName
    {
        get
        {
            PlateMapDisplayModule module = HighlightedModule;
            return module != null ? module.ModuleKey : null;
        }
    }

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
        SubscribeCountryExitEvents();
    }

    private void OnDisable()
    {
        UnsubscribeCountryExitEvents();
        KillAllModuleEmissionTweens();
    }

    /// <summary>退出国家级时取消高亮（进入省级聚焦 / 返回地球）。</summary>
    private void SubscribeCountryExitEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateMapDisplayFocus += HandleCountryExit;
        em.OnTransitionToEarthStarted += HandleCountryExit;
    }

    private void UnsubscribeCountryExitEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateMapDisplayFocus -= HandleCountryExit;
        em.OnTransitionToEarthStarted -= HandleCountryExit;
    }

    private void HandleCountryExit(string _)
    {
        ClearHighlight();
    }

    private void HandleCountryExit()
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

        HighlightModules(new[] { module });
    }

    /// <summary>同时高亮多个板块（按模块名）；未命中的名字会跳过。</summary>
    public void HighlightModulesByName(IReadOnlyList<string> moduleNames)
    {
        if (moduleNames == null || moduleNames.Count == 0)
        {
            ClearHighlight();
            return;
        }

        List<PlateMapDisplayModule> modules = new List<PlateMapDisplayModule>(moduleNames.Count);
        for (int i = 0; i < moduleNames.Count; i++)
        {
            if (TryGetModuleByName(moduleNames[i], out PlateMapDisplayModule module) && module != null)
            {
                modules.Add(module);
            }
            else
            {
                Debug.LogWarning($"[PlateMapHighlightController] 未找到模块：{moduleNames[i]}");
            }
        }

        HighlightModules(modules);
    }

    /// <summary>同时高亮多个板块，其余恢复默认发光。</summary>
    public void HighlightModules(IReadOnlyList<PlateMapDisplayModule> modules)
    {
        KillAllModuleEmissionTweens();
        _highlightedModules.Clear();

        if (modules != null)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null)
                {
                    _highlightedModules.Add(modules[i]);
                }
            }
        }

        ApplyHighlightState(_highlightDuration, _highlightEase);
        Debug.Log($"[PlateMapHighlightController] 高亮模块数：{_highlightedModules.Count}");
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
        _highlightedModules.Clear();
        RestoreAllModulesEmission(duration, ease);
    }

    /// <summary>指定模块是否处于高亮态。</summary>
    public bool IsModuleHighlighted(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName) || _highlightedModules.Count == 0)
        {
            return false;
        }

        foreach (PlateMapDisplayModule module in _highlightedModules)
        {
            if (module != null &&
                (module.ModuleKey == moduleName || module.DisplayName == moduleName))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyHighlightState(float duration, Ease ease)
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

            if (_highlightedModules.Contains(module))
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
