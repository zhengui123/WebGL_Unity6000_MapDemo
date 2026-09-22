using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 国内国家级小地图 UI（ChinaMap_RawImg）：进入国内国家级渐显，离开（回地球 / 进省 / 切国外）渐隐。
/// </summary>
[DisallowMultipleComponent]
public class ChinaMapRawImageVisibility : MonoBehaviour
{
    [SerializeField] private RawImage _rawImage;
    [Tooltip("隐藏结束时是否 SetActive(false)；默认仅 Alpha=0，组件保持启用以便继续收事件。")]
    [SerializeField] private bool _deactivateWhenHidden;
    [SerializeField] private float _fadeDuration = 0.45f;
    [SerializeField] private Ease _fadeEase = Ease.InOutQuad;

    private Tween _alphaTween;
    private float _currentAlpha;
    private bool _isShown;
    private bool _subscribed;

    private void Awake()
    {
        ResolveReferences();
        HideImmediate();
    }

    private void OnEnable()
    {
        Subscribe();
        SyncFromCurrentState(immediate: true);
    }

    private void Start()
    {
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

    /// <summary>立即隐藏：全透明。</summary>
    public void HideImmediate()
    {
        KillAlphaTween();
        ResolveReferences();
        _isShown = false;
        ApplyAlpha(0f);
        if (_deactivateWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>立即显示：不透明。</summary>
    public void ShowImmediate()
    {
        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _isShown = true;
        ApplyAlpha(1f);
    }

    public Tween ShowFade()
    {
        return ShowFade(_fadeDuration, _fadeEase);
    }

    public Tween HideFade()
    {
        return HideFade(_fadeDuration, _fadeEase);
    }

    public Tween ShowFade(float duration, Ease ease)
    {
        if (_isShown && _currentAlpha >= 0.99f)
        {
            return DOTween.Sequence().SetAutoKill(true);
        }

        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _isShown = true;
        if (_currentAlpha >= 0.99f)
        {
            ApplyAlpha(1f);
            return DOTween.Sequence().SetAutoKill(true);
        }

        if (duration <= 0f)
        {
            ShowImmediate();
            return DOTween.Sequence().AppendCallback(() => { }).SetAutoKill(true);
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlpha, 1f, duration)
            .SetEase(ease)
            .SetTarget(this);
        return _alphaTween;
    }

    public Tween HideFade(float duration, Ease ease)
    {
        if (!_isShown && _currentAlpha <= 0.01f)
        {
            HideImmediate();
            return DOTween.Sequence().SetAutoKill(true);
        }

        KillAlphaTween();
        ResolveReferences();
        gameObject.SetActive(true);
        _isShown = false;

        if (duration <= 0f)
        {
            HideImmediate();
            return DOTween.Sequence().AppendCallback(() => { }).SetAutoKill(true);
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlpha, 0f, duration)
            .SetEase(ease)
            .SetTarget(this)
            .OnComplete(() =>
            {
                if (_deactivateWhenHidden)
                {
                    gameObject.SetActive(false);
                }
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

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnTransitionToPlateMapCompleted += HandleEnterDomesticCountry;
            em.OnPlateMapRestoreCameraCompleted += HandleEnterDomesticCountry;
            em.OnTransitionToEarthStarted += HandleLeaveCountry;
            em.OnPlateMapDisplayFocus += HandleLeaveCountryNamed;
            em.OnPlateToVehicleViewTransitionStarted += HandleLeaveCountryNamed;
            em.OnPlateToGaodeMapTransitionStarted += HandleLeaveCountryNamed;
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
            em.OnTransitionToPlateMapCompleted -= HandleEnterDomesticCountry;
            em.OnPlateMapRestoreCameraCompleted -= HandleEnterDomesticCountry;
            em.OnTransitionToEarthStarted -= HandleLeaveCountry;
            em.OnPlateMapDisplayFocus -= HandleLeaveCountryNamed;
            em.OnPlateToVehicleViewTransitionStarted -= HandleLeaveCountryNamed;
            em.OnPlateToGaodeMapTransitionStarted -= HandleLeaveCountryNamed;
        }

        WorldMapRegionContext.OnRegionChanged -= HandleRegionChanged;
        _subscribed = false;
    }

    private void HandleEnterDomesticCountry()
    {
        if (WorldMapRegionContext.Mode != WorldMapRegionMode.Domestic)
        {
            HideFade();
            return;
        }

        ShowFade();
    }

    private void HandleLeaveCountry()
    {
        HideFade();
    }

    private void HandleLeaveCountryNamed(string _)
    {
        HideFade();
    }

    private void HandleRegionChanged()
    {
        SyncFromCurrentState(immediate: false);
    }

    private void SyncFromCurrentState(bool immediate)
    {
        if (ShouldShow())
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

        if (immediate)
        {
            HideImmediate();
        }
        else
        {
            HideFade();
        }
    }

    private static bool ShouldShow()
    {
        GameManager gm = GameManager.Instance;
        return gm != null
            && gm.CurrentState == GameManager.ControlState.CountryLevel
            && WorldMapRegionContext.Mode == WorldMapRegionMode.Domestic;
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
            GameObject found = GameObject.Find("ChinaMap_RawImg");
            if (found != null)
            {
                _rawImage = found.GetComponent<RawImage>();
            }
        }
    }
}
