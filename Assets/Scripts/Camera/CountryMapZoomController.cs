using UnityEngine;

/// <summary>
/// 仅国家级：以地图包围盒中心为锚点，移动 CameraPivot 拉近/拉远。
/// 国内：AllPlateMap；国外：当前激活大板块（Module/Renderer 包围盒中心）。
/// PC 滚轮、Android 双指捏合。缩放不会改写国家级初始机位；每次进入国家级都会还原该初始位。
/// 层级跳转 / 板图相机 DOTween 期间关闭缩放，避免与动画抢 Transform。
/// </summary>
[DisallowMultipleComponent]
public class CountryMapZoomController : MonoBehaviour
{
    [Header("引用（可留空，运行时查找）")]
    [SerializeField] private Transform _cameraRig;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private GameObject _allPlateMapRoot;
    [SerializeField] private CameraController _fogCameraZoomController;
    [SerializeField] private PlateMapDisplayController _plateMapDisplayController;

    [Header("缩放")]
    [SerializeField] private float _wheelZoomSpeed = 400f;
    [SerializeField] private float _pinchZoomSpeed = 1.5f;
    [SerializeField] private float _minDistanceToMapCenter = 200f;
    [SerializeField] private float _maxDistanceToMapCenter = 6000f;
    [SerializeField] private float _zoomSmoothTime = 0.08f;

    private bool _hasCountryHomePose;
    private Vector3 _countryHomeRigPosition;
    private Quaternion _countryHomeRigRotation;

    private float _targetDistance = -1f;
    private float _currentDistance;
    private float _distanceVelocity;
    private float _pinchPrevDistance = -1f;
    private bool _wasCountryLevel;
    private bool _wasZoomBlocked;
    /// <summary>板图聚焦/还原 DOTween 期间由 PlateMapDisplayController 置位，硬禁止写 Pivot。</summary>
    private bool _suppressedByPlateCamera;

    private static CountryMapZoomController _instance;

