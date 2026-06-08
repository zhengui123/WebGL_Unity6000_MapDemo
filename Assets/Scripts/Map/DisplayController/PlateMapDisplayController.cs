using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图显示控制器：启用后点击模块则移动<strong>摄像机</strong>居中并拉近；可 DOTween 还原至聚焦前位姿。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapDisplayController : MonoBehaviour
{
    private struct CameraPoseSnapshot
    {
        public Vector3 RigWorldPosition;
        public Quaternion RigWorldRotation;
        public Vector3 CameraLocalPosition;
        public Quaternion CameraLocalRotation;
        public float ZoomLocalY;
    }

    [Header("板块根（仅用于收集模块，不移动）")]
    [SerializeField] private Transform _plateMapRoot;

    [Header("摄像机")]
    [Tooltip("相机架（CameraController 所在物体，做世界平移）")]
    [SerializeField] private Transform _cameraRig;
    [Tooltip("实际渲染相机（做局部 Y 拉近，留空则用 Pick Camera")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Camera _pickCamera;
    [SerializeField] private CameraController _cameraZoomController;

    [Header("拾取")]
    [SerializeField] private float _raycastMaxDistance = 5000f;
    [SerializeField] private float _clickMaxDragPixels = 8f;
    [SerializeField] private LayerMask _pickLayerMask = Physics.DefaultRaycastLayers;

    [Header("聚焦动画")]
    [SerializeField] private float _defaultFocusCameraLocalY = 650f;
    [SerializeField] private float _focusDuration = 0.45f;
    [SerializeField] private Ease _focusEase = Ease.InOutQuad;

    [Header("还原动画")]
    [SerializeField] private float _restoreDuration = 0.5f;
    [SerializeField] private Ease _restoreEase = Ease.InOutQuad;
    [SerializeField] private KeyCode _restoreKey = KeyCode.Escape;

    [Header("其他板块透明度")]
    [Tooltip("聚焦时其他板块 1→0、还原时 0→1，共用此时长（秒）")]
    [SerializeField] private float _otherModuleFadeDuration = 1f;
    [SerializeField] private Ease _otherModuleFadeEase = Ease.InOutQuad;

    [Header("可点击模块（留空则收集子级 PlateMapDisplayModule）")]
    [SerializeField] private PlateMapDisplayModule[] _modules;


    private Vector3 _mouseDownPosition;
    private Sequence _cameraTweenSequence;
    private PlateMapDisplayModule _focusedModule;
    private PlateMapDisplayModule _transitionCachedModule;
    private CameraPoseSnapshot _preFocusPose;
    private bool _hasPreFocusPose;

    /// <summary>当前聚焦的模块；无则为 null。</summary>
    public PlateMapDisplayModule FocusedModule => _focusedModule;

    /// <summary>过渡隐藏前缓存的聚焦模块；未聚焦则为 null。</summary>
    public PlateMapDisplayModule TransitionCachedModule => _transitionCachedModule;

    /// <summary>是否已缓存首次聚焦前相机位姿（可调用还原）。</summary>
    public bool CanRestoreCamera => _hasPreFocusPose;

    private static PlateMapDisplayController _instance;

    /// <summary>场景中的显示控制器（供 MapApi 等调用）。</summary>
    public static PlateMapDisplayController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PlateMapDisplayController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;

        if (_plateMapRoot == null)
        {
            _plateMapRoot = transform;
        }

        ResolveCameraReferences();
        RefreshModuleList();
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
        RefreshModuleList();
    }

    private void OnDisable()
    {
        KillCameraTweens();
        KillAllModuleAlphaTweens();
    }

    private void Update()
    {
        if (!enabled)
        {
            return;
        }

        HandleModuleClick();
    }

    private void ResolveCameraReferences()
    {
        if (_cameraZoomController == null)
        {
            _cameraZoomController = FindObjectOfType<CameraController>();
        }

        if (_cameraZoomController != null)
        {
            if (_cameraRig == null)
            {
                _cameraRig = _cameraZoomController.transform;
            }

            if (_cameraTransform == null && _cameraZoomController.transform.childCount > 0)
            {
                _cameraTransform = _cameraZoomController.transform.GetChild(0);
            }
        }

        if (_pickCamera == null)
        {
            _pickCamera = Camera.main;
        }

        if (_cameraTransform == null && _pickCamera != null)
        {
            _cameraTransform = _pickCamera.transform;
        }

        if (_cameraRig == null && _cameraTransform != null)
        {
            _cameraRig = _cameraTransform.parent != null ? _cameraTransform.parent : _cameraTransform;
        }
    }

    /// <summary>重新收集子级模块。</summary>
    public void RefreshModuleList()
    {
        if (_modules == null || _modules.Length == 0)
        {
            _modules = GetComponentsInChildren<PlateMapDisplayModule>(true);
        }
    }

    /// <summary>
    /// 按模块名聚焦（默认匹配场景 GameObject 名；亦匹配 DisplayName）。
    /// </summary>
    /// <param name="moduleName">如 polySurface3</param>
    /// <returns>是否找到模块并开始播放动画。</returns>
    public bool FocusModule(string moduleName)
    {
        if (!TryGetModuleByName(moduleName, out PlateMapDisplayModule module))
        {
            Debug.LogWarning($"[PlateMapDisplayController] 未找到模块：{moduleName}");
            return false;
        }

        FocusModule(module);
        return true;
    }

    /// <summary>聚焦到指定模块（移动摄像机使模块位于视口中心；仅首次聚焦前缓存位姿供还原）。</summary>
    public void FocusModule(PlateMapDisplayModule module)
    {
        if (module == null || _cameraRig == null || _cameraTransform == null || _pickCamera == null)
        {
            return;
        }

        KillCameraTweens();
        KillAllModuleAlphaTweens();

        // 仅第一次聚焦前记录相机位姿，还原始终回到该初始机位
        if (!_hasPreFocusPose)
        {
            CaptureCameraPose();
            _hasPreFocusPose = true;
        }

        float targetZoomY = module.FocusCameraLocalY > 0f
            ? module.FocusCameraLocalY
            : _defaultFocusCameraLocalY;

        if (_cameraZoomController != null)
        {
            targetZoomY = Mathf.Clamp(targetZoomY, _cameraZoomController.MinZoomY, _cameraZoomController.MaxZoomY);
        }

        Vector3 camLocalTarget = _cameraTransform.localPosition;
        camLocalTarget.y = targetZoomY;

        if (!TryComputeRigPositionForModuleAtViewCenter(module, targetZoomY, out Vector3 rigTargetPos))
        {
            Debug.LogWarning("[PlateMapDisplayController] 无法计算聚焦机位。");
            return;
        }

        _focusedModule = module;
        string moduleKey = module.ModuleKey;

        EventManager.Instance?.TriggerPlateMapDisplayFocus(moduleKey);

        FadeModulesForFocus(module, _otherModuleFadeDuration, _otherModuleFadeEase);
        PlayCameraTween(
            rigTargetPos,
            _cameraRig.rotation,
            camLocalTarget,
            _cameraTransform.localRotation,
            targetZoomY,
            _focusDuration,
            _focusEase,
            onComplete: () => EventManager.Instance?.TriggerPlateMapFocusModuleCompleted(moduleKey));

        Debug.Log($"[PlateMapDisplayController] 聚焦模块：{moduleKey}");
    }

    private bool TryGetModuleByName(string moduleName, out PlateMapDisplayModule module)
    {
        module = null;
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return false;
        }

        RefreshModuleList();
        if (_modules == null)
        {
            return false;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            PlateMapDisplayModule candidate = _modules[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.ModuleKey == moduleName || candidate.DisplayName == moduleName)
            {
                module = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 计算相机架世界坐标：拉近后模块包围盒中心落在视口 (0.5,0.5) 光轴上。
    /// </summary>
    private bool TryComputeRigPositionForModuleAtViewCenter(
        PlateMapDisplayModule module,
        float targetCameraLocalY,
        out Vector3 rigWorldPosition)
    {
        rigWorldPosition = _cameraRig.position;
        Vector3 moduleCenter = module.GetWorldBounds().center;

        Vector3 targetCamLocal = _cameraTransform.localPosition;
        targetCamLocal.y = targetCameraLocalY;

        Vector3 predictedCamWorld = _cameraRig.TransformPoint(targetCamLocal);
        Quaternion predictedCamWorldRot = _cameraRig.rotation * _cameraTransform.localRotation;
        Vector3 forward = predictedCamWorldRot * Vector3.forward;

        if (forward.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        forward.Normalize();

        float depthAlongView = Vector3.Dot(moduleCenter - predictedCamWorld, forward);
        if (depthAlongView < 0.5f)
        {
            depthAlongView = Mathf.Max(targetCameraLocalY, 1f);
        }

        // 模块中心落在相机正前方 depth 处 → 视口中心对准模块
        Vector3 targetCameraWorld = moduleCenter - forward * depthAlongView;
        Vector3 rigToCamera = predictedCamWorld - _cameraRig.position;
        rigWorldPosition = targetCameraWorld - rigToCamera;
        return true;
    }

    /// <summary>DOTween 还原至首次聚焦前的摄像机位姿。</summary>
    /// <returns>是否已开始还原动画。</returns>
    public bool RestoreCameraPosition()
    {
        if (!_hasPreFocusPose || _cameraRig == null || _cameraTransform == null)
        {
            return false;
        }

        KillCameraTweens();
        FadeAllModulesForRestore();
        PlayCameraTween(
            _preFocusPose.RigWorldPosition,
            _preFocusPose.RigWorldRotation,
            _preFocusPose.CameraLocalPosition,
            _preFocusPose.CameraLocalRotation,
            _preFocusPose.ZoomLocalY,
            _restoreDuration,
            _restoreEase,
            onComplete: () =>
            {
                _hasPreFocusPose = false;
                _focusedModule = null;
                EventManager.Instance?.TriggerPlateMapRestoreCameraCompleted();
            });

        Debug.Log("[PlateMapDisplayController] 正在还原摄像机位置。");
        return true;
    }

    /// <summary>
    /// 过渡至 GaodeMap 前隐藏板块显示：缓存当前聚焦模块（未聚焦则为 null），再淡出可见板块。
    /// </summary>
    public void HidePlateDisplayForTransition(float duration, Ease ease)
    {
        _transitionCachedModule = _focusedModule;
        KillAllModuleAlphaTweens();
        RefreshModuleList();

        if (_transitionCachedModule != null)
        {
            _transitionCachedModule.TweenAlpha(0f, duration, ease);
            return;
        }

        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            _modules[i]?.TweenAlpha(0f, duration, ease);
        }
    }

    /// <summary>
    /// 从 GaodeMap 倒播回板块时恢复显示：有缓存则聚焦块高亮、其余隐藏；无缓存则全部显示。
    /// </summary>
    public void RestorePlateDisplayForTransition(float duration, Ease ease)
    {
        KillAllModuleAlphaTweens();
        RefreshModuleList();

        if (_transitionCachedModule != null)
        {
            FadeModulesForFocus(_transitionCachedModule, duration, ease);
            return;
        }

        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            _modules[i]?.TweenAlpha(1f, duration, ease);
        }
    }

    /// <summary>停止板块透明度 Tween（过渡被中断时调用）。</summary>
    public void KillPlateDisplayTweens()
    {
        KillAllModuleAlphaTweens();
    }

    /// <summary>聚焦：当前模块保持 1，其余模块淡出到 0。</summary>
    private void FadeModulesForFocus(PlateMapDisplayModule focusedModule, float duration, Ease ease)
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            PlateMapDisplayModule module = _modules[i];
            if (module == null)
            {
                continue;
            }

            float target = module == focusedModule ? 1f : 0f;
            module.TweenAlpha(target, duration, ease);
        }
    }

    /// <summary>还原：全部模块透明度回到 1。</summary>
    private void FadeAllModulesForRestore()
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            if (_modules[i] != null)
            {
                _modules[i].TweenAlpha(1f, _otherModuleFadeDuration, _otherModuleFadeEase);
            }
        }
    }

    private void KillAllModuleAlphaTweens()
    {
        RefreshModuleList();
        if (_modules == null)
        {
            return;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            _modules[i]?.KillAlphaTween();
        }
    }

    private void PlayCameraTween(
        Vector3 rigWorldPos,
        Quaternion rigWorldRot,
        Vector3 camLocalPos,
        Quaternion camLocalRot,
        float syncZoomY,
        float duration,
        Ease ease,
        TweenCallback onComplete = null)
    {
        if (_cameraZoomController != null)
        {
            _cameraZoomController.ZoomControlEnabled = false;
        }

        _cameraTweenSequence = DOTween.Sequence();
        _cameraTweenSequence.Join(_cameraRig.DOMove(rigWorldPos, duration).SetEase(ease));
        _cameraTweenSequence.Join(_cameraRig.DORotateQuaternion(rigWorldRot, duration).SetEase(ease));
        _cameraTweenSequence.Join(_cameraTransform.DOLocalMove(camLocalPos, duration).SetEase(ease));
        _cameraTweenSequence.Join(_cameraTransform.DOLocalRotateQuaternion(camLocalRot, duration).SetEase(ease));
        _cameraTweenSequence.OnComplete(() =>
        {
            if (_cameraZoomController != null)
            {
                _cameraZoomController.SetTargetZoomY(syncZoomY, immediate: true);
                _cameraZoomController.ZoomControlEnabled = true;
            }

            onComplete?.Invoke();
        });
    }

    /// <summary>记录当前相机位姿（仅首次聚焦时调用一次）。</summary>
    private void CaptureCameraPose()
    {
        float zoomY = _cameraZoomController != null
            ? _cameraZoomController.CurrentCameraLocalY
            : _cameraTransform.localPosition.y;

        _preFocusPose = new CameraPoseSnapshot
        {
            RigWorldPosition = _cameraRig.position,
            RigWorldRotation = _cameraRig.rotation,
            CameraLocalPosition = _cameraTransform.localPosition,
            CameraLocalRotation = _cameraTransform.localRotation,
            ZoomLocalY = zoomY
        };
    }

    private void KillCameraTweens()
    {
        if (_cameraTweenSequence != null && _cameraTweenSequence.IsActive())
        {
            _cameraTweenSequence.Kill();
        }

        _cameraTweenSequence = null;
        _cameraRig?.DOKill();
        _cameraTransform?.DOKill();

        if (_cameraZoomController != null)
        {
            _cameraZoomController.ZoomControlEnabled = true;
        }
    }

    private void HandleModuleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPosition = Input.mousePosition;
            return;
        }

        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        if (Vector2.Distance(Input.mousePosition, _mouseDownPosition) > _clickMaxDragPixels)
        {
            return;
        }

        if (_pickCamera == null)
        {
            return;
        }

        Ray ray = _pickCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _pickLayerMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        PlateMapDisplayModule module = hit.collider.GetComponentInParent<PlateMapDisplayModule>();
        if (module == null || !IsRegisteredModule(module))
        {
            return;
        }

        FocusModule(module.ModuleKey);
    }

    private bool IsRegisteredModule(PlateMapDisplayModule module)
    {
        if (_modules == null || _modules.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < _modules.Length; i++)
        {
            if (_modules[i] == module)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("为子级 Mesh 批量添加 PlateMapDisplayModule")]
    private void EditorAddModulesToChildMeshes()
    {
        if (_plateMapRoot == null)
        {
            _plateMapRoot = transform;
        }

        MeshRenderer[] renderers = _plateMapRoot.GetComponentsInChildren<MeshRenderer>(true);
        int added = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            GameObject go = renderers[i].gameObject;
            if (go.GetComponent<PlateMapDisplayModule>() != null)
            {
                continue;
            }

            if (go.name == "VehiclePoints")
            {
                continue;
            }

            go.AddComponent<PlateMapDisplayModule>();
            added++;
        }

        RefreshModuleList();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[PlateMapDisplayController] 已添加 {added} 个模块标记。");
    }
#endif
}
