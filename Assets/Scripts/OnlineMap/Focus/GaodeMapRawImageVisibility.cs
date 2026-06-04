using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 GaodeMap 虚拟相机 RenderTexture 在 UI RawImage 上的显隐（透明度渐变）。
/// </summary>
[DisallowMultipleComponent]
public class GaodeMapRawImageVisibility : MonoBehaviour
{
    [SerializeField] private RawImage _rawImage;
    [Tooltip("隐藏时是否 SetActive(false)；默认仅 Alpha=0，便于渐显")]
    [SerializeField] private bool _deactivateWhenHidden;

    private Tween _alphaTween;
    private float _currentAlpha;

    public RawImage RawImage => _rawImage;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        KillAlphaTween();
    }

    /// <summary>初始隐藏：全透明。</summary>
    public void HideImmediate()
    {
        KillAlphaTween();
        ResolveReferences();
        _currentAlpha = 0f;
        ApplyAlpha(0f);
        if (_deactivateWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>显示并从透明渐显到不透明。</summary>
    public Tween ShowFade(float duration, Ease ease = Ease.InOutQuad)
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _currentAlpha = 0f;
        ApplyAlpha(0f);

        if (duration <= 0f)
        {
            _currentAlpha = 1f;
            ApplyAlpha(1f);
            return null;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlpha, 1f, duration)
            .SetEase(ease)
            .SetTarget(this);
        return _alphaTween;
    }

    /// <summary>从不透明渐隐到全透明。</summary>
    public Tween HideFade(float duration, Ease ease = Ease.InOutQuad)
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _currentAlpha = _rawImage != null ? _rawImage.color.a : 1f;

        if (duration <= 0f)
        {
            HideImmediate();
            return null;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlpha, 0f, duration)
            .SetEase(ease)
            .SetTarget(this);
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
        if (_rawImage == null)
        {
            return;
        }

        Color c = _rawImage.color;
        c.a = _currentAlpha;
        _rawImage.color = c;
    }

    private void ResolveReferences()
    {
        if (_rawImage == null)
        {
            _rawImage = GetComponent<RawImage>();
        }

        if (_rawImage == null)
        {
            GameObject found = GameObject.Find("GaodeMap_RawImg");
            if (found != null)
            {
                _rawImage = found.GetComponent<RawImage>();
            }
        }
    }
}