    /// <summary>场景中的国家级缩放控制器。</summary>
    public static CountryMapZoomController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CountryMapZoomController>(FindObjectsInactive.Include);
            }

            return _instance;
        }
    }

    /// <summary>板图相机 DOTween 开始前调用 true，结束/打断后调用 false。</summary>
    public void SetSuppressed(bool suppressed)
    {
        _suppressedByPlateCamera = suppressed;
        if (suppressed)
        {
            StopZoomMotion();
        }
        else if (IsCountryLevel())
        {
            SyncDistanceFromCurrentPose();
        }
    }

    /// <summary>是否已缓存国家级初始 CameraPivot 位姿。</summary>
    public bool HasCountryHomePose => _hasCountryHomePose;

    /// <summary>读取国家级初始 CameraPivot 位姿（供省→国家还原 Tween 终点使用）。</summary>
    public bool TryGetCountryHomePose(out Vector3 rigWorldPosition, out Quaternion rigWorldRotation)
    {
        rigWorldPosition = _countryHomeRigPosition;
        rigWorldRotation = _countryHomeRigRotation;
        return _hasCountryHomePose && _cameraRig != null;
    }

    /// <summary>
    /// 省→国家还原结束后调用：强制回到国家级 Home，并重置缩放距离状态，避免与还原动画冲突。
    /// </summary>
    public void ResetToCountryHomeAfterProvinceRestore()
    {
        ResolveReferences();
        if (_cameraRig == null || !_hasCountryHomePose)
        {
            return;
        }

        _suppressedByPlateCamera = false;
        ApplyCountryHomePose();
        _pinchPrevDistance = -1f;
        _distanceVelocity = 0f;
        SyncDistanceFromCurrentPose();
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnTransitionToPlateMapCompleted += HandleEnteredCountryView;
            em.OnPlateMapRestoreCameraCompleted += HandleProvinceRestoreCompleted;
            em.OnPlateMapDisplayFocus += HandlePlateCameraTweenStarted;
            em.OnPlateMapRestoreCameraStarted += HandlePlateCameraTweenStarted;
            em.OnTransitionToPlateMapStarted += HandleHierarchyTransitionStarted;
            em.OnTransitionToEarthStarted += HandleHierarchyTransitionStarted;
            em.OnPlateToVehicleViewTransitionStarted += HandleNamedTransitionStarted;
            em.OnVehicleToPlateViewTransitionStarted += HandleNamedTransitionStarted;
        }

        WorldMapRegionContext.OnRegionChanged += HandleWorldRegionChanged;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnTransitionToPlateMapCompleted -= HandleEnteredCountryView;
            em.OnPlateMapRestoreCameraCompleted -= HandleProvinceRestoreCompleted;
            em.OnPlateMapDisplayFocus -= HandlePlateCameraTweenStarted;
            em.OnPlateMapRestoreCameraStarted -= HandlePlateCameraTweenStarted;
            em.OnTransitionToPlateMapStarted -= HandleHierarchyTransitionStarted;
            em.OnTransitionToEarthStarted -= HandleHierarchyTransitionStarted;
            em.OnPlateToVehicleViewTransitionStarted -= HandleNamedTransitionStarted;
            em.OnVehicleToPlateViewTransitionStarted -= HandleNamedTransitionStarted;
        }

        WorldMapRegionContext.OnRegionChanged -= HandleWorldRegionChanged;
        RestoreFogZoomControl(true);
    }

    private void LateUpdate()
    {
        bool isCountry = IsCountryLevel();
        if (isCountry != _wasCountryLevel)
        {
            _wasCountryLevel = isCountry;
            // 国家级关掉 FogCamera 滚轮，避免改动子相机污染装框
            RestoreFogZoomControl(!isCountry);

            if (isCountry)
            {
                // 板图 DOTween 期间不抢 Home，交由动画结束后的回调处理
                if (!_suppressedByPlateCamera)
                {
                    EnsureCountryHomeApplied();
                    SyncDistanceFromCurrentPose();
                }
            }
            else
            {
                StopZoomMotion();
            }
        }

        bool zoomBlocked = IsZoomBlocked();
        if (zoomBlocked != _wasZoomBlocked)
        {
            _wasZoomBlocked = zoomBlocked;
            if (zoomBlocked)
            {
                StopZoomMotion();
            }
            else if (isCountry)
            {
                // 动画结束后从当前机位重新同步距离，避免 SmoothDamp 突然拽 Pivot
                SyncDistanceFromCurrentPose();
            }
        }

        if (!isCountry || zoomBlocked || _cameraRig == null || _cameraTransform == null)
        {
            return;
        }

        if (!TryGetMapCenter(out Vector3 mapCenter))
        {
            return;
        }

        bool hadInput = HandleZoomInput(mapCenter);
        // 无输入且已到位则不写 Pivot，避免 LateUpdate 与 DOTween 抢坐标
        if (hadInput || NeedsDistanceSmoothing())
        {
            ApplyZoomTowardMapCenter(mapCenter);
        }
    }

    /// <summary>地球→国家完成：固化或还原国家级初始机位。</summary>
    private void HandleEnteredCountryView()
    {
        if (_suppressedByPlateCamera)
        {
            return;
        }

        // 不依赖 IsCountryLevel：同帧可能尚未被 GameManager 写入 CountryLevel
        EnsureCountryHomeApplied();
        SyncDistanceFromCurrentPose();
    }

    /// <summary>省→国家还原完成：必须以缩放脚本 Home 为准（丢弃进省前缩放）。</summary>
    private void HandleProvinceRestoreCompleted()
    {
        ResetToCountryHomeAfterProvinceRestore();
    }

    private void HandlePlateCameraTweenStarted(string _)
    {
        StopZoomMotion();
    }

    private void HandlePlateCameraTweenStarted()
    {
        StopZoomMotion();
    }

    private void HandleHierarchyTransitionStarted()
    {
        StopZoomMotion();
    }

    private void HandleNamedTransitionStarted(string _)
    {
        StopZoomMotion();
    }

    /// <summary>国内外/国外大板块切换后：按新包围盒中心重同步缩放距离。</summary>
    private void HandleWorldRegionChanged()
    {
        if (!IsCountryLevel() || _suppressedByPlateCamera || IsZoomBlocked())
        {
            return;
        }

        if (_cameraTransform == null)
        {
            ResolveReferences();
        }

        SyncDistanceFromCurrentPose();
    }

    private void StopZoomMotion()
    {
        _targetDistance = -1f;
        _pinchPrevDistance = -1f;
        _distanceVelocity = 0f;
    }

    private bool NeedsDistanceSmoothing()
    {
        return _targetDistance >= 0f && Mathf.Abs(_currentDistance - _targetDistance) > 0.05f;
    }

    private bool IsZoomBlocked()
    {
        if (_suppressedByPlateCamera)
        {
            return true;
        }

        ControlStateHierarchyTransitionController hierarchy =
            ControlStateHierarchyTransitionController.Instance;
        if (hierarchy != null && hierarchy.IsBootstrapping)
        {
            return true;
        }

        if (ControlStateHierarchyTransitionController.IsAnyTransitionAnimationBusy())
        {
            return true;
        }

        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }

        if (_plateMapDisplayController != null && _plateMapDisplayController.IsCameraTweening)
        {
            return true;
        }

        return false;
    }

    private void EnsureCountryHomeApplied()
    {
        ResolveReferences();
        if (_cameraRig == null)
        {
            return;
        }

        if (!_hasCountryHomePose)
        {
            CacheCountryHomePose();
            return;
        }

        ApplyCountryHomePose();
    }

    private void CacheCountryHomePose()
    {
        _countryHomeRigPosition = _cameraRig.position;
        _countryHomeRigRotation = _cameraRig.rotation;
        _hasCountryHomePose = true;
        Debug.Log(
            $"[CountryMapZoom] 已缓存国家级初始 CameraPivot | pos={_countryHomeRigPosition} | euler={_countryHomeRigRotation.eulerAngles}");
    }

    private void ApplyCountryHomePose()
    {
        _cameraRig.SetPositionAndRotation(_countryHomeRigPosition, _countryHomeRigRotation);
        _distanceVelocity = 0f;
    }

    private void SyncDistanceFromCurrentPose()
    {
        if (!TryGetMapCenter(out Vector3 mapCenter))
        {
            return;
        }

        float dist = Vector3.Distance(_cameraTransform.position, mapCenter);
        _currentDistance = dist;
        _targetDistance = dist;
        _distanceVelocity = 0f;
    }

    private bool HandleZoomInput(Vector3 mapCenter)
    {
        float delta = 0f;

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > Mathf.Epsilon)
        {
            // 滚轮向上（正）→ 拉近（减小到地图中心距离）
            delta -= wheel * _wheelZoomSpeed;
        }

        delta += ReadPinchZoomDelta();

        if (Mathf.Abs(delta) < Mathf.Epsilon)
        {
            return false;
        }

        if (_targetDistance < 0f)
        {
            _targetDistance = Vector3.Distance(_cameraTransform.position, mapCenter);
            _currentDistance = _targetDistance;
        }

        _targetDistance = Mathf.Clamp(
            _targetDistance + delta,
            _minDistanceToMapCenter,
            _maxDistanceToMapCenter);
        return true;
    }

    /// <summary>双指捏合：两指距离变大→拉远，变小→拉近。</summary>
    private float ReadPinchZoomDelta()
    {
        if (Input.touchCount != 2)
        {
            _pinchPrevDistance = -1f;
            return 0f;
        }

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        float curr = Vector2.Distance(t0.position, t1.position);

        if (_pinchPrevDistance < 0f)
        {
            _pinchPrevDistance = curr;
            return 0f;
        }

        float pinchDelta = curr - _pinchPrevDistance;
        _pinchPrevDistance = curr;

        // 双指张开（距离变大）→ 地图常见手势：拉近（减小视距）
        return -pinchDelta * _pinchZoomSpeed;
    }

    private void ApplyZoomTowardMapCenter(Vector3 mapCenter)
    {
        if (_targetDistance < 0f)
        {
            return;
        }

        _currentDistance = Mathf.SmoothDamp(
            _currentDistance,
            _targetDistance,
            ref _distanceVelocity,
            _zoomSmoothTime);

        Vector3 camWorld = _cameraTransform.position;
        Vector3 fromCenter = camWorld - mapCenter;
        if (fromCenter.sqrMagnitude < 1e-6f)
        {
            fromCenter = -_cameraTransform.forward;
        }

        Vector3 newCamWorld = mapCenter + fromCenter.normalized * Mathf.Max(_currentDistance, 0.01f);
        Vector3 localOffsetWorld = _cameraRig.rotation * _cameraTransform.localPosition;
        _cameraRig.position = newCamWorld - localOffsetWorld;
    }

    private bool TryGetMapCenter(out Vector3 mapCenter)
    {
        mapCenter = Vector3.zero;
        ResolveReferences();

        // 国内/国外统一优先当前激活板块根的包围盒中心
        if (TryGetActivePlateCenter(out mapCenter))
        {
            return true;
        }

        if (_allPlateMapRoot == null)
        {
            return false;
        }

        if (PlateMapCameraFitUtility.TryGetRenderersWorldBounds(_allPlateMapRoot, out Bounds bounds))
        {
            mapCenter = bounds.center;
            return true;
        }

        mapCenter = _allPlateMapRoot.transform.position;
        return true;
    }

    /// <summary>
    /// 当前激活板块根的世界包围盒中心。
    /// 国内为中国地图根，国外为当前激活大板块根。
    /// </summary>
    private static bool TryGetActivePlateCenter(out Vector3 mapCenter)
    {
        mapCenter = Vector3.zero;
        WorldMapRegionController region = WorldMapRegionController.Instance;
        Transform plateRoot = region != null ? region.ActivePlateRoot : null;
        if (plateRoot == null)
        {
            return false;
        }

        PlateMapDisplayModule[] modules =
            plateRoot.GetComponentsInChildren<PlateMapDisplayModule>(true);
        if (PlateMapCameraFitUtility.TryGetModulesWorldBounds(modules, out Bounds moduleBounds) &&
            moduleBounds.size.sqrMagnitude > 1e-8f)
        {
            mapCenter = moduleBounds.center;
            return true;
        }

        if (TryGetActiveRenderersWorldBounds(plateRoot.gameObject, out Bounds rendererBounds) &&
            rendererBounds.size.sqrMagnitude > 1e-8f)
        {
            mapCenter = rendererBounds.center;
            return true;
        }

        mapCenter = plateRoot.position;
        return true;
    }

    /// <summary>仅合并激活中的 Renderer 世界包围盒。</summary>
    private static bool TryGetActiveRenderersWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (renderer.bounds.size.sqrMagnitude <= 1e-12f)
            {
                continue;
            }

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return has;
    }

    private static bool IsCountryLevel()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentState == GameManager.ControlState.CountryLevel;
    }

    private void RestoreFogZoomControl(bool enabled)
    {
        if (_fogCameraZoomController == null)
        {
            _fogCameraZoomController = FindFirstObjectByType<CameraController>();
        }

        if (_fogCameraZoomController != null)
        {
            _fogCameraZoomController.ZoomControlEnabled = enabled;
        }
    }

    private void ResolveReferences()
    {
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
            else if (_cameraRig.childCount > 0)
            {
                _cameraTransform = _cameraRig.GetChild(0);
            }
        }

        if (_allPlateMapRoot == null)
        {
            _allPlateMapRoot = GameObject.Find("AllPlateMap");
        }

        if (_fogCameraZoomController == null)
        {
            _fogCameraZoomController = _cameraRig != null
                ? _cameraRig.GetComponent<CameraController>()
                : FindFirstObjectByType<CameraController>();
        }

        if (_plateMapDisplayController == null)
        {
            _plateMapDisplayController = PlateMapDisplayController.Instance;
        }
    }
}
