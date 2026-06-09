using DG.Tweening;
using UnityEngine;

/// <summary>
/// 城市隐藏/显现过渡：正播剔除 City 层 + RawImage 渐隐；倒播 RawImage 渐显 + 恢复 City 层与模型显示。
/// </summary>
[DisallowMultipleComponent]
public class CityHideTransitionController : MonoBehaviour
{
    [Header("场景引用（留空则自动查找）")]
    [SerializeField] private CityRawImageVisibility _cityRawImageVisibility;
    [SerializeField] private PlateToGaodeMapScanlineOverlay _scanlineOverlay;
    [SerializeField] private GameObject _cityMakerRoot;
    [SerializeField] private GameObject _cityModelRoot;
    [SerializeField] private Camera _mainCamera;

    [Header("层级")]
    [SerializeField] private string _cityLayerName = "City";

    [Header("过渡参数")]
    [SerializeField] private float _hideDuration = 0.7f;
    [SerializeField] private Ease _hideEase = Ease.InOutQuad;
    [SerializeField] private Ease _scanlineEase = Ease.InOutCubic;

    private Sequence _sequence;
    private bool _isTransitioning;
    private int _cityLayerMask;
    private int _cachedCullingMask;
    private bool _hasCachedCullingMask;

    public bool IsTransitioning => _isTransitioning;

    private static CityHideTransitionController _instance;

    public static CityHideTransitionController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CityHideTransitionController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
        CacheCityLayerMask();
        EnsureCityRawImageHiddenAtStart();
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 正播隐藏：保持 City 激活并剔除相机 City 层 → RawImage 渐隐 + 扫描带 → 结束后隐藏 City。
    /// </summary>
    public bool PlayHideTransition()
    {
        if (!TryBeginTransition())
        {
            return false;
        }

        PrepareHidePhase();

        _sequence = DOTween.Sequence();
        AppendForwardOverlayPhase(_sequence);
        _sequence.OnComplete(CompleteHideTransition);
        return true;
    }

    /// <summary>
    /// 倒播显现：显示 City 并剔除 City 层 → RawImage 渐显 + 扫描带收回 → 恢复相机 City 层。
    /// </summary>
    public bool PlayHideTransitionReverse()
    {
        if (!TryBeginTransition())
        {
            return false;
        }

        PrepareReversePhase();

        _sequence = DOTween.Sequence();
        AppendReverseOverlayPhase(_sequence);
        _sequence.OnComplete(CompleteReverseTransition);
        return true;
    }

