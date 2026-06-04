using DG.Tweening;
using UnityEngine;

/// <summary>
/// AllPlateMap ↔ GaodeMap：扫描线过渡 + 板块淡入淡出 + GaodeMap_RawImg 渐显/渐隐。
/// </summary>
[DisallowMultipleComponent]
public class PlateToGaodeMapTransitionController : MonoBehaviour
{
    [Header("场景对象（留空则自动查找）")]
    [SerializeField] private GameObject _allPlateMapRoot;
    [SerializeField] private GaodeMapController _gaodeMapController;

    [Header("组件引用")]
    [SerializeField] private GaodeMapProvinceFocusController _provinceFocusController;
    [SerializeField] private GaodeMapRawImageVisibility _gaodeRawImageVisibility;
    [SerializeField] private PlateToGaodeMapScanlineOverlay _scanlineOverlay;

    [Header("过渡参数")]
    [SerializeField] private float _transitionDuration = 2.5f;
    [SerializeField] private float _plateFadeDuration = 2f;
    [SerializeField] private float _gaodeFadeDuration = 2f;
    [SerializeField] private Ease _plateFadeEase = Ease.InOutQuad;
    [SerializeField] private Ease _gaodeFadeEase = Ease.InOutQuad;
    [SerializeField] private string _defaultProvinceName = "山东";

    private Sequence _sequence;
    private PlateMapDisplayModule[] _plateModules;
    private bool _isTransitioning;
    private string _activeProvinceName;

    public bool IsTransitioning => _isTransitioning;

    private static PlateToGaodeMapTransitionController _instance;

    public static PlateToGaodeMapTransitionController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlateToGaodeMapTransitionController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
        CachePlateModules();
        HideGaodeRawImageAtStart();
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>播放过渡：扫描线 + AllPlateMap 淡出 + GaodeMap_RawImg 渐显。</summary>
    public bool PlayTransition(string provinceName = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_allPlateMapRoot == null)
        {
            Debug.LogError("[PlateToGaodeMapTransition] 未找到 AllPlateMap。");
            return false;
        }

        if (_gaodeMapController == null || _gaodeMapController.OnlineMaps == null)
        {
            Debug.LogError("[PlateToGaodeMapTransition] 未找到 GaodeMapController.OnlineMaps。");
            return false;
        }

        if (_gaodeRawImageVisibility == null)
        {
            Debug.LogError("[PlateToGaodeMapTransition] 未找到 GaodeMapRawImageVisibility。");
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceName);
        _isTransitioning = true;
        KillSequence();

