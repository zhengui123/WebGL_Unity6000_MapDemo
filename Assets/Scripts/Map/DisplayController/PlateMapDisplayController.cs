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

    [Header("关联（可选）")]
    [SerializeField] private PlateMapProvinceTechOpacity _provinceTechOpacity;

    private Vector3 _mouseDownPosition;
    private Sequence _cameraTweenSequence;
    private PlateMapDisplayModule _focusedModule;
    private CameraPoseSnapshot _preFocusPose;
    private bool _hasPreFocusPose;

    /// <summary>当前聚焦的模块；无则为 null。</summary>
    public PlateMapDisplayModule FocusedModule => _focusedModule;

    /// <summary>是否已缓存首次聚焦前相机位姿（可调用还原）。</summary>
    public bool CanRestoreCamera => _hasPreFocusPose;

    private void Awake()
    {
        if (_plateMapRoot == null)
        {
            _plateMapRoot = transform;
        }

        ResolveCameraReferences();
        RefreshModuleList();
        EventManager.Instance.OnPlateMapDisplayFocus += OnPlateMapDisplayFocus;
    }

    private void OnPlateMapDisplayFocus(string plateMapName)
    {
        PlateMapVehiclePointsJsonApiDemo.Instance.GenerateTestJsonAndPushApi();

        if (plateMapName == _focusedModule.DisplayName)
        {
            Debug.Log($"[PlateMapDisplayController] 聚焦模块比对一致：{plateMapName}");
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

        if (_restoreKey != KeyCode.None && Input.GetKeyDown(_restoreKey) && CanRestoreCamera)
        {
            RestoreCameraPosition();
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

        if (_provinceTechOpacity == null)
        {
            _provinceTechOpacity = GetComponentInChildren<PlateMapProvinceTechOpacity>(true);
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

        _focusedModule = module;

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

        EventManager.Instance.TriggerPlateMapDisplayFocus(module.DisplayName);

        FadeModulesForFocus(module);
        PlayCameraTween(rigTargetPos, _cameraRig.rotation, camLocalTarget, _cameraTransform.localRotation, targetZoomY,
            _focusDuration, _focusEase);
        Debug.Log($"[PlateMapDisplayController] 聚焦模块：{module.DisplayName}");
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
    public void RestoreCameraPosition()
    {
        if (!_hasPreFocusPose || _cameraRig == null || _cameraTransform == null)
        {
            return;
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
            });

        Debug.Log("[PlateMapDisplayController] 正在还原摄像机位置。");
    }

    /// <summary>聚焦：当前模块保持 1，其余模块淡出到 0。</summary>
    private void FadeModulesForFocus(PlateMapDisplayModule focusedModule)
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
            module.TweenAlpha(target, _otherModuleFadeDuration, _otherModuleFadeEase);
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

        FocusModule(module);
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
