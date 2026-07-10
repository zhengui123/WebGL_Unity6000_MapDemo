using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏过渡叠层：旧版扫描带 + 新版科技感效果按权重叠加。
/// </summary>
[DisallowMultipleComponent]
public class PlateToGaodeMapScanlineOverlay : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ScanIntensityId = Shader.PropertyToID("_ScanIntensity");
    private static readonly int LegacyIntensityId = Shader.PropertyToID("_LegacyIntensity");
    private static readonly int LegacyWeightId = Shader.PropertyToID("_LegacyWeight");
    private static readonly int TechWeightId = Shader.PropertyToID("_TechWeight");
    private static readonly int RadialSoftnessId = Shader.PropertyToID("_RadialSoftness");
    private static readonly int LegacyRadialSoftnessId = Shader.PropertyToID("_LegacyRadialSoftness");
    private static readonly int LegacyBandWidthId = Shader.PropertyToID("_LegacyBandWidth");
    private static readonly int LegacyScanSpeedId = Shader.PropertyToID("_LegacyScanSpeed");
    private static readonly int GridDensityId = Shader.PropertyToID("_GridDensity");
    private static readonly int GridLineWidthId = Shader.PropertyToID("_GridLineWidth");
    private static readonly int EdgeGlowWidthId = Shader.PropertyToID("_EdgeGlowWidth");
    private static readonly int TrailWidthId = Shader.PropertyToID("_TrailWidth");
    private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
    private static readonly int ScanLineSpeedId = Shader.PropertyToID("_ScanLineSpeed");
    private static readonly int PulseStrengthId = Shader.PropertyToID("_PulseStrength");
    private static readonly int StreakStrengthId = Shader.PropertyToID("_StreakStrength");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
    private static readonly int LegacyTintColorId = Shader.PropertyToID("_LegacyTintColor");

    [SerializeField] private Material _overlayMaterial;

    [Header("层叠权重")]
    [SerializeField] private float _legacyWeight = 0.65f;
    [SerializeField] private float _techWeight = 0.75f;
    [SerializeField] private float _legacyIntensity = 0.55f;
    [SerializeField] private float _scanIntensity = 0.85f;
    [SerializeField] private float _pulseIntensityDuringTransition = 1.12f;

    [Header("旧版扫描带")]
    [SerializeField] private float _legacyRadialSoftness = 1f;
    [SerializeField] private float _legacyBandWidth = 0.08f;
    [SerializeField] private float _legacyScanSpeed = 0.35f;
    [SerializeField] private Color _legacyTintColor = new Color(0.1f, 0.65f, 1f, 0.72f);

    [Header("新版科技感")]
    [SerializeField] private float _radialSoftness = 0.58f;
    [SerializeField] private float _gridDensity = 36f;
    [SerializeField] private float _gridLineWidth = 0.012f;
    [SerializeField] private float _edgeGlowWidth = 0.028f;
    [SerializeField] private float _trailWidth = 0.14f;
    [SerializeField] private float _noiseAmount = 0.32f;
    [SerializeField] private float _scanLineSpeed = 0.55f;
    [SerializeField] private float _pulseStrength = 0.45f;
    [SerializeField] private float _streakStrength = 0.3f;
    [SerializeField] private Color _tintColor = new Color(0.05f, 0.75f, 1f, 0.85f);
    [SerializeField] private Color _edgeColor = new Color(0.85f, 0.97f, 1f, 1f);
    [SerializeField] private Color _gridColor = new Color(0.15f, 0.55f, 1f, 0.6f);

    [SerializeField] private Canvas _canvas;
    [SerializeField] private RawImage _rawImage;

    private Tween _progressTween;
    private Tween _intensityTween;
    private float _baseScanIntensity;

    private void Awake()
    {
        _baseScanIntensity = _scanIntensity;
        EnsureOverlayMaterial();
        EnsureUi();
        ApplyVisualSettings();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        KillProgressTween();
        KillIntensityTween();
    }

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(visible);
            _canvas.enabled = visible;
        }
    }

    public void SetProgressImmediate(float progress)
    {
        if (_overlayMaterial == null)
        {
            return;
        }

        ApplyVisualSettings();
        _overlayMaterial.SetFloat(ProgressId, Mathf.Clamp01(progress));
    }

    public Tween TweenProgress(float to, float duration, Ease ease = Ease.InOutCubic)
    {
        EnsureUi();
        float from = _overlayMaterial != null ? _overlayMaterial.GetFloat(ProgressId) : 0f;
        return TweenProgressFromTo(from, to, duration, ease);
    }

    /// <summary>指定起止 Progress 的扫描线动画（倒播时须从 1→0）。</summary>
    public Tween TweenProgressFromTo(float from, float to, float duration, Ease ease = Ease.InOutCubic)
    {
        KillProgressTween();
        EnsureUi();
        if (_overlayMaterial == null)
        {
            return null;
        }

        if (duration <= 0f)
        {
            SetVisible(true);
            ApplyVisualSettings();
            SetProgressImmediate(to);
            return DOTween.Sequence().AppendCallback(() => { }).SetAutoKill(true);
        }

        float current = from;
        _progressTween = DOTween.To(
            () => current,
            v =>
            {
                current = v;
                _overlayMaterial.SetFloat(ProgressId, v);
            },
            Mathf.Clamp01(to),
            duration
        )
            .OnStart(() =>
            {
                SetVisible(true);
                ApplyVisualSettings();
                BeginTransitionPulse(duration);
                current = from;
                _overlayMaterial.SetFloat(ProgressId, from);
            })
            .SetEase(ease)
            .SetTarget(this);
        return _progressTween;
    }

    public void KillProgressTween()
    {
        if (_progressTween != null && _progressTween.IsActive())
        {
            _progressTween.Kill();
        }

        _progressTween = null;
        KillIntensityTween();
        RestoreBaseIntensity();
    }

    private void BeginTransitionPulse(float duration)
    {
        KillIntensityTween();
        if (_overlayMaterial == null || duration <= 0f || _pulseIntensityDuringTransition <= _baseScanIntensity)
        {
            return;
        }

        _overlayMaterial.SetFloat(ScanIntensityId, _baseScanIntensity);
        _intensityTween = DOTween.To(
            () => _baseScanIntensity,
            v => _overlayMaterial.SetFloat(ScanIntensityId, v),
            _pulseIntensityDuringTransition,
            duration * 0.45f
        )
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo)
            .SetTarget(this);
    }

    private void KillIntensityTween()
    {
        if (_intensityTween != null && _intensityTween.IsActive())
        {
            _intensityTween.Kill();
        }

        _intensityTween = null;
    }

    private void RestoreBaseIntensity()
    {
        if (_overlayMaterial != null)
        {
            _overlayMaterial.SetFloat(ScanIntensityId, _baseScanIntensity);
        }
    }

    private void ApplyVisualSettings()
    {
        if (_overlayMaterial == null)
        {
            return;
        }

        _overlayMaterial.SetFloat(LegacyWeightId, _legacyWeight);
        _overlayMaterial.SetFloat(TechWeightId, _techWeight);
        _overlayMaterial.SetFloat(LegacyIntensityId, _legacyIntensity);
        _overlayMaterial.SetFloat(ScanIntensityId, _scanIntensity);
        _overlayMaterial.SetFloat(LegacyRadialSoftnessId, _legacyRadialSoftness);
        _overlayMaterial.SetFloat(LegacyBandWidthId, _legacyBandWidth);
        _overlayMaterial.SetFloat(LegacyScanSpeedId, _legacyScanSpeed);
        _overlayMaterial.SetColor(LegacyTintColorId, _legacyTintColor);
        _overlayMaterial.SetFloat(RadialSoftnessId, _radialSoftness);
        _overlayMaterial.SetFloat(GridDensityId, _gridDensity);
        _overlayMaterial.SetFloat(GridLineWidthId, _gridLineWidth);
        _overlayMaterial.SetFloat(EdgeGlowWidthId, _edgeGlowWidth);
        _overlayMaterial.SetFloat(TrailWidthId, _trailWidth);
        _overlayMaterial.SetFloat(NoiseAmountId, _noiseAmount);
        _overlayMaterial.SetFloat(ScanLineSpeedId, _scanLineSpeed);
        _overlayMaterial.SetFloat(PulseStrengthId, _pulseStrength);
        _overlayMaterial.SetFloat(StreakStrengthId, _streakStrength);
        _overlayMaterial.SetColor(TintColorId, _tintColor);
        _overlayMaterial.SetColor(EdgeColorId, _edgeColor);
        _overlayMaterial.SetColor(GridColorId, _gridColor);
    }

    private void EnsureOverlayMaterial()
    {
        if (_overlayMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/PlateToGaodeScanlineOverlay");
        if (shader != null)
        {
            _overlayMaterial = new Material(shader);
        }
    }

    private void EnsureUi()
    {
        if (_canvas != null && _rawImage != null)
        {
            return;
        }

        GameObject root = new GameObject("PlateToGaodeScanlineCanvas");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject imageGo = new GameObject("ScanlineRawImage");
        imageGo.transform.SetParent(root.transform, false);
        RectTransform rt = imageGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _rawImage = imageGo.AddComponent<RawImage>();
        _rawImage.raycastTarget = false;
        if (_overlayMaterial != null)
        {
            _rawImage.material = _overlayMaterial;
            _rawImage.texture = Texture2D.whiteTexture;
            _rawImage.color = Color.white;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_overlayMaterial != null)
        {
            ApplyVisualSettings();
        }
    }
#endif
}