        PrepareGaodeMapView(_activeProvinceName);

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.SetVisible(true);
            _scanlineOverlay.SetProgressImmediate(0f);
        }

        FadeAllPlateModules(0f, _plateFadeDuration);

        _sequence = DOTween.Sequence();
        Tween scanTween = _scanlineOverlay != null
            ? _scanlineOverlay.TweenProgress(1f, _transitionDuration, Ease.Linear)
            : null;
        Tween gaodeTween = _gaodeRawImageVisibility.ShowFade(_gaodeFadeDuration, _gaodeFadeEase);

        if (scanTween != null)
        {
            _sequence.Append(scanTween);
            if (gaodeTween != null)
            {
                _sequence.Join(gaodeTween);
            }
        }
        else if (gaodeTween != null)
        {
            _sequence.Append(gaodeTween);
        }
        else
        {
            _sequence.AppendInterval(_gaodeFadeDuration);
        }

        _sequence.OnComplete(CompleteTransition);
        EventManager.Instance?.TriggerPlateToGaodeMapTransitionStarted(_activeProvinceName);
        return true;
    }

    /// <summary>倒放过渡：扫描线收回 + AllPlateMap 淡入 + GaodeMap_RawImg 渐隐。</summary>
    public bool PlayTransitionReverse(string provinceName = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_allPlateMapRoot == null)
        {
            Debug.LogError("[PlateToGaodeMapTransition] 未找到 AllPlateMap。");
            return false;
        }

        if (_gaodeRawImageVisibility == null)
        {
            Debug.LogError("[PlateToGaodeMapTransition] 未找到 GaodeMapRawImageVisibility。");
            return false;
        }

        _activeProvinceName = ResolveProvinceName(provinceName);
        _isTransitioning = true;
        KillSequence();

        _allPlateMapRoot.SetActive(true);

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.SetVisible(true);
            _scanlineOverlay.SetProgressImmediate(1f);
        }

        FadeAllPlateModules(1f, _plateFadeDuration);

        _sequence = DOTween.Sequence();
        Tween scanTween = _scanlineOverlay != null
            ? _scanlineOverlay.TweenProgress(0f, _transitionDuration, Ease.Linear)
            : null;
        Tween gaodeTween = _gaodeRawImageVisibility.HideFade(_gaodeFadeDuration, _gaodeFadeEase);

        if (scanTween != null)
        {
            _sequence.Append(scanTween);
            if (gaodeTween != null)
            {
                _sequence.Join(gaodeTween);
            }
        }
        else if (gaodeTween != null)
        {
            _sequence.Append(gaodeTween);
        }
        else
        {
            _sequence.AppendInterval(_gaodeFadeDuration);
        }

        _sequence.OnComplete(CompleteTransitionReverse);
        EventManager.Instance?.TriggerGaodeMapToPlateTransitionStarted(_activeProvinceName);
        return true;
    }

    private void PrepareGaodeMapView(string provinceName)
    {
        if (_provinceFocusController != null && !string.IsNullOrEmpty(provinceName))
        {
            _provinceFocusController.FocusProvince(provinceName);
        }

        _gaodeMapController.OnlineMaps.RedrawImmediately();
    }

    private void CompleteTransition()
    {
        if (_allPlateMapRoot != null)
        {
            _allPlateMapRoot.SetActive(false);
        }

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.KillProgressTween();
            _scanlineOverlay.SetProgressImmediate(0f);
            _scanlineOverlay.SetVisible(false);
        }

        _isTransitioning = false;
        EventManager.Instance?.TriggerPlateToGaodeMapTransitionCompleted(_activeProvinceName);
    }

    private void CompleteTransitionReverse()
    {
        _gaodeRawImageVisibility?.HideImmediate();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.KillProgressTween();
            _scanlineOverlay.SetProgressImmediate(0f);
            _scanlineOverlay.SetVisible(false);
        }

        _isTransitioning = false;
        EventManager.Instance?.TriggerGaodeMapToPlateTransitionCompleted(_activeProvinceName);
    }

    private void HideGaodeRawImageAtStart()
    {
        if (_gaodeRawImageVisibility != null)
        {
            _gaodeRawImageVisibility.HideImmediate();
        }
    }

    private string ResolveProvinceName(string provinceNameOverride)
    {
        if (!string.IsNullOrWhiteSpace(provinceNameOverride))
        {
            return provinceNameOverride.Trim();
        }

        return _defaultProvinceName;
    }

    private void FadeAllPlateModules(float alpha, float duration)
    {
        CachePlateModules();
        if (_plateModules == null)
        {
            return;
        }

        for (int i = 0; i < _plateModules.Length; i++)
        {
            _plateModules[i]?.TweenAlpha(alpha, duration, _plateFadeEase);
        }
    }

    private void CachePlateModules()
    {
        if (_allPlateMapRoot == null)
        {
            return;
        }

        _plateModules = _allPlateMapRoot.GetComponentsInChildren<PlateMapDisplayModule>(true);
    }

    private void ResolveReferences()
    {
        if (_allPlateMapRoot == null)
        {
            GameObject found = GameObject.Find("AllPlateMap");
            if (found != null)
            {
                _allPlateMapRoot = found;
            }
        }

        if (_gaodeMapController == null)
        {
            _gaodeMapController = GetComponent<GaodeMapController>();
        }

        if (_gaodeMapController == null)
        {
            _gaodeMapController = GaodeMapController.Instance;
        }

        if (_provinceFocusController == null)
        {
            _provinceFocusController = GetComponent<GaodeMapProvinceFocusController>();
        }

        if (_provinceFocusController == null)
        {
            _provinceFocusController = GaodeMapProvinceFocusController.Instance;
        }

        if (_gaodeRawImageVisibility == null)
        {
            _gaodeRawImageVisibility = FindFirstObjectByType<GaodeMapRawImageVisibility>();
        }

        if (_scanlineOverlay == null)
        {
            _scanlineOverlay = GetComponent<PlateToGaodeMapScanlineOverlay>();
        }

        CachePlateModules();
    }

    private void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
        }

        _sequence = null;
        _gaodeRawImageVisibility?.KillAlphaTween();
    }

#if UNITY_EDITOR
    [ContextMenu("测试：播放过渡")]
    private void EditorTestPlay()
    {
        PlayTransition(_defaultProvinceName);
    }

    [ContextMenu("测试：倒放过渡")]
    private void EditorTestPlayReverse()
    {
        PlayTransitionReverse(_defaultProvinceName);
    }
#endif
}
