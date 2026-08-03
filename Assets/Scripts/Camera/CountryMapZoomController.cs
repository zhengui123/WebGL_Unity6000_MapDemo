using UnityEngine;

/// <summary>
/// 仅国家级：移动 CameraPivot 做缩放与世界 XZ 拖拽平移。
/// 缩放锚点为当前屏幕中心落在世界 XZ 上的点（打不中则回退地图包围盒中心）；平移范围仍相对地图中心钳制。
/// PC：滚轮缩放、左键拖拽平移；Android：双指捏合缩放、单指拖拽平移。
/// 缩放/平移不会改写国家级初始机位；每次进入国家级 / 回 Home 都会还原该初始位。
/// 层级跳转 / 板图相机 DOTween 期间关闭操控，避免与动画抢 Transform。
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

    [Header("平移（世界 XZ）")]
    [Tooltip("超过该像素位移才开始平移，避免与省份点击冲突")]
    [SerializeField] private float _panDragThresholdPixels = 8f;
    [Tooltip("相机相对地图中心在 XZ 平面上的最大偏移")]
    [SerializeField] private float _maxPanOffsetFromMapCenter = 3000f;

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

    private bool _pointerHeld;
    private bool _isPanning;
    private Vector2 _pointerDownScreen;
    private Vector3 _lastPanHitWorld;
    private Camera _panRayCamera;

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
            StopZoomAndPanMotion();
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
        StopZoomAndPanMotion();
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
                StopZoomAndPanMotion();
            }
        }

        bool zoomBlocked = IsZoomBlocked();
        if (zoomBlocked != _wasZoomBlocked)
        {
            _wasZoomBlocked = zoomBlocked;
            if (zoomBlocked)
            {
                StopZoomAndPanMotion();
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

        HandlePanInput(mapCenter);

        if (!TryGetZoomAnchor(out Vector3 zoomAnchor))
        {
            return;
        }

        bool hadZoomInput = HandleZoomInput(zoomAnchor);
        // 无输入且已到位则不写 Pivot，避免 LateUpdate 与 DOTween 抢坐标
        if (hadZoomInput || NeedsDistanceSmoothing())
        {
            ApplyZoomTowardAnchor(zoomAnchor);
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
        StopZoomAndPanMotion();
    }

    private void HandlePlateCameraTweenStarted()
    {
        StopZoomAndPanMotion();
    }

    private void HandleHierarchyTransitionStarted()
    {
        StopZoomAndPanMotion();
    }

    private void HandleNamedTransitionStarted(string _)
    {
        StopZoomAndPanMotion();
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

    private void StopZoomAndPanMotion()
    {
        _targetDistance = -1f;
        _pinchPrevDistance = -1f;
        _distanceVelocity = 0f;
        ClearPanState();
    }

    private void ClearPanState()
    {
        _pointerHeld = false;
        _isPanning = false;
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
        ClearPanState();
    }

    /// <summary>
    /// 左键 / 单指拖拽：在世界 XZ 平面上平移 CameraPivot；相对地图中心钳制最大偏移。
    /// </summary>
    private void HandlePanInput(Vector3 mapCenter)
    {
        // 双指交给捏合缩放
        if (Input.touchCount >= 2)
        {
            ClearPanState();
            return;
        }

        if (!TryReadPanPointer(out Vector2 screen, out bool down, out bool held, out bool up))
        {
            ClearPanState();
            return;
        }

        if (down)
        {
            _pointerHeld = true;
            _isPanning = false;
            _pointerDownScreen = screen;
        }

        if (up || !held)
        {
            ClearPanState();
            return;
        }

        if (!_pointerHeld)
        {
            return;
        }

        float planeY = mapCenter.y;

        if (!_isPanning)
        {
            if (Vector2.Distance(screen, _pointerDownScreen) < _panDragThresholdPixels)
            {
                return;
            }

            if (!TryScreenPointToWorldXZ(screen, planeY, out Vector3 grabHit))
            {
                return;
            }

            _isPanning = true;
            _lastPanHitWorld = grabHit;
            return;
        }

        if (!TryScreenPointToWorldXZ(screen, planeY, out Vector3 currHit))
        {
            return;
        }

        Vector3 delta = _lastPanHitWorld - currHit;
        delta.y = 0f;
        if (delta.sqrMagnitude < 1e-12f)
        {
            return;
        }

        Vector3 proposedRig = _cameraRig.position + delta;
        _cameraRig.position = ClampRigXZOffsetFromMapCenter(proposedRig, mapCenter);

        // 以钳制后机位重新采样抓取点，避免越界时手指与地图错位
        if (TryScreenPointToWorldXZ(screen, planeY, out Vector3 hitAfter))
        {
            _lastPanHitWorld = hitAfter;
        }

        SyncDistanceFromCurrentPose();
    }

    /// <summary>PC 左键；移动端单指。同时存在触屏时优先触屏。</summary>
    private static bool TryReadPanPointer(out Vector2 screen, out bool down, out bool held, out bool up)
    {
        screen = Vector2.zero;
        down = false;
        held = false;
        up = false;

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            screen = touch.position;
            down = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        if (Input.touchCount > 1)
        {
            return false;
        }

        screen = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        up = Input.GetMouseButtonUp(0);
        return down || held || up;
    }

    private bool TryScreenPointToWorldXZ(Vector2 screen, float planeY, out Vector3 worldOnPlane)
    {
        worldOnPlane = Vector3.zero;
        Camera cam = ResolvePanRayCamera();
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldOnPlane = ray.GetPoint(enter);
        worldOnPlane.y = planeY;
        return true;
    }

    /// <summary>按相机世界坐标相对地图中心的 XZ 距离钳制，再反推 Pivot。</summary>
    private Vector3 ClampRigXZOffsetFromMapCenter(Vector3 proposedRigPosition, Vector3 mapCenter)
    {
        if (_maxPanOffsetFromMapCenter <= 0f || _cameraTransform == null)
        {
            return proposedRigPosition;
        }

        Vector3 localOffsetWorld = _cameraRig.rotation * _cameraTransform.localPosition;
        Vector3 camWorld = proposedRigPosition + localOffsetWorld;
        Vector3 flat = new Vector3(camWorld.x - mapCenter.x, 0f, camWorld.z - mapCenter.z);
        float max = _maxPanOffsetFromMapCenter;
        if (flat.sqrMagnitude <= max * max)
        {
            return proposedRigPosition;
        }

        flat = flat.normalized * max;
        camWorld.x = mapCenter.x + flat.x;
        camWorld.z = mapCenter.z + flat.z;
        return camWorld - localOffsetWorld;
    }

    private Camera ResolvePanRayCamera()
    {
        if (_panRayCamera != null)
        {
            return _panRayCamera;
        }

        if (_cameraTransform != null)
        {
            _panRayCamera = _cameraTransform.GetComponent<Camera>();
            if (_panRayCamera == null)
            {
                _panRayCamera = _cameraTransform.GetComponentInChildren<Camera>(true);
            }
        }

        if (_panRayCamera == null)
        {
            _panRayCamera = Camera.main;
        }

        return _panRayCamera;
    }

    private void SyncDistanceFromCurrentPose()
    {
        if (!TryGetZoomAnchor(out Vector3 zoomAnchor))
        {
            return;
        }

        float dist = Vector3.Distance(_cameraTransform.position, zoomAnchor);
        _currentDistance = dist;
        _targetDistance = dist;
        _distanceVelocity = 0f;
    }

    private bool HandleZoomInput(Vector3 zoomAnchor)
    {
        float delta = 0f;

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > Mathf.Epsilon)
        {
            // 滚轮向上（正）→ 拉近（减小到缩放锚点距离）
            delta -= wheel * _wheelZoomSpeed;
        }

        delta += ReadPinchZoomDelta();

        if (Mathf.Abs(delta) < Mathf.Epsilon)
        {
            return false;
        }

        if (_targetDistance < 0f)
        {
            _targetDistance = Vector3.Distance(_cameraTransform.position, zoomAnchor);
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

    /// <summary>沿「相机 → 缩放锚点」方向调整视距（锚点一般为屏幕中心落点）。</summary>
    private void ApplyZoomTowardAnchor(Vector3 zoomAnchor)
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
        Vector3 fromAnchor = camWorld - zoomAnchor;
        if (fromAnchor.sqrMagnitude < 1e-6f)
        {
            fromAnchor = -_cameraTransform.forward;
        }

        Vector3 newCamWorld = zoomAnchor + fromAnchor.normalized * Mathf.Max(_currentDistance, 0.01f);
        Vector3 localOffsetWorld = _cameraRig.rotation * _cameraTransform.localPosition;
        _cameraRig.position = newCamWorld - localOffsetWorld;
    }

    /// <summary>
    /// 缩放锚点：当前屏幕中心射线与世界 XZ（高度取地图中心 Y）的交点；打不中则回退地图包围盒中心。
    /// </summary>
    private bool TryGetZoomAnchor(out Vector3 zoomAnchor)
    {
        zoomAnchor = Vector3.zero;
        if (!TryGetMapCenter(out Vector3 mapCenter))
        {
            return false;
        }

        Camera cam = ResolvePanRayCamera();
        if (cam != null)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (TryScreenPointToWorldXZ(screenCenter, mapCenter.y, out zoomAnchor))
            {
                return true;
            }
        }

        zoomAnchor = mapCenter;
        return true;
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
