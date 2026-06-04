using DG.Tweening;
using UnityEngine;

/// <summary>
/// 控制 GaodeMap 显示/隐藏（瓦片材质透明度；初始为全透明）。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class GaodeMapTransitionVisibility : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private GaodeMapController _gaodeMapController;
    [SerializeField] private OnlineMaps _onlineMaps;
    [SerializeField] private Renderer _tileRenderer;
    [Tooltip("隐藏时是否同时 SetActive(false)；默认仅全透明，便于渐显前预加载瓦片")]
    [SerializeField] private bool _deactivateWhenHidden;

    private Material _runtimeMaterial;
    private Tween _alphaTween;
    private float _currentAlpha;

    public float CurrentAlpha => _currentAlpha;
    public GameObject GaodeMapObject => _onlineMaps != null ? _onlineMaps.gameObject : null;

    private void Awake()
    {
        ResolveReferences();
        ApplyInitialTransparentState();
    }

    private void Start()
    {
        // OnlineMaps 首帧可能重建材质，再次锁定为全透明
        ApplyInitialTransparentState();
    }

    private void OnDestroy()
    {
        KillAlphaTween();
    }

    /// <summary>初始/隐藏：Alpha=0 全透明。</summary>
    public void ApplyInitialTransparentState()
    {
        KillAlphaTween();
        ResolveReferences();
        _currentAlpha = 0f;

        if (GaodeMapObject != null)
        {
            GaodeMapObject.SetActive(true);
        }

        InvalidateRuntimeMaterial();
        SetTileAlpha(0f);
        ApplyInteraction(false);
    }

    public void HideImmediate()
    {
        ApplyInitialTransparentState();
        if (_deactivateWhenHidden && GaodeMapObject != null)
        {
            GaodeMapObject.SetActive(false);
        }
    }

    /// <summary>激活物体并从透明渐显到不透明。</summary>
    public Tween ShowFade(float duration, Ease ease = Ease.InOutQuad)
    {
        KillAlphaTween();
        ResolveReferences();

        if (GaodeMapObject != null)
        {
            GaodeMapObject.SetActive(true);
        }

        PrepareMapForFadeIn();
        _currentAlpha = 0f;
        SetTileAlpha(0f);

        if (duration <= 0f)
        {
            _currentAlpha = 1f;
            SetTileAlpha(1f);
            ApplyInteraction(true);
            RefreshMap();
            return null;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlpha, 1f, duration)
            .SetEase(ease)
            .SetTarget(this)
            .OnComplete(() =>
            {
                ApplyInteraction(true);
                RefreshMap();
            });
        return _alphaTween;
    }

    public void KillAlphaTween()
    {
        if (_alphaTween != null && _alphaTween.IsActive())
        {
            _alphaTween.Kill();
        }

        _alphaTween = null;
    }

    private void ApplyAlpha(float alpha)
    {
        _currentAlpha = Mathf.Clamp01(alpha);
        SetTileAlpha(_currentAlpha);
    }

    private void ResolveReferences()
    {
        if (_gaodeMapController == null)
        {
            _gaodeMapController = GetComponent<GaodeMapController>();
        }

        if (_gaodeMapController == null)
        {
            _gaodeMapController = GaodeMapController.Instance;
        }

        if (_onlineMaps == null && _gaodeMapController != null)
        {
            _onlineMaps = _gaodeMapController.OnlineMaps;
        }

        if (_tileRenderer == null && _onlineMaps != null)
        {
            _tileRenderer = _onlineMaps.GetComponent<Renderer>();
            if (_tileRenderer == null)
            {
                _tileRenderer = _onlineMaps.GetComponentInChildren<Renderer>(true);
            }
        }
    }

    private void InvalidateRuntimeMaterial()
    {
        _runtimeMaterial = null;
    }

    private void EnsureRuntimeMaterial()
    {
        if (_tileRenderer == null)
        {
            return;
        }

        if (_runtimeMaterial == null)
        {
            _runtimeMaterial = _tileRenderer.sharedMaterial;
        }
    }

    private void SetTileAlpha(float alpha)
    {
        EnsureRuntimeMaterial();
        if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(ColorId))
        {
            Color c = _runtimeMaterial.color;
            c.a = Mathf.Clamp01(alpha);
            _runtimeMaterial.color = c;
        }
    }

    private void ApplyInteraction(bool enabled)
    {
        if (_onlineMaps == null)
        {
            return;
        }

        _onlineMaps.blockAllInteractions = !enabled;
    }

    private void PrepareMapForFadeIn()
    {
        if (_onlineMaps == null)
        {
            return;
        }

        _onlineMaps.allowRedraw = true;
        _onlineMaps.needRedraw = true;
        _onlineMaps.RedrawImmediately();
        SetTileAlpha(_currentAlpha);
    }

    private void RefreshMap()
    {
        if (_onlineMaps == null)
        {
            return;
        }

        _onlineMaps.RedrawImmediately();
        SetTileAlpha(_currentAlpha);
    }
}
