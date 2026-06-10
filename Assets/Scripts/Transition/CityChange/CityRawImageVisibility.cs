using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制 City_RawImg 的显隐与透明度渐变。默认关闭 GameObject，隐藏时仅 Alpha=0。
/// </summary>
[DisallowMultipleComponent]
public class CityRawImageVisibility : MonoBehaviour
{
    [SerializeField] private RawImage _rawImage;
    [Tooltip("隐藏时是否 SetActive(false)；城市过渡默认关闭 City_RawImg")]
    [SerializeField] private bool _deactivateWhenHidden = true;

    private Tween _alphaTween;
    private float _currentAlpha;

    public RawImage RawImage => _rawImage;

    private void Awake()
    {
        ResolveReferences();
        HideImmediate();
    }

    private void OnDestroy()
    {
        KillAlphaTween();
    }

    /// <summary>立即隐藏：全透明，并按配置关闭 GameObject。</summary>
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

    /// <summary>开启 RawImage 并设为不透明（准备城市隐藏前显示快照）。</summary>
    public void ShowImmediate()
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _currentAlpha = 1f;
        ApplyAlpha(1f);
    }

    /// <summary>开启 RawImage 并设为全透明（倒播渐显起点）。</summary>
    public void ShowTransparentImmediate()
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _currentAlpha = 0f;
        ApplyAlpha(0f);
    }

    /// <summary>从全透明渐显到不透明。</summary>
    public Tween ShowFade(float duration, Ease ease = Ease.InOutQuad)
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _currentAlpha = 0f;
        ApplyAlpha(0f);

        if (duration <= 0f)
        {
            ShowImmediate();
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
            GameObject found = GameObject.Find("City_RawImg");
            if (found != null)
            {
                _rawImage = found.GetComponent<RawImage>();
            }
        }
    }
}
