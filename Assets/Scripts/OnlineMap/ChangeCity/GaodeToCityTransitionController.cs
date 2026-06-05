using DG.Tweening;
using UnityEngine;

/// <summary>
/// 第二阶段：GaodeMap 缩放至城市级 → 显示 City-Maker + 扫描线 → 隐藏 RawImage → 相机倾斜拉近。
/// </summary>
[DisallowMultipleComponent]
public class GaodeToCityTransitionController : MonoBehaviour
{
    [Header("地图引用")]
    [SerializeField] private GaodeMapController _gaodeMapController;
    [SerializeField] private GaodeMapRawImageVisibility _gaodeRawImageVisibility;
    [SerializeField] private PlateToGaodeMapScanlineOverlay _scanlineOverlay;

    [Header("城市场景")]
    [SerializeField] private GameObject _cityMakerRoot;
    [SerializeField] private Transform _cameraRig;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Camera _mainCamera;

    [Header("缩放阶段")]
    [SerializeField] private float _targetZoom = 17f;
    [SerializeField] private float _zoomDuration = 2.5f;
    [SerializeField] private Ease _zoomEase = Ease.InOutQuad;

    [Header("城市显现 + 第一阶段扫描特效")]
    [SerializeField] private float _scanlineDuration = 0.7f;
    [SerializeField] private float _rawImageHideDuration = 0.5f;
    [SerializeField] private Ease _scanlineEase = Ease.InOutCubic;
    [SerializeField] private Ease _rawImageHideEase = Ease.InOutQuad;

    [Header("主相机拉近终点（起点为 PlayTransition 开始时缓存的本地位姿）")]
    [SerializeField] private CityCameraPoseSettings _cityFocusPose = new CityCameraPoseSettings();
    [SerializeField] private float _cameraDollyDuration = 2f;
    [SerializeField] private Ease _cameraDollyEase = Ease.InOutQuad;

    private Sequence _sequence;
    private Tween _zoomTween;
    private bool _isTransitioning;
    private CityCameraPoseSettings _dollyStartPose;
    private float _zoomAtTransitionStart;
    private bool _hasDollyStartPose;
    private Vector3 _cameraRigInitialPosition;
    private Quaternion _cameraRigInitialRotation;
    private bool _hasCameraRigInitialPose;
    private Vector3 _cameraRigPreDollyPosition;
    private Quaternion _cameraRigPreDollyRotation;
    private bool _hasCameraRigPreDollyPose;

    public bool IsTransitioning => _isTransitioning;

    private static GaodeToCityTransitionController _instance;

