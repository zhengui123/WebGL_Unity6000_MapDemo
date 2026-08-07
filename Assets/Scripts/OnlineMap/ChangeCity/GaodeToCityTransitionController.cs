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
    [SerializeField] private Transform _cityCameraTransform;
    [SerializeField] private Transform _vehicleParentTransform;
    [SerializeField] private Camera _mainCamera;

    [Header("缩放阶段")]
    [SerializeField] private float _targetZoom = 17f;
    [Tooltip("国外国家：GaodeMap → 城市 的目标 floatZoom")]
    [SerializeField] private float _foreignTargetZoom = 10f;
    [SerializeField] private float _zoomDuration = 2.5f;
    [SerializeField] private Ease _zoomEase = Ease.InOutQuad;

    [Header("城市显现 + 第一阶段扫描特效")]
    [SerializeField] private float _scanlineDuration = 0.7f;
    [SerializeField] private float _rawImageHideDuration = 0.5f;
    [SerializeField] private Ease _scanlineEase = Ease.InOutCubic;
    [SerializeField] private Ease _rawImageHideEase = Ease.InOutQuad;

    [Header("拉近终点（起点为 PlayTransition 开始时缓存位姿）")]
    [SerializeField] private CityCameraPoseSettings[] _cityFocusPoses;
    [SerializeField] private int _focusPoseIndex;
    [SerializeField] private float _cameraDollyDuration = 2f;
    [SerializeField] private Ease _cameraDollyEase = Ease.InOutQuad;

    private Sequence _sequence;
    private Tween _zoomTween;
    private bool _isTransitioning;
    private float _zoomAtTransitionStart;
    private bool _hasDollyStartPose;
    private Vector3 _dollyStartCameraLocalPosition;
    private Quaternion _dollyStartCameraLocalRotation;
    private Vector3 _dollyStartCityCameraLocalPosition;
    private Quaternion _dollyStartCityCameraLocalRotation;
    private Vector3 _dollyStartVehicleWorldPosition;
    private Quaternion _dollyStartVehicleWorldRotation;
    private CityCameraPoseSettings _activeFocusPose;
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

        _activeFocusPose = GetCurrentFocusPose();
        if (_activeFocusPose == null || !_activeFocusPose.IsValid())
        {
            Debug.LogError("[GaodeToCityTransition] 拉近终点数组为空或当前标号缺少 Marker Transform。");
            return false;
        }

        _isTransitioning = true;
        KillSequence();
        _zoomAtTransitionStart = _gaodeMapController.OnlineMaps.floatZoom;
        CacheCameraRigPreDollyPose();
        ApplyCameraRigInitialPose();
        CacheDollyStartPose();
        AdvanceFocusPoseIndex();

        EventManager.Instance?.TriggerGaodeMapToCityTransitionStarted();

        _sequence = DOTween.Sequence();

        _zoomTween = GaodeMapZoomTween.TweenFloatZoom(
            _gaodeMapController.OnlineMaps,
            ResolveTargetZoom(),
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
        _sequence.Append(TweenToFocusPose(_activeFocusPose));
        _sequence.OnComplete(CompleteTransition);
        ForceCompleteSequenceIfInstant();
        return true;
    }

    /// <summary>倒放：相机与车辆拉回 → 扫描线收回 + RawImage 渐显 → 隐藏 City-Maker → zoom 还原。</summary>
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
        _sequence.Append(TweenToDollyStartPose());
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
        ForceCompleteSequenceIfInstant();
        return true;
    }

    /// <summary>若高德/城市过渡正在播放，则立即完成并执行完成回调。</summary>
    public void CompleteCurrentTransitionImmediate()
    {
        if (_sequence == null || !_sequence.IsActive())
        {
            return;
        }

        _sequence.Complete(withCallbacks: true);
    }

    /// <summary>瞬时过渡时强制完成 Sequence，确保 OnComplete 与后续阶段能衔接。</summary>
    private void ForceCompleteSequenceIfInstant()
    {
        if (_sequence == null || !_sequence.IsActive())
        {
            return;
        }

        if (_zoomDuration <= 0f
            && _scanlineDuration <= 0f
            && _rawImageHideDuration <= 0f
            && _cameraDollyDuration <= 0f)
        {
            _sequence.Complete(withCallbacks: true);
        }
    }

    private CityCameraPoseSettings GetCurrentFocusPose()
    {
        if (_cityFocusPoses == null || _cityFocusPoses.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(_focusPoseIndex, 0, _cityFocusPoses.Length - 1);
        return _cityFocusPoses[index];
    }

    /// <summary>国内用 _targetZoom；国外用 _foreignTargetZoom。</summary>
    private float ResolveTargetZoom()
    {
        if (WorldMapRegionContext.IsInitialized &&
            WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign)
        {
            return _foreignTargetZoom;
        }

        return _targetZoom;
    }

    private void AdvanceFocusPoseIndex()
    {
        if (_cityFocusPoses == null || _cityFocusPoses.Length == 0)
        {
            return;
        }

        _focusPoseIndex = (_focusPoseIndex + 1) % _cityFocusPoses.Length;
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

    private Tween TweenToFocusPose(CityCameraPoseSettings target)
    {
        if (target == null || !target.IsValid())
        {
            return DOTween.Sequence().AppendInterval(_cameraDollyDuration);
        }

        ApplyFocusPoseInstant(target);
        ResolveCameraLocalPoseFromMarker(target.targetCameraTransform, out Vector3 camLocalPos, out Quaternion camLocalRot);
        return BuildCameraDollyTween(camLocalPos, camLocalRot);
    }

    private Tween TweenToDollyStartPose()
    {
        if (!_hasDollyStartPose)
        {
            return DOTween.Sequence().AppendInterval(_cameraDollyDuration);
        }

        //ApplyDollyStartInstant();
        return BuildCameraDollyTween(_dollyStartCameraLocalPosition, _dollyStartCameraLocalRotation);
    }

    /// <summary>拉近终点变更：车辆与 CityCamera 立即到位，不做插值。</summary>
    private void ApplyFocusPoseInstant(CityCameraPoseSettings target)
    {
        if (target == null || !target.IsValid())
        {
            return;
        }

        if (_vehicleParentTransform != null)
        {
            _vehicleParentTransform.SetPositionAndRotation(
                target.targetVehicleTransform.position,
                target.targetVehicleTransform.rotation);
        }

        ApplyCityCameraFromMarker(target.targetCameraTransform);
    }

    /// <summary>倒播起点：车辆与 CityCamera 立即还原为正向拉进前缓存位姿。</summary>
    private void ApplyDollyStartInstant()
    {
        if (!_hasDollyStartPose)
        {
            return;
        }

        if (_vehicleParentTransform != null)
        {
            _vehicleParentTransform.SetPositionAndRotation(
                _dollyStartVehicleWorldPosition,
                _dollyStartVehicleWorldRotation);
        }

        if (_cityCameraTransform != null)
        {
            _cityCameraTransform.localPosition = _dollyStartCityCameraLocalPosition;
            _cityCameraTransform.localRotation = _dollyStartCityCameraLocalRotation;
        }
    }

    private void ApplyCityCameraFromMarker(Transform cameraMarker)
    {
        if (_cityCameraTransform == null || cameraMarker == null)
        {
            return;
        }

        ResolveCameraLocalPoseFromMarker(cameraMarker, out Vector3 localPos, out Quaternion localRot);
        _cityCameraTransform.localPosition = localPos;
        _cityCameraTransform.localRotation = localRot;
    }

    private Tween BuildCameraDollyTween(Vector3 cameraLocalPosition, Quaternion cameraLocalRotation)
    {
        if (_cameraTransform == null)
        {
            return DOTween.Sequence().AppendInterval(_cameraDollyDuration);
        }

        Sequence dollySeq = DOTween.Sequence();
        dollySeq.Join(_cameraTransform.DOLocalMove(cameraLocalPosition, _cameraDollyDuration).SetEase(_cameraDollyEase));
        dollySeq.Join(_cameraTransform.DOLocalRotateQuaternion(cameraLocalRotation, _cameraDollyDuration).SetEase(_cameraDollyEase));
        return dollySeq;
    }

    private void ResolveCameraLocalPoseFromMarker(Transform cameraMarker, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (_cameraRig == null || cameraMarker == null)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return;
        }

        localPosition = _cameraRig.InverseTransformPoint(cameraMarker.position);
        localRotation = Quaternion.Inverse(_cameraRig.rotation) * cameraMarker.rotation;
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

    private void CacheDollyStartPose()
    {
        _hasDollyStartPose = false;

        if (_cameraTransform != null)
        {
            _dollyStartCameraLocalPosition = _cameraTransform.localPosition;
            _dollyStartCameraLocalRotation = _cameraTransform.localRotation;
            _hasDollyStartPose = true;
        }

        if (_cityCameraTransform != null)
        {
            _dollyStartCityCameraLocalPosition = _cityCameraTransform.localPosition;
            _dollyStartCityCameraLocalRotation = _cityCameraTransform.localRotation;
            _hasDollyStartPose = true;
        }

        if (_vehicleParentTransform != null)
        {
            _dollyStartVehicleWorldPosition = _vehicleParentTransform.position;
            _dollyStartVehicleWorldRotation = _vehicleParentTransform.rotation;
            _hasDollyStartPose = true;
        }
    }

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

    private void ApplyCameraRigInitialPose()
    {
        if (_cameraRig == null || !_hasCameraRigInitialPose)
        {
            return;
        }

        _cameraRig.SetPositionAndRotation(_cameraRigInitialPosition, _cameraRigInitialRotation);
    }

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

        if (_cityCameraTransform == null && _cameraRig != null)
        {
            Transform cityCamera = _cameraRig.Find("CityCamera");
            if (cityCamera != null)
            {
                _cityCameraTransform = cityCamera;
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

        if (_vehicleParentTransform == null)
        {
            Transform model = GameObject.Find("Model")?.transform;
            Transform cityMaker = model != null ? model.Find("City-Maker") : null;
            Transform carModel = cityMaker != null ? cityMaker.Find("CarModel") : null;
            if (carModel != null)
            {
                _vehicleParentTransform = carModel;
            }
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
    [ContextMenu("将当前位姿同步到当前标号对应 Marker")]
    private void EditorSyncFocusPoseMarkers()
    {
        ResolveReferences();
        CityCameraPoseSettings pose = GetCurrentFocusPose();
        if (pose == null)
        {
            Debug.LogWarning("[GaodeToCityTransition] 拉近终点数组为空。");
            return;
        }

        pose.SyncMarkersFrom(_cameraTransform, _vehicleParentTransform);
        Debug.Log($"[GaodeToCityTransition] 已同步标号 {_focusPoseIndex} 的 Marker。");
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
