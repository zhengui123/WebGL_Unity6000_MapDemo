using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 车辆 ↔ 零件、车辆 ↔ 攻击路径 过渡控制器。
/// 零件过渡：零件移动 + KJ_Car 溶解显隐；攻击路径过渡：KJ_Car 溶解后与 AttackPathController 衔接。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionController : MonoBehaviour
{
    #region 字段与属性

    [Header("零件列表（按 GameObject 名称查找；名称为空时使用第一个）")]
    [SerializeField] private List<Transform> _partRoots = new List<Transform>();

    [Header("目标位姿")]
    [SerializeField] private Transform _firstTarget;
    [SerializeField] private Transform _secondTarget;

    [Header("KJ_Car（留空则自动查找 Model/Car/KJ_Car）")]
    [SerializeField] private GameObject _kjCarRoot;

    [Header("车辆旋转（倒播时重置拖拽旋转）")]
    [SerializeField] private MouseDragYawRotate _carDragYawRotate;

    [Header("过渡参数")]
    [SerializeField] private float _firstMoveDuration = 1.2f;
    [SerializeField] private float _secondMoveDuration = 1.5f;
    [SerializeField] private Ease _moveEase = Ease.InOutQuad;
    [SerializeField] private float _kjDissolveDuration = 1.5f;
    [SerializeField] private Ease _kjDissolveEase = Ease.InOutQuad;
    [SerializeField] private float _dissolveNoiseScale = 12f;

    [Header("攻击路径（车辆 ↔ 攻击路径过渡）")]
    [SerializeField] private AttackPathController _attackPathController;
    [SerializeField] private List<Transform> _showAttackPath = new List<Transform>();
    [SerializeField] private Transform _attackPathCamera;
    [Tooltip("车辆 → 攻击路径时相机移动到的目标位姿（世界坐标）")]
    [SerializeField] private Transform _attackPathCameraTarget;
    [SerializeField] private float _attackPathCameraMoveDuration = 1.2f;
    [SerializeField] private Ease _attackPathCameraEase = Ease.InOutQuad;

    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();
    private readonly Dictionary<int, PartInitialState> _partInitialStates = new Dictionary<int, PartInitialState>();
    private Sequence _sequence;
    private bool _isTransitioning;
    private Transform _activePart;
    private string _lastPartName;
    private Vector3 _cachedPartLocalPosition;
    private Quaternion _cachedPartLocalRotation;
    private Vector3 _cachedPartLocalScale;
    private Vector3 _cachedAttackPathCameraPosition;
    private Quaternion _cachedAttackPathCameraRotation;
    private bool _hasCachedAttackPathCameraPose;

    private struct PartInitialState
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public bool IsActive;
    }

    public bool IsTransitioning => _isTransitioning;
    public Transform ActivePart => _activePart;
    public string LastPartName => _lastPartName;
    public IReadOnlyList<Transform> ShowAttackPath => _showAttackPath;
    public IReadOnlyList<Transform> ConfiguredPartRoots => _partRoots;

    private static VehicleToPartTransitionController _instance;

    public static VehicleToPartTransitionController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<VehicleToPartTransitionController>();
            }

            return _instance;
        }
    }

    #endregion

    #region 生命周期

    private void Awake()
    {
        _instance = this;
        ResolveKjCarReference();
        ResolveAttackPathController();
        CacheAllPartInitialStates();
        StopAndHideAttackPath();
        ConfigureShowAttackPath(_partRoots);
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region 车辆 ↔ 零件过渡

    /// <summary>
    /// 播放车辆 → 零件过渡。
    /// </summary>
    /// <param name="partName">零件 GameObject 名称；null 或空字符串时使用列表第一项。</param>
    public bool PlayTransition(string partName = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryResolvePart(partName, out Transform part))
        {
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            Debug.LogError("[VehicleToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        ResolveKjCarReference();

        if (_kjCarRoot == null)
        {
            Debug.LogError("[VehicleToPart] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        if (_kjDissolve.MaterialCount == 0)
        {
            Debug.LogWarning("[VehicleToPart] KJ_Car 未找到带 _DissolveAmount 的材质。");
        }

        KillSequence();
        StopAndHideAttackPath();
        RestoreAllPartsToInitialState();
        ApplyCachedPoseFromInitialState(part);

        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
        EventManager.Instance?.TriggerVehicleToPartTransitionStarted(_lastPartName);
        part.gameObject.SetActive(true);

        _sequence = DOTween.Sequence();
        AppendMoveToTarget(_sequence, part, _firstTarget, _firstMoveDuration);
        JoinCarHideTween(_sequence);

        _sequence.AppendCallback(() => BeginForwardSecondPhase(part));
        return true;
    }

    /// <summary>
    /// 倒播车辆 → 零件过渡：零件第二目标 → 第一目标 → 初始位姿，KJ_Car 溶解显现。
    /// </summary>
    /// <param name="partName">零件名称；为空时使用上次正播记录的零件。</param>
    public bool PlayTransitionReverse(string partName = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (string.IsNullOrEmpty(partName))
        {
            partName = _lastPartName;
        }

        if (string.IsNullOrEmpty(partName) || !TryResolvePart(partName, out Transform part))
        {
            Debug.LogError("[VehicleToPart] 倒播失败：未指定零件或找不到对应零件。");
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            Debug.LogError("[VehicleToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        if (!TryGetPartInitialState(part, out PartInitialState initialState))
        {
            Debug.LogWarning("[VehicleToPart] 未找到零件开局位姿缓存，倒播终点将使用当前位姿。");
            ApplyCachedPoseFromCurrentTransform(part);
        }
        else
        {
            ApplyCachedPoseFromInitialState(part);
        }

        ResolveKjCarReference();
        if (_kjCarRoot == null)
        {
            Debug.LogError("[VehicleToPart] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        KillSequence();
        StopAndHideAttackPath();
        ResetCarDragRotation();
        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
        EventManager.Instance?.TriggerVehicleToPartTransitionReverseStarted(part.name);
        part.gameObject.SetActive(true);
        _kjCarRoot.SetActive(true);
        _kjDissolve.SetDissolveAmount(1f);

        _sequence = DOTween.Sequence();
        AppendMoveToTarget(_sequence, part, _firstTarget, _secondMoveDuration);
        JoinCarAppearTween(_sequence);
        _sequence.AppendCallback(() => BeginReverseSecondPhase(part));
        return true;
    }

    /// <summary>按名称在缓存列表中查找零件；名称为空时返回第一个有效项。</summary>
    public bool TryResolvePart(string partName, out Transform part)
    {
        part = null;
        if (_partRoots == null || _partRoots.Count == 0)
        {
            Debug.LogError("[VehicleToPart] 零件列表为空，请在 Inspector 中挂载。");
            return false;
        }

        if (string.IsNullOrEmpty(partName))
        {
            part = FindFirstValidPart();
            if (part == null)
            {
                Debug.LogError("[VehicleToPart] 列表中没有有效的零件 Transform。");
            }

            return part != null;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            Transform candidate = _partRoots[i];
            if (candidate != null && candidate.name == partName)
            {
                part = candidate;
                return true;
            }
        }

        Debug.LogError($"[VehicleToPart] 未在列表中找到名为「{partName}」的零件。");
        return false;
    }

    private void BeginForwardSecondPhase(Transform part)
    {
        _sequence = DOTween.Sequence();
        AppendMoveToTarget(_sequence, part, _secondTarget, _secondMoveDuration);
        _sequence.OnComplete(CompleteForwardTransition);
    }

    private void BeginReverseSecondPhase(Transform part)
    {
        _sequence = DOTween.Sequence();
        Tween moveTween = BuildMoveTweenToCachedPose(part, _firstMoveDuration);
        if (moveTween != null)
        {
            _sequence.Append(moveTween);
        }
        else
        {
            _sequence.AppendInterval(_firstMoveDuration);
        }

        _sequence.OnComplete(CompleteReverseTransition);
    }

    private void AppendMoveToTarget(Sequence sequence, Transform part, Transform target, float duration)
    {
        Tween moveTween = BuildMoveTween(part, target, duration);
        if (moveTween != null)
        {
            sequence.Append(moveTween);
        }
        else
        {
            sequence.AppendInterval(duration);
        }
    }

    private Tween BuildMoveTween(Transform part, Transform target, float duration)
    {
        if (part == null || target == null)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(part.DOMove(target.position, duration).SetEase(_moveEase));
        moveSequence.Join(part.DORotateQuaternion(target.rotation, duration).SetEase(_moveEase));
        moveSequence.Join(part.DOScale(target.localScale, duration).SetEase(_moveEase));

        return moveSequence;
    }

    private Tween BuildMoveTweenToCachedPose(Transform part, float duration)
    {
        if (part == null)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(part.DOLocalMove(_cachedPartLocalPosition, duration).SetEase(_moveEase));
        moveSequence.Join(part.DOLocalRotateQuaternion(_cachedPartLocalRotation, duration).SetEase(_moveEase));
        moveSequence.Join(part.DOScale(_cachedPartLocalScale, duration).SetEase(_moveEase));
        return moveSequence;
    }

    private void CompleteForwardTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(1f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        NotifyPartTransitionCompleted(isReverse: false);
    }

    private void CompleteReverseTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(0f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(true);
        }

        NotifyPartTransitionCompleted(isReverse: true);
    }

    private void NotifyPartTransitionCompleted(bool isReverse)
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        string partName = _lastPartName ?? string.Empty;
        if (isReverse)
        {
            em.TriggerVehicleToPartTransitionReverseCompleted(partName);
        }
        else
        {
            em.TriggerVehicleToPartTransitionCompleted(partName);
        }
    }

    /// <summary>开局记录列表中所有零件的本地位姿与显隐状态。</summary>
    private void CacheAllPartInitialStates()
    {
        _partInitialStates.Clear();
        if (_partRoots == null)
        {
            return;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            Transform part = _partRoots[i];
            if (part == null)
            {
                continue;
            }

            part.gameObject.SetActive(false);

            _partInitialStates[part.GetInstanceID()] = new PartInitialState
            {
                LocalPosition = part.localPosition,
                LocalRotation = part.localRotation,
                LocalScale = part.localScale,
                IsActive = part.gameObject.activeSelf
            };
        }
    }

    /// <summary>将列表内零件立即还原为开局缓存的本地 Transform 与显隐状态。</summary>
    private void RestoreAllPartsToInitialState()
    {
        if (_partRoots == null)
        {
            return;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            Transform part = _partRoots[i];
            if (part == null || !_partInitialStates.TryGetValue(part.GetInstanceID(), out PartInitialState state))
            {
                continue;
            }

            part.DOKill();
            part.localPosition = state.LocalPosition;
            part.localRotation = state.LocalRotation;
            part.localScale = state.LocalScale;
            part.gameObject.SetActive(state.IsActive);
        }
    }

    private bool TryGetPartInitialState(Transform part, out PartInitialState state)
    {
        state = default;
        if (part == null)
        {
            return false;
        }

        return _partInitialStates.TryGetValue(part.GetInstanceID(), out state);
    }

    private void ApplyCachedPoseFromInitialState(Transform part)
    {
        if (!TryGetPartInitialState(part, out PartInitialState state))
        {
            ApplyCachedPoseFromCurrentTransform(part);
            return;
        }

        _cachedPartLocalPosition = state.LocalPosition;
        _cachedPartLocalRotation = state.LocalRotation;
        _cachedPartLocalScale = state.LocalScale;
    }

    private void ApplyCachedPoseFromCurrentTransform(Transform part)
    {
        if (part == null)
        {
            return;
        }

        _cachedPartLocalPosition = part.localPosition;
        _cachedPartLocalRotation = part.localRotation;
        _cachedPartLocalScale = part.localScale;
    }

    private Transform FindFirstValidPart()
    {
        for (int i = 0; i < _partRoots.Count; i++)
        {
            if (_partRoots[i] != null)
            {
                return _partRoots[i];
            }
        }

        return null;
    }

    #endregion

    #region 车辆 ↔ 攻击路径过渡

    /// <summary>配置用于显示攻击路径的路点 Transform 列表（忽略 null）；配置后默认隐藏路点物体。</summary>
    public void ConfigureShowAttackPath(IReadOnlyList<Transform> waypoints)
    {
        _showAttackPath.Clear();
        if (waypoints == null)
        {
            return;
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            Transform waypoint = waypoints[i];
            if (waypoint != null)
            {
                _showAttackPath.Add(waypoint);
            }
        }

        SetShowAttackPathWaypointsActive(false);
    }

    /// <summary>清空攻击路径路点列表并隐藏原路点物体。</summary>
    public void ClearShowAttackPath()
    {
        SetShowAttackPathWaypointsActive(false);
        _showAttackPath.Clear();
    }

    /// <summary>
    /// 车辆 → 攻击路径：过渡开始时显示路点标记，KJ_Car 溶解隐藏后播放攻击路径。
    /// </summary>
    public bool PlayVehicleToAttackPathTransition()
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryValidateAttackPathTransition(out AttackPathController controller))
        {
            return false;
        }

        ResolveKjCarReference();
        if (_kjCarRoot == null)
        {
            Debug.LogError("[VehicleToAttackPath] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        if (_kjDissolve.MaterialCount == 0)
        {
            Debug.LogWarning("[VehicleToAttackPath] KJ_Car 未找到带 _DissolveAmount 的材质。");
        }

        KillSequence();
        RestoreAllPartsToInitialState();
        HideAttackPathControllerImmediate();
        SetShowAttackPathWaypointsActive(true);
        _activePart = null;
        CacheAttackPathCameraPose();

        _isTransitioning = true;
        EventManager.Instance?.TriggerVehicleToAttackPathTransitionStarted();
        _sequence = DOTween.Sequence();
        AppendCarHideOnlyTween(_sequence);
        JoinAttackPathCameraToTargetTween(_sequence);
        _sequence.OnComplete(() => CompleteVehicleToAttackPathTransition(controller));
        return true;
    }

    /// <summary>
    /// 攻击路径 → 车辆：停止路径动画并隐藏攻击路径，KJ_Car 溶解显现。
    /// </summary>
    public bool PlayAttackPathToVehicleTransition()
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveKjCarReference();
        if (_kjCarRoot == null)
        {
            Debug.LogError("[AttackPathToVehicle] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        KillSequence();
        StopAndHideAttackPath();
        RestoreAllPartsToInitialState();
        ResetCarDragRotation();
        _activePart = null;

        _isTransitioning = true;
        EventManager.Instance?.TriggerAttackPathToVehicleTransitionStarted();
        _kjCarRoot.SetActive(true);
        _kjDissolve.SetDissolveAmount(1f);

        _sequence = DOTween.Sequence();
        AppendCarAppearOnlyTween(_sequence);
        JoinAttackPathCameraRestoreTween(_sequence);
        _sequence.OnComplete(CompleteAttackPathToVehicleTransition);
        return true;
    }

    private bool TryValidateAttackPathTransition(out AttackPathController controller)
    {
        controller = ResolveAttackPathController();
        if (controller == null)
        {
            Debug.LogError("[VehicleToAttackPath] 未配置 AttackPathController。");
            return false;
        }

        int validWaypointCount = CountValidWaypoints(_showAttackPath);
        if (validWaypointCount < 2)
        {
            Debug.LogError("[VehicleToAttackPath] showAttackPath 至少需要 2 个有效路点。");
            return false;
        }

        return true;
    }

    private static int CountValidWaypoints(IReadOnlyList<Transform> waypoints)
    {
        if (waypoints == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>仅播放 KJ_Car 溶解隐藏（与 PlayTransition 前半段车辆部分一致）。</summary>
    private void AppendCarHideOnlyTween(Sequence sequence)
    {
        Tween dissolveTween = BuildKjDissolveHideTween();
        if (dissolveTween != null)
        {
            sequence.Append(dissolveTween);
        }
        else
        {
            sequence.AppendInterval(_kjDissolveDuration);
        }
    }

    /// <summary>仅播放 KJ_Car 溶解显现。</summary>
    private void AppendCarAppearOnlyTween(Sequence sequence)
    {
        Tween appearTween = BuildKjDissolveAppearTween();
        if (appearTween != null)
        {
            sequence.Append(appearTween);
        }
        else
        {
            sequence.AppendInterval(_kjDissolveDuration);
        }
    }

    private void CompleteVehicleToAttackPathTransition(AttackPathController controller)
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(1f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        if (controller == null)
        {
            return;
        }

        controller.gameObject.SetActive(true);
        controller.PlayPath(_showAttackPath);
        EventManager.Instance?.TriggerVehicleToAttackPathTransitionCompleted();
    }

    private void CompleteAttackPathToVehicleTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(0f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(true);
        }

        EventManager.Instance?.TriggerAttackPathToVehicleTransitionCompleted();
    }

    private AttackPathController ResolveAttackPathController()
    {
        if (_attackPathController != null)
        {
            return _attackPathController;
        }

        _attackPathController = FindFirstObjectByType<AttackPathController>();
        return _attackPathController;
    }

    private void StopAndHideAttackPath()
    {
        SetShowAttackPathWaypointsActive(false);
        HideAttackPathControllerImmediate();

    }



    /// <summary>仅停止并隐藏攻击路径控制器物体，不影响路点标记显隐。</summary>
    private void HideAttackPathControllerImmediate()
    {
        AttackPathController controller = ResolveAttackPathController();
        if (controller == null)
        {
            return;
        }

        controller.StopPath();
        if (controller.gameObject.activeSelf)
        {
            controller.gameObject.SetActive(false);
        }
    }

    /// <summary>统一设置 _showAttackPath 中路点物体的显隐。</summary>
    private void SetShowAttackPathWaypointsActive(bool active)
    {
        if (_showAttackPath == null)
        {
            return;
        }

        for (int i = 0; i < _showAttackPath.Count; i++)
        {
            Transform waypoint = _showAttackPath[i];
            if (waypoint != null)
            {
                waypoint.gameObject.SetActive(active);
            }
        }
    }

    /// <summary>过渡开始前缓存相机世界位姿，供攻击路径 → 车辆时还原。</summary>
    private void CacheAttackPathCameraPose()
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null)
        {
            _hasCachedAttackPathCameraPose = false;
            return;
        }

        _cachedAttackPathCameraPosition = camera.position;
        _cachedAttackPathCameraRotation = camera.rotation;
        _hasCachedAttackPathCameraPose = true;
    }

    private Transform ResolveAttackPathCamera()
    {
        if (_attackPathCamera != null)
        {
            return _attackPathCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _attackPathCamera = mainCamera.transform;
        }

        return _attackPathCamera;
    }

    /// <summary>与溶解隐藏并行：相机移动到攻击路径目标点位。</summary>
    private void JoinAttackPathCameraToTargetTween(Sequence sequence)
    {
        Tween cameraTween = BuildAttackPathCameraMoveTween(_attackPathCameraTarget);
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    /// <summary>与溶解显现并行：相机还原到过渡前缓存位姿。</summary>
    private void JoinAttackPathCameraRestoreTween(Sequence sequence)
    {
        Tween cameraTween = BuildAttackPathCameraRestoreTween();
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    private Tween BuildAttackPathCameraMoveTween(Transform target)
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || target == null)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(target.position, _attackPathCameraMoveDuration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(target.rotation, _attackPathCameraMoveDuration).SetEase(_attackPathCameraEase));
        return moveSequence;
    }

    private Tween BuildAttackPathCameraRestoreTween()
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || !_hasCachedAttackPathCameraPose)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(_cachedAttackPathCameraPosition, _attackPathCameraMoveDuration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(_cachedAttackPathCameraRotation, _attackPathCameraMoveDuration).SetEase(_attackPathCameraEase));
        return moveSequence;
    }

    #endregion

    #region 共享：KJ_Car 溶解与序列

    /// <summary>与当前 Sequence 最后一段并行：KJ_Car 溶解隐藏（0→1）。</summary>
    private void JoinCarHideTween(Sequence sequence)
    {
        Tween dissolveTween = BuildKjDissolveHideTween();
        if (dissolveTween != null)
        {
            sequence.Join(dissolveTween);
        }
    }

    /// <summary>与当前 Sequence 最后一段并行：KJ_Car 溶解显现（1→0）。</summary>
    private void JoinCarAppearTween(Sequence sequence)
    {
        Tween kjAppearTween = BuildKjDissolveAppearTween();
        if (kjAppearTween != null)
        {
            sequence.Join(kjAppearTween);
        }
    }

    private Tween BuildKjDissolveHideTween()
    {
        if (_kjDissolve.MaterialCount == 0)
        {
            return null;
        }

        _kjCarRoot.SetActive(true);
        _kjDissolve.SetDissolveAmount(0f);

        float dissolveAmount = 0f;
        return DOTween.To(() => dissolveAmount, value =>
        {
            dissolveAmount = value;
            _kjDissolve.SetDissolveAmount(value);
        }, 1f, _kjDissolveDuration).SetEase(_kjDissolveEase);
    }

    private Tween BuildKjDissolveAppearTween()
    {
        if (_kjDissolve.MaterialCount == 0)
        {
            return null;
        }

        float dissolveAmount = 1f;
        return DOTween.To(() => dissolveAmount, value =>
        {
            dissolveAmount = value;
            _kjDissolve.SetDissolveAmount(value);
        }, 0f, _kjDissolveDuration).SetEase(_kjDissolveEase);
    }

    private void PrepareKjCarDissolve()
    {
        // 使用材质实例，避免修改 sharedMaterial 污染 Part_CarLine 等资源
        _kjDissolve.CollectFrom(_kjCarRoot, isShareMaterial: false);
        _kjDissolve.SetDissolveNoiseScale(_dissolveNoiseScale);
        _kjDissolve.SetDissolveAmount(0f);
    }

    private void ResetCarDragRotation()
    {
        ResolveCarDragYawRotate();
        if (_carDragYawRotate != null)
        {
            _carDragYawRotate.ResetRotation();
        }
    }

    private void ResolveCarDragYawRotate()
    {
        if (_carDragYawRotate != null)
        {
            return;
        }

        Transform carRoot = FindCarRootTransform();
        if (carRoot != null)
        {
            _carDragYawRotate = carRoot.GetComponent<MouseDragYawRotate>();
            if (_carDragYawRotate == null)
            {
                _carDragYawRotate = carRoot.GetComponentInChildren<MouseDragYawRotate>(true);
            }
        }

        if (_carDragYawRotate != null)
        {
            return;
        }

        CarModelController carModelController = FindFirstObjectByType<CarModelController>();
        if (carModelController != null)
        {
            _carDragYawRotate = carModelController.carModelRotateController;
        }
    }

    private void ResolveKjCarReference()
    {
        if (_kjCarRoot != null)
        {
            return;
        }

        Transform carRoot = FindCarRootTransform();
        if (carRoot == null)
        {
            return;
        }

        Transform kj = carRoot.Find("KJ_Car");
        if (kj != null)
        {
            _kjCarRoot = kj.gameObject;
        }
    }

    private static Transform FindCarRootTransform()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform car = all[i];
            if (car == null || car.name != "Car")
            {
                continue;
            }

            if (car.Find("KJ_Car") != null)
            {
                return car;
            }
        }

        return null;
    }

    private void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
        }

        Transform camera = ResolveAttackPathCamera();
        if (camera != null)
        {
            camera.DOKill();
        }

        _sequence = null;
        _isTransitioning = false;
    }

    #endregion

    #region 编辑器

#if UNITY_EDITOR
    [ContextMenu("重新缓存零件开局位姿")]
    private void EditorRecachePartInitialStates()
    {
        CacheAllPartInitialStates();
    }

    [ContextMenu("测试：正播（列表第一个零件）")]
    private void EditorPlayFirstPartTransition()
    {
        PlayTransition();
    }

    [ContextMenu("测试：倒播（上次正播零件）")]
    private void EditorPlayReverseTransition()
    {
        PlayTransitionReverse();
    }

    [ContextMenu("测试：车辆 → 攻击路径")]
    private void EditorPlayVehicleToAttackPathTransition()
    {
        PlayVehicleToAttackPathTransition();
    }

    [ContextMenu("测试：攻击路径 → 车辆")]
    private void EditorPlayAttackPathToVehicleTransition()
    {
        PlayAttackPathToVehicleTransition();
    }
#endif

    #endregion
}