    private bool TryBeginTransition()
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_cityRawImageVisibility == null)
        {
            Debug.LogError("[CityHideTransition] 未找到 CityRawImageVisibility / City_RawImg。");
            return false;
        }

        if (_mainCamera == null)
        {
            Debug.LogError("[CityHideTransition] 未找到主摄像机。");
            return false;
        }

        _isTransitioning = true;
        KillSequence();
        return true;
    }

    /// <summary>正播准备：City 保持激活；剔除 City 层；RawImage 不透明。</summary>
    private void PrepareHidePhase()
    {
        ExcludeCityLayerFromMainCamera();
        _cityRawImageVisibility.ShowImmediate();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.SetVisible(true);
            _scanlineOverlay.SetProgressImmediate(0f);
        }
    }

    /// <summary>倒播准备：显示 City；剔除 City 层；RawImage 全透明待渐显；扫描带从 1 收回。</summary>
    private void PrepareReversePhase()
    {
        ShowCityModel();
        ExcludeCityLayerFromMainCamera();
        _cityRawImageVisibility.ShowTransparentImmediate();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.SetVisible(true);
            _scanlineOverlay.SetProgressImmediate(1f);
        }
    }

    private void AppendForwardOverlayPhase(Sequence sequence)
    {
        Tween scanTween = BuildForwardScanlineTween();
        Tween rawHideTween = _cityRawImageVisibility.HideFade(_hideDuration, _hideEase);
        AppendOverlayTweens(sequence, scanTween, rawHideTween, _hideDuration);
    }

    private void AppendReverseOverlayPhase(Sequence sequence)
    {
        Tween scanTween = BuildReverseScanlineTween();
        Tween rawShowTween = _cityRawImageVisibility.ShowFade(_hideDuration, _hideEase);
        AppendOverlayTweens(sequence, scanTween, rawShowTween, _hideDuration);
    }

    private static void AppendOverlayTweens(Sequence sequence, Tween scanTween, Tween rawTween, float fallbackDuration)
    {
        if (scanTween != null)
        {
            sequence.Append(scanTween);
            if (rawTween != null)
            {
                sequence.Join(rawTween);
            }
        }
        else if (rawTween != null)
        {
            sequence.Append(rawTween);
        }
        else
        {
            sequence.AppendInterval(fallbackDuration);
        }
    }

    private void ExcludeCityLayerFromMainCamera()
    {
        if (_mainCamera == null)
        {
            return;
        }

        if (!_hasCachedCullingMask)
        {
            _cachedCullingMask = _mainCamera.cullingMask;
            _hasCachedCullingMask = true;
        }

        if (_cityLayerMask != 0)
        {
            _mainCamera.cullingMask &= ~_cityLayerMask;
        }
    }

    private Tween BuildForwardScanlineTween()
    {
        if (_scanlineOverlay == null)
        {
            return null;
        }

        return _scanlineOverlay.TweenProgress(1f, _hideDuration, _scanlineEase);
    }

    private Tween BuildReverseScanlineTween()
    {
        if (_scanlineOverlay == null)
        {
            return null;
        }

        return _scanlineOverlay.TweenProgressFromTo(1f, 0f, _hideDuration, _scanlineEase);
    }

    /// <summary>正播结束：隐藏 RawImage 与 City 模型。</summary>
    private void CompleteHideTransition()
    {
        _isTransitioning = false;
        _cityRawImageVisibility?.HideImmediate();
        HideCityModel();
        CleanupScanlineOverlay();
    }

    /// <summary>倒播结束：恢复相机 City 层，关闭 RawImage，City 模型保持显示。</summary>
    private void CompleteReverseTransition()
    {
        _isTransitioning = false;
        RestoreMainCameraCullingMask();
        _cityRawImageVisibility?.HideImmediate();
        CleanupScanlineOverlay();
    }

    private void CleanupScanlineOverlay()
    {
        if (_scanlineOverlay == null)
        {
            return;
        }

        _scanlineOverlay.KillProgressTween();
        _scanlineOverlay.SetProgressImmediate(0f);
        _scanlineOverlay.SetVisible(false);
    }

    private void EnsureCityRawImageHiddenAtStart()
    {
        if (_cityRawImageVisibility != null)
        {
            _cityRawImageVisibility.HideImmediate();
        }
    }

    private void ShowCityModel()
    {
        if (_cityModelRoot != null)
        {
            _cityModelRoot.SetActive(true);
            return;
        }

        if (_cityMakerRoot != null)
        {
            Transform city = _cityMakerRoot.transform.Find("City");
            if (city != null)
            {
                city.gameObject.SetActive(true);
            }
        }
    }

    private void HideCityModel()
    {
        if (_cityModelRoot != null)
        {
            _cityModelRoot.SetActive(false);
            return;
        }

        if (_cityMakerRoot != null)
        {
            Transform city = _cityMakerRoot.transform.Find("City");
            if (city != null)
            {
                city.gameObject.SetActive(false);
            }
        }
    }

    private void CacheCityLayerMask()
    {
        int layer = LayerMask.NameToLayer(_cityLayerName);
        _cityLayerMask = layer >= 0 ? 1 << layer : 0;
    }

    private void RestoreMainCameraCullingMask()
    {
        if (_mainCamera == null || !_hasCachedCullingMask)
        {
            return;
        }

        _mainCamera.cullingMask = _cachedCullingMask;
        _hasCachedCullingMask = false;
    }

    private void ResolveReferences()
    {
        if (_cityRawImageVisibility == null)
        {
            _cityRawImageVisibility = FindFirstObjectByType<CityRawImageVisibility>();
        }

        if (_cityRawImageVisibility == null)
        {
            GameObject rawImgGo = GameObject.Find("City_RawImg");
            if (rawImgGo != null)
            {
                _cityRawImageVisibility = rawImgGo.GetComponent<CityRawImageVisibility>();
            }
        }

        if (_scanlineOverlay == null)
        {
            _scanlineOverlay = FindFirstObjectByType<PlateToGaodeMapScanlineOverlay>();
        }

        if (_cityMakerRoot == null)
        {
            GameObject found = GameObject.Find("City-Maker");
            if (found != null)
            {
                _cityMakerRoot = found;
            }
        }

        if (_cityModelRoot == null && _cityMakerRoot != null)
        {
            Transform city = _cityMakerRoot.transform.Find("City");
            if (city != null)
            {
                _cityModelRoot = city.gameObject;
            }
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
    }

    private void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
            RestoreMainCameraCullingMask();
        }

        _sequence = null;
        _cityRawImageVisibility?.KillAlphaTween();
        _scanlineOverlay?.KillProgressTween();
        _isTransitioning = false;
    }

#if UNITY_EDITOR
    [ContextMenu("测试：城市隐藏过渡")]
    private void EditorTestHideTransition()
    {
        PlayHideTransition();
    }

    [ContextMenu("测试：城市隐藏倒播")]
    private void EditorTestHideTransitionReverse()
    {
        PlayHideTransitionReverse();
    }
#endif
}
