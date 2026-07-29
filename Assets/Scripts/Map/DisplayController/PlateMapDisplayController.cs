using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图显示控制器：启用后点击模块则移动 CameraPivot 居中并按视距装框（不改 FogCamera 本地位姿）；可 DOTween 还原至聚焦前位姿。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapDisplayController : MonoBehaviour
{
    private static readonly int BoundaryAlphaId = Shader.PropertyToID("_Alpha");

    private struct CameraPoseSnapshot
    {
        public Vector3 RigWorldPosition;
        public Quaternion RigWorldRotation;
        public Vector3 CameraLocalPosition;
        public Quaternion CameraLocalRotation;
        public float ZoomLocalY;
    }

    private struct PlateRootLocalPose
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    [Header("板块根（收集模块）")]
    [SerializeField] private Transform _plateMapRoot;

    [Header("摄像机")]
    [Tooltip("相机架（CameraController 所在物体，做世界平移）")]
    [SerializeField] private Transform _cameraRig;
    [Tooltip("实际渲染相机（滚轮仍调其局部 Y；省级聚焦动画不移动它）")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Camera _pickCamera;
    [SerializeField] private CameraController _cameraZoomController;

    [Header("拾取")]
    [SerializeField] private float _raycastMaxDistance = 5000f;
    [SerializeField] private float _clickMaxDragPixels = 8f;
    [SerializeField] private LayerMask _pickLayerMask = Physics.DefaultRaycastLayers;

    [Header("聚焦动画")]
    [SerializeField] private float _defaultFocusCameraLocalY = 650f;
    [Tooltip("勾选后按省份包围盒自动计算拉近高度，使省份占 Game 视图约 Viewport Fill Ratio")]
    [SerializeField] private bool _autoFitProvinceToViewport = true;
    [Tooltip("省级聚焦时目标占视口比例（越小越远；>1 更近，省可能超出视口；0.55≈留边约 45%）")]
    [Range(0.1f, 3f)]
    [SerializeField] private float _provinceViewportFillRatio = 0.55f;
    [Tooltip("视距缩放（装框距离×此系数；俯视场景一般 1）")]
    [SerializeField] private float _provinceFitDistanceToLocalYScale = 1f;
    [Tooltip("省自适应允许的最小视距（沿视线，作用于 CameraPivot 远近）")]
    [SerializeField] private float _provinceFitMinLocalY = 80f;
    [Tooltip("省自适应允许的最大视距")]
    [SerializeField] private float _provinceFitMaxLocalY = 5000f;
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
    [SerializeField] private GameObject _worldMapBoundaryLine;


    private Vector3 _mouseDownPosition;
    private Sequence _cameraTweenSequence;
    private Tween _worldMapBoundaryLineAlphaTween;
    private PlateMapDisplayModule _focusedModule;
    private PlateMapDisplayModule _transitionCachedModule;
    private CameraPoseSnapshot _preFocusPose;
    private bool _hasPreFocusPose;

    /// <summary>各板块根首次记录的本地位姿，切回国内时可还原。</summary>
    private readonly Dictionary<int, PlateRootLocalPose> _plateRootOriginalLocalPoses =
        new Dictionary<int, PlateRootLocalPose>(32);

    /// <summary>国家级 Home 下的相机 LocalY（首次 Snap 时缓存，切回国内/切板块前恢复）。</summary>
    private float _countryHomeZoomLocalY = 2000f;
    private bool _hasCountryHomeZoomLocalY;
    private Renderer[] _worldMapBoundaryLineRenderers;
    private MaterialPropertyBlock _worldMapBoundaryPropertyBlock;
    private float _worldMapBoundaryCurrentAlpha = 1f;

    /// <summary>当前聚焦的模块；无则为 null。</summary>
    public PlateMapDisplayModule FocusedModule => _focusedModule;

    /// <summary>过渡隐藏前缓存的聚焦模块；未聚焦则为 null。</summary>
    public PlateMapDisplayModule TransitionCachedModule => _transitionCachedModule;

    /// <summary>是否已缓存首次聚焦前相机位姿（可调用还原）。</summary>
    public bool CanRestoreCamera => _hasPreFocusPose;

    /// <summary>板块聚焦 / 还原相机 DOTween 是否正在播放。</summary>
    public bool IsCameraTweening =>
        _cameraTweenSequence != null && _cameraTweenSequence.IsActive() && _cameraTweenSequence.IsPlaying();

    private static PlateMapDisplayController _instance;

    /// <summary>场景中的显示控制器（供 MapApi 等调用）。</summary>
    public static PlateMapDisplayController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlateMapDisplayController>(FindObjectsInactive.Include);
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
        ResolveWorldMapBoundaryLine();
        RefreshModuleList();
    }

    private void OnDestroy()
    {
        KillWorldMapBoundaryLineTween();
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
        // 聚焦开始时 GameManager 会禁用本组件；此时尚未创建 DOTween，勿解除缩放抑制
        KillCameraTweens(releaseZoomSuppress: false);
        KillAllModuleAlphaTweens();
        KillWorldMapBoundaryLineTween();
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

    private void ResolveWorldMapBoundaryLine()
    {
        if (_worldMapBoundaryLine != null)
        {
            return;
        }

        _worldMapBoundaryLine = GameObject.Find("世界地图边界线");
        CacheWorldMapBoundaryLineRenderers();
    }

    private void CacheWorldMapBoundaryLineRenderers()
    {
        if (_worldMapBoundaryLine == null)
        {
            _worldMapBoundaryLineRenderers = null;
            return;
        }

        _worldMapBoundaryLineRenderers = _worldMapBoundaryLine.GetComponentsInChildren<Renderer>(true);
        if (_worldMapBoundaryPropertyBlock == null)
        {
            _worldMapBoundaryPropertyBlock = new MaterialPropertyBlock();
        }
    }

    /// <summary>重新收集子级模块（相对当前板块根）。</summary>
    public void RefreshModuleList()
    {
        if (_modules == null || _modules.Length == 0)
        {
            Transform root = _plateMapRoot != null ? _plateMapRoot : transform;
            _modules = root.GetComponentsInChildren<PlateMapDisplayModule>(true);
        }
    }

    /// <summary>
    /// 切换世界地图板块根：重绑模块收集范围并瞬时清除聚焦状态。
    /// 用于国内/国外大板块切换后只显示当前板块下的模块。
    /// </summary>
    public void BindPlateMapRoot(Transform plateRoot)
    {
        _plateMapRoot = plateRoot != null ? plateRoot : transform;
        KillCameraTweens();
        _focusedModule = null;
        _transitionCachedModule = null;
        _hasPreFocusPose = false;
        PlateProvinceFocusResolver.ClearCache();
        _cameraZoomController?.ResetZoomLimitOverrides();
        _modules = _plateMapRoot.GetComponentsInChildren<PlateMapDisplayModule>(true);

        PlateMapDisplayModule[] modules = _modules;
        if (modules == null)
        {
            return;
        }

        for (int i = 0; i < modules.Length; i++)
        {
            modules[i]?.KillAlphaTween();
            modules[i]?.ApplyAlphaImmediate(1f);
        }
    }

    /// <summary>
    /// 瞬时归位国家级相机（杀 Tween、恢复 Home 缩放、不播还原动画）。
    /// </summary>
    public void SnapCameraToCountryHomeImmediate()
    {
        ResolveCameraReferences();
        ResolveWorldMapBoundaryLine();
        KillCameraTweens();

        CacheCountryHomeZoomIfNeeded();
        if (_hasCountryHomeZoomLocalY && _cameraZoomController != null)
        {
            _cameraZoomController.SetTargetZoomY(_countryHomeZoomLocalY, immediate: true, clampToLimits: true);
        }

        CountryMapZoomController countryZoom = CountryMapZoomController.Instance;
        if (countryZoom != null &&
            countryZoom.TryGetCountryHomePose(out Vector3 homePos, out Quaternion homeRot) &&
            _cameraRig != null)
        {
            _cameraRig.SetPositionAndRotation(homePos, homeRot);
            countryZoom.ResetToCountryHomeAfterProvinceRestore();
        }
        else if (_hasPreFocusPose && _cameraRig != null && _cameraTransform != null)
        {
            _cameraRig.SetPositionAndRotation(_preFocusPose.RigWorldPosition, _preFocusPose.RigWorldRotation);
            _cameraTransform.localPosition = _preFocusPose.CameraLocalPosition;
            _cameraTransform.localRotation = _preFocusPose.CameraLocalRotation;
        }

        _focusedModule = null;
        _transitionCachedModule = null;
        _hasPreFocusPose = false;
        PlateProvinceFocusResolver.ClearCache();
        _cameraZoomController?.ResetZoomLimitOverrides();
        KillAllModuleAlphaTweens();
        PlateMapDisplayModule[] modules = CollectAllModules(forceRefresh: true);
        if (modules != null)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                modules[i]?.ApplyAlphaImmediate(1f);
            }
        }

        FadeWorldMapBoundaryLineForCurrentRegion(immediate: true);
    }

    private void CacheCountryHomeZoomIfNeeded()
    {
        if (_hasCountryHomeZoomLocalY || _cameraZoomController == null)
        {
            return;
        }

        float y = _cameraZoomController.CurrentCameraLocalY;
        if (y > 1f && y < float.MaxValue * 0.5f)
        {
            _countryHomeZoomLocalY = y;
            _hasCountryHomeZoomLocalY = true;
        }
    }

    /// <summary>瞬时清除聚焦状态（不播还原动画）。</summary>
    public void ClearFocusState()
    {
        KillCameraTweens();
        _focusedModule = null;
        _transitionCachedModule = null;
        _hasPreFocusPose = false;
        PlateProvinceFocusResolver.ClearCache();
        _cameraZoomController?.ResetZoomLimitOverrides();
    }

    /// <summary>若已缓存过原始本地位姿则还原（用于国内板块回退）。</summary>
    public bool RestorePlateRootOriginalLocalPose(Transform plateRoot)
    {
        if (plateRoot == null)
        {
            return false;
        }

        int id = plateRoot.GetInstanceID();
        if (!_plateRootOriginalLocalPoses.TryGetValue(id, out PlateRootLocalPose pose))
        {
            return false;
        }

        plateRoot.localPosition = pose.LocalPosition;
        plateRoot.localRotation = pose.LocalRotation;
        plateRoot.localScale = pose.LocalScale;
        return true;
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

    /// <summary>
    /// 聚焦到指定模块：只移动 CameraPivot，使模块居中并按视距装框；FogCamera 本地位姿不变。
    /// 仅首次聚焦前缓存位姿供还原。
    /// </summary>
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

        float viewDistance = ResolveProvinceFocusViewDistance(module);
        bool fromAutoFit = _autoFitProvinceToViewport && _pickCamera != null;

        // 自动适配不要再用 CameraController.MinZoomY(常为 500) 钳死，否则大小省会同一高度
        if (!fromAutoFit && _cameraZoomController != null)
        {
            viewDistance = Mathf.Clamp(viewDistance, _cameraZoomController.MinZoomY, _cameraZoomController.MaxZoomY);
        }
        else
        {
            viewDistance = Mathf.Clamp(viewDistance, _provinceFitMinLocalY, _provinceFitMaxLocalY);
        }

        // FogCamera 保持当前本地坐标；远近完全由 CameraPivot 承担
        Vector3 fogLocalKeep = _cameraTransform.localPosition;
        Quaternion fogLocalRotKeep = _cameraTransform.localRotation;

        if (!TryComputeRigPositionForModuleAtViewCenter(module, fogLocalKeep, viewDistance, out Vector3 rigTargetPos))
        {
            Debug.LogWarning("[PlateMapDisplayController] 无法计算聚焦机位。");
            return;
        }

        _focusedModule = module;
        string moduleKey = module.ModuleKey;

        // 进入省级时立即缓存 name/code，供下钻二维地图使用
        PlateProvinceFocusResolver.TryCacheFromModule(module);
        FadeWorldMapBoundaryLine(0f, _otherModuleFadeDuration, _otherModuleFadeEase, disableWhenHidden: true);

        // 先硬关缩放；先发聚焦事件（会禁用组件并 OnDisable Kill 空 Tween）；再开 DOTween
        CountryMapZoomController.Instance?.SetSuppressed(true);
        EventManager.Instance?.TriggerPlateMapDisplayFocus(moduleKey);

        FadeModulesForFocus(module, _otherModuleFadeDuration, _otherModuleFadeEase);
        PlayCameraTween(
            rigTargetPos,
            _cameraRig.rotation,
            fogLocalKeep,
            fogLocalRotKeep,
            fogLocalKeep.y,
            _focusDuration,
            _focusEase,
            clampSyncZoom: true,
            tweenCameraLocal: false,
            onComplete: () =>
            {
                CountryMapZoomController.Instance?.SetSuppressed(false);
                EventManager.Instance?.TriggerPlateMapFocusModuleCompleted(moduleKey);
            });

        Debug.Log($"[PlateMapDisplayController] 聚焦模块：{moduleKey} | viewDistance={viewDistance:F1}");
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
    /// 省级装框视距（沿视线）：按子物体外包围盒 XZ 最长边匹配通用屏幕占比。
    /// 该距离用于摆 CameraPivot，不写入 FogCamera。
    /// </summary>
    private float ResolveProvinceFocusViewDistance(PlateMapDisplayModule module)
    {
        if (_autoFitProvinceToViewport && _pickCamera != null)
        {
            Bounds bounds = module.GetWorldBounds();
            float depth = PlateMapCameraFitUtility.ComputeViewDistanceToFitBounds(
                _pickCamera,
                bounds,
                _provinceViewportFillRatio);
            depth *= Mathf.Max(0.01f, _provinceFitDistanceToLocalYScale);
            depth = Mathf.Clamp(depth, _provinceFitMinLocalY, _provinceFitMaxLocalY);

            if (depth > 1f)
            {
                float longestXZ = Mathf.Max(bounds.size.x, bounds.size.z);
                Debug.Log(
                    $"[PlateMapDisplayController] 省聚焦严格装框 | module={module.ModuleKey} | " +
                    $"fill={_provinceViewportFillRatio:P0} | viewDistance={depth:F1} | " +
                    $"longestXZ={longestXZ:F1} | boundsXZ=({bounds.size.x:F1},{bounds.size.z:F1})");
                return depth;
            }
        }

        if (module.FocusCameraLocalY > 0f)
        {
            return module.FocusCameraLocalY;
        }

        return _defaultFocusCameraLocalY;
    }

    /// <summary>
    /// 计算 CameraPivot 世界坐标：在 FogCamera 本地位姿不变的前提下，
    /// 使实际相机位于模块中心沿视线后退 viewDistanceAlongForward 处。
    /// </summary>
    private bool TryComputeRigPositionForModuleAtViewCenter(
        PlateMapDisplayModule module,
        Vector3 fogCameraLocalOffset,
        float viewDistanceAlongForward,
        out Vector3 rigWorldPosition)
    {
        rigWorldPosition = _cameraRig.position;
        Vector3 moduleCenter = module.GetWorldBounds().center;

        Quaternion predictedCamWorldRot = _cameraRig.rotation * _cameraTransform.localRotation;
        Vector3 forward = predictedCamWorldRot * Vector3.forward;

        if (forward.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        forward.Normalize();

        float depthAlongView = Mathf.Max(viewDistanceAlongForward, 1f);

        // 模块中心落在相机正前方 depth 处；FogCamera 本地偏移不变，只反推 Pivot
        Vector3 targetCameraWorld = moduleCenter - forward * depthAlongView;
        Vector3 localOffsetWorld = _cameraRig.rotation * fogCameraLocalOffset;
        rigWorldPosition = targetCameraWorld - localOffsetWorld;
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
        CountryMapZoomController.Instance?.SetSuppressed(true);
        FadeWorldMapBoundaryLineForCurrentRegion(immediate: false);
        EventManager.Instance?.TriggerPlateMapRestoreCameraStarted();

        // 省→国家：CameraPivot 终点用国家级缩放 Home，丢弃进省前缩放，避免与 CountryMapZoom 冲突
        Vector3 rigTargetPos = _preFocusPose.RigWorldPosition;
        Quaternion rigTargetRot = _preFocusPose.RigWorldRotation;
        CountryMapZoomController countryZoom = CountryMapZoomController.Instance;
        if (countryZoom != null &&
            countryZoom.TryGetCountryHomePose(out Vector3 homePos, out Quaternion homeRot))
        {
            rigTargetPos = homePos;
            rigTargetRot = homeRot;
        }

        PlayCameraTween(
            rigTargetPos,
            rigTargetRot,
            _preFocusPose.CameraLocalPosition,
            _preFocusPose.CameraLocalRotation,
            _preFocusPose.ZoomLocalY,
            _restoreDuration,
            _restoreEase,
            clampSyncZoom: true,
            onComplete: () =>
            {
                _cameraZoomController?.ResetZoomLimitOverrides();
                _hasPreFocusPose = false;
                _focusedModule = null;
                PlateProvinceFocusResolver.ClearCache();
                CountryMapZoomController.Instance?.SetSuppressed(false);
                CountryMapZoomController.Instance?.ResetToCountryHomeAfterProvinceRestore();
                EventManager.Instance?.TriggerPlateMapRestoreCameraCompleted();
            });

        Debug.Log("[PlateMapDisplayController] 正在还原摄像机位置（国家级 Home）。");
        return true;
    }

    /// <summary>
    /// 地球级跳转完成后立刻还原板块透明度（板块根节点可能已 SetActive(false)，仍可写入材质）。
    /// </summary>
    public void RestoreAllModulesAlphaImmediate()
    {
        KillCameraTweens();
        _hasPreFocusPose = false;
        _focusedModule = null;
        _transitionCachedModule = null;
        PlateProvinceFocusResolver.ClearCache();
        _cameraZoomController?.ResetZoomLimitOverrides();

        PlateMapDisplayModule[] modules = CollectAllModules(forceRefresh: true);
        if (modules == null)
        {
            return;
        }

        for (int i = 0; i < modules.Length; i++)
        {
            modules[i]?.KillAlphaTween();
            modules[i]?.ApplyAlphaImmediate(1f);
        }
    }

    /// <summary>过渡至 GaodeMap 前隐藏板块显示：缓存当前聚焦模块（未聚焦则为 null），再淡出可见板块。</summary>
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
        PlateMapDisplayModule[] modules = CollectAllModules(forceRefresh: true);
        if (modules == null)
        {
            return;
        }

        for (int i = 0; i < modules.Length; i++)
        {
            modules[i]?.KillAlphaTween();
        }
    }

    /// <summary>强制从板块根收集模块（包含未激活子物体）。</summary>
    private PlateMapDisplayModule[] CollectAllModules(bool forceRefresh = false)
    {
        if (!forceRefresh && _modules != null && _modules.Length > 0)
        {
            return _modules;
        }

        Transform root = _plateMapRoot != null ? _plateMapRoot : transform;
        _modules = root.GetComponentsInChildren<PlateMapDisplayModule>(true);
        return _modules;
    }

    private void PlayCameraTween(
        Vector3 rigWorldPos,
        Quaternion rigWorldRot,
        Vector3 camLocalPos,
        Quaternion camLocalRot,
        float syncZoomY,
        float duration,
        Ease ease,
        bool clampSyncZoom = true,
        bool tweenCameraLocal = true,
        TweenCallback onComplete = null)
    {
        if (_cameraZoomController != null)
        {
            _cameraZoomController.ZoomControlEnabled = false;
        }

        _cameraTweenSequence = DOTween.Sequence();
        _cameraTweenSequence.Join(_cameraRig.DOMove(rigWorldPos, duration).SetEase(ease));
        _cameraTweenSequence.Join(_cameraRig.DORotateQuaternion(rigWorldRot, duration).SetEase(ease));
        if (tweenCameraLocal)
        {
            _cameraTweenSequence.Join(_cameraTransform.DOLocalMove(camLocalPos, duration).SetEase(ease));
            _cameraTweenSequence.Join(_cameraTransform.DOLocalRotateQuaternion(camLocalRot, duration).SetEase(ease));
        }

        _cameraTweenSequence.OnComplete(() =>
        {
            if (_cameraZoomController != null)
            {
                // 聚焦路径传入当前 FogCamera.y，仅同步滚轮状态，不改子相机坐标
                _cameraZoomController.SetTargetZoomY(syncZoomY, immediate: true, clampToLimits: clampSyncZoom);
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

    private void KillCameraTweens(bool releaseZoomSuppress = true)
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

        if (releaseZoomSuppress)
        {
            CountryMapZoomController.Instance?.SetSuppressed(false);
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
        Debug.Log($"[PlateMapDisplayController] 点击模块：{hit.collider.transform.parent.name}");
        PlateMapDisplayModule module = hit.collider.transform.parent.GetComponentInParent<PlateMapDisplayModule>();
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

    private void FadeWorldMapBoundaryLineForCurrentRegion(bool immediate)
    {
        bool visible = WorldMapRegionContext.Mode == WorldMapRegionMode.Foreign;
        float targetAlpha = visible ? 1f : 0f;
        FadeWorldMapBoundaryLine(
            targetAlpha,
            immediate ? 0f : _otherModuleFadeDuration,
            _otherModuleFadeEase,
            disableWhenHidden: true);
    }

    private void FadeWorldMapBoundaryLine(float targetAlpha, float duration, Ease ease, bool disableWhenHidden)
    {
        ResolveWorldMapBoundaryLine();
        CacheWorldMapBoundaryLineRenderers();
        if (_worldMapBoundaryLine == null || _worldMapBoundaryLineRenderers == null || _worldMapBoundaryLineRenderers.Length == 0)
        {
            return;
        }

        KillWorldMapBoundaryLineTween();
        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (targetAlpha > 0f && !_worldMapBoundaryLine.activeSelf)
        {
            _worldMapBoundaryLine.SetActive(true);
        }

        if (duration <= 0f)
        {
            ApplyWorldMapBoundaryLineAlphaImmediate(targetAlpha);
            if (disableWhenHidden && targetAlpha <= 0f && _worldMapBoundaryLine.activeSelf)
            {
                _worldMapBoundaryLine.SetActive(false);
            }

            return;
        }

        _worldMapBoundaryLineAlphaTween = DOTween
            .To(() => _worldMapBoundaryCurrentAlpha, ApplyWorldMapBoundaryLineAlphaImmediate, targetAlpha, duration)
            .SetEase(ease)
            .OnComplete(() =>
            {
                _worldMapBoundaryLineAlphaTween = null;
                if (disableWhenHidden && targetAlpha <= 0f && _worldMapBoundaryLine != null && _worldMapBoundaryLine.activeSelf)
                {
                    _worldMapBoundaryLine.SetActive(false);
                }
            });
    }

    private void ApplyWorldMapBoundaryLineAlphaImmediate(float alpha)
    {
        _worldMapBoundaryCurrentAlpha = Mathf.Clamp01(alpha);
        if (_worldMapBoundaryLineRenderers == null || _worldMapBoundaryLineRenderers.Length == 0)
        {
            return;
        }

        if (_worldMapBoundaryPropertyBlock == null)
        {
            _worldMapBoundaryPropertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < _worldMapBoundaryLineRenderers.Length; i++)
        {
            Renderer renderer = _worldMapBoundaryLineRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_worldMapBoundaryPropertyBlock);
            _worldMapBoundaryPropertyBlock.SetFloat(BoundaryAlphaId, _worldMapBoundaryCurrentAlpha);
            renderer.SetPropertyBlock(_worldMapBoundaryPropertyBlock);
        }
    }

    private void KillWorldMapBoundaryLineTween()
    {
        if (_worldMapBoundaryLineAlphaTween != null && _worldMapBoundaryLineAlphaTween.IsActive())
        {
            _worldMapBoundaryLineAlphaTween.Kill();
        }

        _worldMapBoundaryLineAlphaTween = null;
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
