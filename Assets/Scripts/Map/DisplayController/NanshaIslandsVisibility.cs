using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 南沙群岛材质显隐：进省聚焦时渐隐，国家相机还原时渐显。
/// 通过 MaterialPropertyBlock 写 <c>_Alpha</c>（对齐 <see cref="PlateMapDisplayModule"/>），请手动挂到南沙根节点。
/// </summary>
[DisallowMultipleComponent]
public class NanshaIslandsVisibility : MonoBehaviour
{
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Tooltip("渐隐/渐显时长（秒），建议与 PlateMapDisplayController 的 Other Module Fade Duration 一致")]
    [SerializeField] private float _fadeDuration = 1f;
    [SerializeField] private Ease _fadeEase = Ease.InOutQuad;

    [Tooltip("为 true 时只收集 Custom/PlateMapProvinceTech；为 false 时收集所有带 _Alpha 的 Renderer")]
    [SerializeField] private bool _onlyPlateMapProvinceTech = true;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private float _currentAlpha = 1f;
    private Tween _alphaTween;
    private bool _subscribed;

    private void Awake()
    {
        CacheRenderers();
        ApplyAlphaImmediate(_currentAlpha);
    }

    private void OnEnable()
    {
        Subscribe();
        SyncFromCurrentState(immediate: true);
    }

    private void OnDisable()
    {
        Unsubscribe();
        KillAlphaTween();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        KillAlphaTween();
    }

    /// <summary>省聚焦：渐隐。</summary>
    public void HideFade()
    {
        TweenAlpha(0f, _fadeDuration, _fadeEase);
    }

    /// <summary>国家还原：渐显。</summary>
    public void ShowFade()
    {
        TweenAlpha(1f, _fadeDuration, _fadeEase);
    }

    public void HideImmediate()
    {
        KillAlphaTween();
        ApplyAlphaImmediate(0f);
    }

    public void ShowImmediate()
    {
        KillAlphaTween();
        ApplyAlphaImmediate(1f);
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateMapDisplayFocus += HandleProvinceFocus;
        // 与 FadeAllModulesForRestore 同步：还原开始即渐显，而非等相机结束
        em.OnPlateMapRestoreCameraStarted += HandleCountryRestore;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnPlateMapDisplayFocus -= HandleProvinceFocus;
            em.OnPlateMapRestoreCameraStarted -= HandleCountryRestore;
        }

        _subscribed = false;
    }

    private void HandleProvinceFocus(string _)
    {
        HideFade();
    }

    private void HandleCountryRestore()
    {
        ShowFade();
    }

    private void SyncFromCurrentState(bool immediate)
    {
        GameManager gm = GameManager.Instance;
        bool show = gm == null || gm.CurrentState == GameManager.ControlState.CountryLevel;

        if (show)
        {
            if (immediate)
            {
                ShowImmediate();
            }
            else
            {
                ShowFade();
            }

            return;
        }

        // 省级及以下：保持隐藏（本脚本只管省聚焦↔国家还原，不处理地球/车辆事件）
        if (gm.CurrentState == GameManager.ControlState.ProvinceLevel)
        {
            if (immediate)
            {
                HideImmediate();
            }
            else
            {
                HideFade();
            }
        }
    }

    private void TweenAlpha(float targetAlpha, float duration, Ease ease)
    {
        KillAlphaTween();
        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (duration <= 0f)
        {
            ApplyAlphaImmediate(targetAlpha);
            return;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlphaImmediate, targetAlpha, duration)
            .SetEase(ease)
            .SetTarget(this);
    }

    private void ApplyAlphaImmediate(float alpha)
    {
        _currentAlpha = Mathf.Clamp01(alpha);
        CacheRenderers();
        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(AlphaId, _currentAlpha);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void KillAlphaTween()
    {
        if (_alphaTween != null && _alphaTween.IsActive())
        {
            _alphaTween.Kill();
        }

        _alphaTween = null;
    }

    private void CacheRenderers()
    {
        if (_renderers != null && _renderers.Length > 0)
        {
            return;
        }

        var candidates = new List<Renderer>();
        GetComponentsInChildren(true, candidates);

        var list = new List<Renderer>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            Renderer renderer = candidates[i];
            if (renderer == null)
            {
                continue;
            }

            Material mat = renderer.sharedMaterial;
            if (mat == null || !mat.HasProperty(AlphaId))
            {
                continue;
            }

            if (_onlyPlateMapProvinceTech)
            {
                if (mat.shader != null && mat.shader.name == "Custom/PlateMapProvinceTech")
                {
                    list.Add(renderer);
                }
            }
            else
            {
                list.Add(renderer);
            }
        }

        _renderers = list.ToArray();
        if (_renderers.Length == 0)
        {
            LogManager.LogFeatureWarning(
                $"[NanshaIslandsVisibility] {name} 未找到可用 Renderer（需材质带 _Alpha" +
                (_onlyPlateMapProvinceTech ? " 且为 Custom/PlateMapProvinceTech" : string.Empty) + "）。");
        }
    }
}
