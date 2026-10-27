using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 北美洲安卓额外边界线：国家级显示、省级聚焦隐藏（对齐世界地图边界线时机）。
/// Web 等未绑定边界线时整脚本空操作，不影响现有逻辑。
/// </summary>
[DisallowMultipleComponent]
public class NorthAmericaExtraBoundaryVisibility : MonoBehaviour
{
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("边界线（可留空：Web 无额外线时不绑即可）")]
    [Tooltip("安卓北美洲额外边界线；留空或全 null 时本脚本不做事")]
    [SerializeField] private GameObject[] _boundaryLines;

    [Header("生效条件")]
    [Tooltip("仅当国外当前大板块为此 code 时显示（默认北美洲）")]
    [SerializeField] private string _targetPlateCode = "NORTH_AMERICA_REGION";

    [Header("淡入淡出")]
    [Tooltip("建议与 PlateMapDisplayController 的 Other Module Fade Duration 一致")]
    [SerializeField] private float _fadeDuration = 1f;
    [SerializeField] private Ease _fadeEase = Ease.InOutQuad;

    private readonly List<Renderer> _renderers = new List<Renderer>(16);
    private MaterialPropertyBlock _propertyBlock;
    private float _currentAlpha = 1f;
    private Tween _alphaTween;
    private bool _subscribed;
    private bool _hasBoundLines;

    private void Awake()
    {
        RefreshBoundLinesCache();
        if (!_hasBoundLines)
        {
            return;
        }

        CacheRenderers();
        ApplyAlphaImmediate(_currentAlpha);
    }

    private void OnEnable()
    {
        RefreshBoundLinesCache();
        if (!_hasBoundLines)
        {
            return;
        }

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

    /// <summary>省级聚焦：渐隐并隐藏。</summary>
    public void HideFade()
    {
        if (!_hasBoundLines)
        {
            return;
        }

        TweenAlpha(0f, _fadeDuration, _fadeEase, disableWhenHidden: true);
    }

    /// <summary>国家还原：渐显（仅当前为北美洲国外大板时）。</summary>
    public void ShowFade()
    {
        if (!_hasBoundLines || !ShouldShowForCurrentRegion())
        {
            return;
        }

        TweenAlpha(1f, _fadeDuration, _fadeEase, disableWhenHidden: true);
    }

    public void HideImmediate()
    {
        if (!_hasBoundLines)
        {
            return;
        }

        KillAlphaTween();
        ApplyAlphaImmediate(0f);
        SetBoundaryLinesActive(false);
    }

    public void ShowImmediate()
    {
        if (!_hasBoundLines || !ShouldShowForCurrentRegion())
        {
            HideImmediate();
            return;
        }

        KillAlphaTween();
        SetBoundaryLinesActive(true);
        ApplyAlphaImmediate(1f);
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnPlateMapDisplayFocus += HandleProvinceFocus;
            em.OnPlateMapRestoreCameraStarted += HandleCountryRestore;
        }

        WorldMapRegionContext.OnRegionChanged += HandleRegionChanged;
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

        WorldMapRegionContext.OnRegionChanged -= HandleRegionChanged;
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

    private void HandleRegionChanged()
    {
        SyncFromCurrentState(immediate: true);
    }

    private void SyncFromCurrentState(bool immediate)
    {
        if (!_hasBoundLines)
        {
            return;
        }

        if (!ShouldShowForCurrentRegion())
        {
            if (immediate)
            {
                HideImmediate();
            }
            else
            {
                HideFade();
            }

            return;
        }

        GameManager gm = GameManager.Instance;
        bool countryLevel = gm == null || gm.CurrentState == GameManager.ControlState.CountryLevel;
        if (countryLevel)
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

        if (gm != null && gm.CurrentState == GameManager.ControlState.ProvinceLevel)
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

    private bool ShouldShowForCurrentRegion()
    {
        if (WorldMapRegionContext.Mode != WorldMapRegionMode.Foreign)
        {
            return false;
        }

        string plate = WorldMapRegionContext.PlateCode;
        if (string.IsNullOrWhiteSpace(plate) || string.IsNullOrWhiteSpace(_targetPlateCode))
        {
            return false;
        }

        return string.Equals(plate.Trim(), _targetPlateCode.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private void TweenAlpha(float targetAlpha, float duration, Ease ease, bool disableWhenHidden)
    {
        KillAlphaTween();
        targetAlpha = Mathf.Clamp01(targetAlpha);
        CacheRenderers();

        if (targetAlpha > 0f)
        {
            SetBoundaryLinesActive(true);
        }

        if (duration <= 0f)
        {
            ApplyAlphaImmediate(targetAlpha);
            if (disableWhenHidden && targetAlpha <= 0f)
            {
                SetBoundaryLinesActive(false);
            }

            return;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlphaImmediate, targetAlpha, duration)
            .SetEase(ease)
            .SetTarget(this)
            .OnComplete(() =>
            {
                _alphaTween = null;
                if (disableWhenHidden && targetAlpha <= 0f)
                {
                    SetBoundaryLinesActive(false);
                }
            });
    }

    private void ApplyAlphaImmediate(float alpha)
    {
        _currentAlpha = Mathf.Clamp01(alpha);
        if (_renderers.Count == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < _renderers.Count; i++)
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

    private void RefreshBoundLinesCache()
    {
        _hasBoundLines = false;
        if (_boundaryLines == null || _boundaryLines.Length == 0)
        {
            return;
        }

        for (int i = 0; i < _boundaryLines.Length; i++)
        {
            if (_boundaryLines[i] != null)
            {
                _hasBoundLines = true;
                return;
            }
        }
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        if (!_hasBoundLines)
        {
            return;
        }

        for (int i = 0; i < _boundaryLines.Length; i++)
        {
            GameObject line = _boundaryLines[i];
            if (line == null)
            {
                continue;
            }

            Renderer[] found = line.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < found.Length; r++)
            {
                Renderer renderer = found[r];
                if (renderer == null)
                {
                    continue;
                }

                Material mat = renderer.sharedMaterial;
                if (mat == null || !mat.HasProperty(AlphaId))
                {
                    continue;
                }

                _renderers.Add(renderer);
            }
        }
    }

    private void SetBoundaryLinesActive(bool active)
    {
        if (_boundaryLines == null)
        {
            return;
        }

        for (int i = 0; i < _boundaryLines.Length; i++)
        {
            GameObject line = _boundaryLines[i];
            if (line == null)
            {
                continue;
            }

            if (line.activeSelf != active)
            {
                line.SetActive(active);
            }
        }
    }
}