    public static GaodeToCityTransitionController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GaodeToCityTransitionController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
        HideCityMakerAtStart();
    }

    private void Start()
    {
        ResolveReferences();
        CacheCameraRigInitialPose();
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>播放 GaodeMap → City-Maker 第二阶段过渡。</summary>
    public bool PlayTransition()
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_gaodeMapController == null || _gaodeMapController.OnlineMaps == null)
        {
            Debug.LogError("[GaodeToCityTransition] 未找到 GaodeMapController.OnlineMaps。");
            return false;
        }

        if (_gaodeRawImageVisibility == null)
        {
            Debug.LogError("[GaodeToCityTransition] 未找到 GaodeMapRawImageVisibility。");
            return false;
        }

        if (_cityMakerRoot == null)
        {
            Debug.LogError("[GaodeToCityTransition] 未找到 City-Maker。");
            return false;
        }

        _isTransitioning = true;
        KillSequence();
        _zoomAtTransitionStart = _gaodeMapController.OnlineMaps.floatZoom;
        CacheCameraRigPreDollyPose();
        ApplyCameraRigInitialPose();
        CacheDollyStartPose();

        EventManager.Instance?.TriggerGaodeMapToCityTransitionStarted();

        _sequence = DOTween.Sequence();

        _zoomTween = GaodeMapZoomTween.TweenFloatZoom(
            _gaodeMapController.OnlineMaps,
            _targetZoom,
            _zoomDuration,
            _zoomEase);
        if (_zoomTween != null)
        {
            _sequence.Append(_zoomTween);
        }
        else
        {
            _sequence.AppendInterval(_zoomDuration);
        }

        _sequence.AppendCallback(BeginRevealAndOverlayPhase);

        Tween scanTween = BuildScanlineTween();
        Tween rawHideTween = _gaodeRawImageVisibility.HideFade(_rawImageHideDuration, _rawImageHideEase);
        float overlayPhaseDuration = Mathf.Max(_scanlineDuration, _rawImageHideDuration);

        if (scanTween != null)
        {
            _sequence.Append(scanTween);
            if (rawHideTween != null)
            {
                _sequence.Join(rawHideTween);
            }
        }
        else if (rawHideTween != null)
        {
            _sequence.Append(rawHideTween);
        }
        else
        {
            _sequence.AppendInterval(overlayPhaseDuration);
        }

        _sequence.AppendCallback(CompleteOverlayPhase);
        _sequence.Append(TweenCameraToFocusPose());
        _sequence.OnComplete(CompleteTransition);
        return true;
    }

    /// <summary>倒放：相机拉回 → 扫描线收回 + RawImage 渐显 → 隐藏 City-Maker → zoom 还原。</summary>
    public bool PlayTransitionReverse()
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_gaodeMapController == null || _gaodeMapController.OnlineMaps == null)
        {
            Debug.LogError("[GaodeToCityTransition] 未找到 GaodeMapController.OnlineMaps。");
            return false;
        }

        if (_gaodeRawImageVisibility == null)
        {
            Debug.LogError("[GaodeToCityTransition] 未找到 GaodeMapRawImageVisibility。");
            return false;
        }

        _isTransitioning = true;
        KillSequence();

        EventManager.Instance?.TriggerCityToGaodeMapTransitionReverseStarted();

        _sequence = DOTween.Sequence();
        _sequence.Append(TweenCameraToDollyStartPose());
        _sequence.Append(BuildReverseOverlaySequence());
        _sequence.AppendCallback(RestoreCameraRigPreDollyPose);
        _sequence.AppendCallback(CompleteReverseOverlayPhase);

        float zoomTo = _zoomAtTransitionStart > 0f ? _zoomAtTransitionStart : _gaodeMapController.GetCurrentZoom();
        _zoomTween = GaodeMapZoomTween.TweenFloatZoom(
            _gaodeMapController.OnlineMaps,
            zoomTo,
            _zoomDuration,
            _zoomEase);
        if (_zoomTween != null)
        {
            _sequence.Append(_zoomTween);
        }
        else
        {
            _gaodeMapController.OnlineMaps.floatZoom = zoomTo;
            _sequence.AppendInterval(_zoomDuration);
        }

        _sequence.OnComplete(CompleteTransitionReverse);
        return true;
    }

    private void BeginRevealAndOverlayPhase()
    {
        ShowAndFrameCityMaker();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.SetVisible(true);
            _scanlineOverlay.SetProgressImmediate(0f);
        }
    }

    private Tween BuildScanlineTween()
    {
        if (_scanlineOverlay == null)
        {
            return null;
        }

        return _scanlineOverlay.TweenProgress(1f, _scanlineDuration, _scanlineEase);
    }

    private Tween BuildReverseScanlineTween()
    {
        if (_scanlineOverlay == null)
        {
            return null;
        }

        return _scanlineOverlay.TweenProgressFromTo(1f, 0f, _scanlineDuration, _scanlineEase);
    }

    private void BeginReverseOverlayPhase()
    {
        if (_cityMakerRoot != null && !_cityMakerRoot.activeSelf)
        {
            _cityMakerRoot.SetActive(true);
        }
    }

    /// <summary>倒播叠层子序列：扫描线 1→0 与 RawImage 渐显并行。</summary>
    private Sequence BuildReverseOverlaySequence()
    {
        BeginReverseOverlayPhase();

        Sequence overlaySeq = DOTween.Sequence();
        Tween scanTween = BuildReverseScanlineTween();
        Tween rawShowTween = _gaodeRawImageVisibility.ShowFade(_rawImageHideDuration, _rawImageHideEase);

        if (scanTween != null)
        {
            overlaySeq.Append(scanTween);
            if (rawShowTween != null)
            {
                overlaySeq.Join(rawShowTween);
            }
        }
        else if (rawShowTween != null)
        {
            overlaySeq.Append(rawShowTween);
        }
        else
        {
            overlaySeq.AppendInterval(Mathf.Max(_scanlineDuration, _rawImageHideDuration));
        }

        return overlaySeq;
    }

    private void CompleteReverseOverlayPhase()
    {
        HideCityMakerAtStart();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.KillProgressTween();
            _scanlineOverlay.SetProgressImmediate(0f);
            _scanlineOverlay.SetVisible(false);
        }
    }

    private void CompleteOverlayPhase()
    {
        _gaodeRawImageVisibility?.HideImmediate();

        if (_scanlineOverlay != null)
        {
            _scanlineOverlay.KillProgressTween();
            _scanlineOverlay.SetProgressImmediate(0f);
            _scanlineOverlay.SetVisible(false);
        }
    }

    private void ShowAndFrameCityMaker()
    {
        _cityMakerRoot.SetActive(true);
    }

    /// <summary>从当前位姿插值到面板配置的拉近终点。</summary>
    private Tween TweenCameraToFocusPose()
    {
        return TweenCameraToPose(_cityFocusPose);
    }

    /// <summary>倒播：从当前位姿回到正向拉进前的起点。</summary>
    private Tween TweenCameraToDollyStartPose()
    {
        if (!_hasDollyStartPose || _dollyStartPose == null)
        {
            return DOTween.Sequence().AppendInterval(_cameraDollyDuration);
        }

        return TweenCameraToPose(_dollyStartPose);
    }

    private Tween TweenCameraToPose(CityCameraPoseSettings target)
    {
        if (_cameraTransform == null || target == null)
        {
            return DOTween.Sequence().AppendInterval(_cameraDollyDuration);
        }

        Quaternion targetCamLocalRot = Quaternion.Euler(target.cameraLocalEuler);
        Sequence camSeq = DOTween.Sequence();
        camSeq.Join(_cameraTransform.DOLocalMove(target.cameraLocalPosition, _cameraDollyDuration).SetEase(_cameraDollyEase));
        camSeq.Join(_cameraTransform.DOLocalRotateQuaternion(targetCamLocalRot, _cameraDollyDuration).SetEase(_cameraDollyEase));
        return camSeq;
    }

    private void CompleteTransition()
    {
        _isTransitioning = false;
        EventManager.Instance?.TriggerGaodeMapToCityTransitionCompleted();
    }

    private void CompleteTransitionReverse()
    {
        _isTransitioning = false;
        EventManager.Instance?.TriggerCityToGaodeMapTransitionReverseCompleted();
    }

    private void HideCityMakerAtStart()
    {
        if (_cityMakerRoot != null)
        {
            _cityMakerRoot.SetActive(false);
        }
    }

    /// <summary>记录拉进动画起点（重置父物体后主相机本地位姿，倒播时复用）。</summary>
    private void CacheDollyStartPose()
    {
        if (_cameraTransform == null)
        {
            return;
        }

        _dollyStartPose ??= new CityCameraPoseSettings();
        _dollyStartPose.CaptureFrom(_cameraTransform);
        _hasDollyStartPose = true;
    }

    /// <summary>Start 时记录相机父物体（CameraPivot）初始世界位姿。</summary>
    private void CacheCameraRigInitialPose()
    {
        if (_cameraRig == null)
        {
            return;
        }

        _cameraRigInitialPosition = _cameraRig.position;
        _cameraRigInitialRotation = _cameraRig.rotation;
        _hasCameraRigInitialPose = true;
    }

    /// <summary>PlayTransition 开始时记录拉进前相机父物体世界位姿。</summary>
    private void CacheCameraRigPreDollyPose()
    {
        if (_cameraRig == null)
        {
            return;
        }

        _cameraRigPreDollyPosition = _cameraRig.position;
        _cameraRigPreDollyRotation = _cameraRig.rotation;
        _hasCameraRigPreDollyPose = true;
    }

    /// <summary>将相机父物体重置为 Start 时缓存的初始世界位姿。</summary>
    private void ApplyCameraRigInitialPose()
    {
        if (_cameraRig == null || !_hasCameraRigInitialPose)
        {
            return;
        }

        _cameraRig.SetPositionAndRotation(_cameraRigInitialPosition, _cameraRigInitialRotation);
    }

    /// <summary>倒播 RawImage 渐显完成后，将相机父物体还原为拉进前缓存的世界位姿。</summary>
    private void RestoreCameraRigPreDollyPose()
    {
        if (_cameraRig == null || !_hasCameraRigPreDollyPose)
        {
            return;
        }

        _cameraRig.SetPositionAndRotation(_cameraRigPreDollyPosition, _cameraRigPreDollyRotation);
    }

    private void ResolveReferences()
    {
        if (_gaodeMapController == null)
        {
            _gaodeMapController = GaodeMapController.Instance;
        }

        if (_gaodeRawImageVisibility == null)
        {
            _gaodeRawImageVisibility = FindFirstObjectByType<GaodeMapRawImageVisibility>();
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

        if (_cameraRig == null)
        {
            GameObject pivot = GameObject.Find("CameraPivot");
            if (pivot != null)
            {
                _cameraRig = pivot.transform;
            }
        }

        if (_cameraTransform == null && _cameraRig != null)
        {
            Transform fog = _cameraRig.Find("FogCamera");
            if (fog != null)
            {
                _cameraTransform = fog;
            }
        }

        if (_mainCamera == null && _cameraTransform != null)
        {
            _mainCamera = _cameraTransform.GetComponent<Camera>();
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
        }

        _sequence = null;

        if (_zoomTween != null && _zoomTween.IsActive())
        {
            _zoomTween.Kill();
        }

        _zoomTween = null;
        _gaodeRawImageVisibility?.KillAlphaTween();
        _scanlineOverlay?.KillProgressTween();
        _cameraTransform?.DOKill();
    }

#if UNITY_EDITOR
    [ContextMenu("将当前主相机位姿写入拉近终点")]
    private void EditorCaptureCityFocusPose()
    {
        ResolveReferences();
        _cityFocusPose ??= new CityCameraPoseSettings();
        _cityFocusPose.CaptureFrom(_cameraTransform);
    }

    [ContextMenu("测试：Gaode → City 过渡")]
    private void EditorTestPlay()
    {
        PlayTransition();
    }

    [ContextMenu("测试：City → Gaode 倒放")]
    private void EditorTestPlayReverse()
    {
        PlayTransitionReverse();
    }
#endif
}
