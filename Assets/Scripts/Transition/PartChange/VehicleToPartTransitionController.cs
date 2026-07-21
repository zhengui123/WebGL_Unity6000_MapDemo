using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 车辆 ↔ 零件、车辆 ↔ 攻击路径 过渡控制器。
/// 零件过渡：零件移动 + KJ_Car 溶解显隐（KJ_Car 引用统一由 <see cref="CarModelDissolveController"/> 配置）。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionController : MonoBehaviour
{
    #region 字段与属性

    [System.Serializable]
    public struct PartBindingData
    {
        [Tooltip("业务零部件ID；留空时运行时自动回填为 partRoot.name")]
        public string partId;
        public Transform partRoot;
    }

    [Header("零件列表（按 partId 查找；ID 为空时自动使用 GameObject 名称）")]
    [SerializeField] private List<PartBindingData> _partRoots = new List<PartBindingData>();

    [Header("目标位姿")]
    [SerializeField] private Transform _firstTarget;
    [SerializeField] private Transform _secondTarget;

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
    [Tooltip("零件显隐数据源（LineBindings.start3D）；留空则运行时查找")]
    [SerializeField] private GridLine _gridLine;

    [Tooltip("零部件名称文本生成器；留空则运行时查找")]
    [SerializeField] private PartNameLabelGenerator _partNameLabelGenerator;
    [SerializeField] private Transform _attackPathCamera;
    [Tooltip("车辆 → 攻击路径时相机移动到的目标位姿（世界坐标）")]
    [SerializeField] private Transform _attackPathCameraTarget;
    [SerializeField] private float _attackPathCameraMoveDuration = 1.2f;
    [SerializeField] private Ease _attackPathCameraEase = Ease.InOutQuad;
    [Tooltip("攻击路径 → 零件：镜头短恢复时长（与第一段零件动画并行）")]
    [SerializeField] private float _attackPathToPartCameraRestoreDuration = 0.6f;
    [Tooltip("攻击路径 → 零件：第二段零件动画时镜头跟随目标位姿；留空则第二段不再移动镜头")]
    [SerializeField] private Transform _partCameraFollowTarget;

    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();
    private GameObject _kjCarRoot;
    private readonly Dictionary<int, PartInitialState> _partInitialStates = new Dictionary<int, PartInitialState>();
    private Sequence _sequence;
    private bool _isTransitioning;
    private Transform _activePart;
    private string _lastPartName;
    private string _lastPartId;
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
    public string LastPartId => _lastPartId;
    public IReadOnlyList<PartBindingData> ConfiguredPartRoots => _partRoots;

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
        NormalizePartBindings();
        ResolveKjCarReference();
        ResolveAttackPathController();
        CacheAllPartInitialStates();
        StopAndHideAttackPath();
        ShowPartsByNames(null);
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
    /// <param name="partId">业务零部件 ID；null 或空字符串时使用列表第一项。</param>
    public bool PlayTransition(string partId = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryResolvePart(partId, out Transform part))
        {
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            Debug.LogError("[VehicleToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        string normalizedPartId = NormalizePartId(partId, part);
        if (IsPartLevel() && _activePart == part)
        {
            _lastPartName = part.name;
            _lastPartId = normalizedPartId;
            return true;
        }

        if (ShouldPlayPartToPartTransition(part))
        {
            return PlayPartToPartTransition(part, normalizedPartId);
        }

        if (IsAttackPathLevel())
        {
            return PlayAttackPathToPartTransition(part, normalizedPartId);
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
        _lastPartId = normalizedPartId;
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
    /// <param name="partId">业务零部件 ID；为空时使用上次正播记录的零件 ID。</param>
    public bool PlayTransitionReverse(string partId = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (string.IsNullOrEmpty(partId))
        {
            partId = _lastPartId;
        }

        if (string.IsNullOrEmpty(partId) || !TryResolvePart(partId, out Transform part))
        {
            Debug.LogError("[VehicleToPart] 倒播失败：未指定零件ID或找不到对应零件。");
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
        _lastPartId = ResolvePartIdByTransform(part);
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

    /// <summary>按 partId 在缓存列表中查找零件；partId 为空时返回第一个有效项。</summary>
    public bool TryResolvePart(string partId, out Transform part)
    {
        part = null;
        if (_partRoots == null || _partRoots.Count == 0)
        {
            Debug.LogError("[VehicleToPart] 零件列表为空，请在 Inspector 中挂载。");
            return false;
        }

        if (string.IsNullOrEmpty(partId))
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
            PartBindingData binding = _partRoots[i];
            Transform candidate = binding.partRoot;
            string candidateId = NormalizePartId(binding.partId, candidate);
            if (candidate != null && candidateId == partId)
            {
                part = candidate;
                return true;
            }
        }

        Debug.LogError($"[VehicleToPart] 未在列表中找到 id 为「{partId}」的零件。");
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

    #region 零件→零件切换（旧零件倒播 + 新零件正播）

    /// <summary>
    /// 判定是否走零件→零件切换：
    /// 当前必须在 PartLevel，且已有激活零件，且目标零件与当前零件不同。
    /// </summary>
    private bool ShouldPlayPartToPartTransition(Transform targetPart)
    {
        if (targetPart == null || _activePart == null)
        {
            return false;
        }

        if (_activePart == targetPart)
        {
            return false;
        }

        return IsPartLevel();
    }

    /// <summary>
    /// 执行零件→零件切换（严格串行）：
    /// 1) 旧零件 second->first->初始位姿（等价倒播）
    /// 2) 新零件 初始位姿->first->second（正播）
    /// </summary>
    private bool PlayPartToPartTransition(Transform targetPart, string targetPartId)
    {
        if (_activePart == null)
        {
            return false;
        }

        Transform oldPart = _activePart;
        ApplyCachedPoseFromInitialState(oldPart);
        Vector3 oldInitialPosition = _cachedPartLocalPosition;
        Quaternion oldInitialRotation = _cachedPartLocalRotation;
        Vector3 oldInitialScale = _cachedPartLocalScale;

        if (TryGetPartInitialState(targetPart, out PartInitialState targetInitialState))
        {
            targetPart.localPosition = targetInitialState.LocalPosition;
            targetPart.localRotation = targetInitialState.LocalRotation;
            targetPart.localScale = targetInitialState.LocalScale;
        }

        KillSequence();
        StopAndHideAttackPath();

        // StopAndHideAttackPath 只停攻击路径线，不影响零件显隐；此处再显式激活切换双方。
        oldPart.gameObject.SetActive(true);
        targetPart.gameObject.SetActive(false);

        _isTransitioning = true;
        _activePart = targetPart;
        _lastPartName = targetPart.name;
        _lastPartId = targetPartId;
        EventManager.Instance?.TriggerPartToPartTransitionStarted(_lastPartName, _lastPartId);

        _sequence = DOTween.Sequence();
        AppendMoveToTarget(_sequence, oldPart, _firstTarget, _secondMoveDuration);
        AppendMoveToPose(_sequence, oldPart, oldInitialPosition, oldInitialRotation, oldInitialScale, _firstMoveDuration);
        _sequence.AppendCallback(() =>
        {
            oldPart.gameObject.SetActive(false);
            targetPart.gameObject.SetActive(true);
        });
        AppendMoveToTarget(_sequence, targetPart, _firstTarget, _firstMoveDuration);
        AppendMoveToTarget(_sequence, targetPart, _secondTarget, _secondMoveDuration);
        _sequence.OnComplete(CompletePartToPartTransition);
        return true;
    }

    private void CompletePartToPartTransition()
    {
        _isTransitioning = false;
        EventManager.Instance?.TriggerPartToPartTransitionCompleted(_lastPartName ?? string.Empty, _lastPartId ?? string.Empty);
    }

    #endregion

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

    private void AppendMoveToPose(
        Sequence sequence,
        Transform part,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        float duration)
    {
        Tween moveTween = BuildMoveTweenToPose(part, localPosition, localRotation, localScale, duration);
        if (moveTween != null)
        {
            sequence.Append(moveTween);
        }
        else
        {
            sequence.AppendInterval(duration);
        }
    }

    private Tween BuildMoveTweenToPose(
        Transform part,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        float duration)
    {
        if (part == null)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(part.DOLocalMove(localPosition, duration).SetEase(_moveEase));
        moveSequence.Join(part.DOLocalRotateQuaternion(localRotation, duration).SetEase(_moveEase));
        moveSequence.Join(part.DOScale(localScale, duration).SetEase(_moveEase));
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
            Transform part = _partRoots[i].partRoot;
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
            Transform part = _partRoots[i].partRoot;
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
            Transform part = _partRoots[i].partRoot;
            if (part != null)
            {
                return part;
            }
        }

        return null;
    }

    private static string NormalizePartId(string partId, Transform part)
    {
        if (!string.IsNullOrWhiteSpace(partId))
        {
            return partId.Trim();
        }

        return part != null ? part.name : string.Empty;
    }

    private string ResolvePartIdByTransform(Transform part)
    {
        if (part == null || _partRoots == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == part)
            {
                return NormalizePartId(binding.partId, part);
            }
        }

        return part.name;
    }

    private void NormalizePartBindings()
    {
        if (_partRoots == null)
        {
            return;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.partId))
            {
                binding.partId = binding.partRoot.name;
                _partRoots[i] = binding;
            }
        }
    }

    private static bool IsPartLevel()
    {
        GameManager manager = GameManager.Instance;
        return manager != null && manager.CurrentState == GameManager.ControlState.PartLevel;
    }

    private static bool IsAttackPathLevel()
    {
        GameManager manager = GameManager.Instance;
        return manager != null && manager.CurrentState == GameManager.ControlState.AttackPathLevel;
    }

    #endregion

    #region 车辆 ↔ 攻击路径过渡

    /// <summary>
    /// 按零件名显示 GridLine.LineBindings 中的 start3D。
    /// partNames 为 null 或空：显示全部；否则仅显示名单内零件。
    /// </summary>
    public void ShowPartsByNames(IReadOnlyList<string> partNames)
    {
        GridLine gridLine = ResolveGridLine();
        if (gridLine == null)
        {
            Debug.LogWarning("[VehicleToPart] 未找到 GridLine，无法设置零部件显隐。");
            return;
        }

        gridLine.SetPartTransformsVisible(partNames);
    }

    /// <summary>显示 GridLine 中全部零部件。</summary>
    public void ShowAllParts()
    {
        ShowPartsByNames(null);
    }

    /// <summary>
    /// 攻击路径 → 零件：先隐藏攻击路径，再播放“车辆 → 零件”两段零件动画（不经过车辆界面）。
    /// </summary>
    /// <param name="partId">业务零部件 ID；null 或空字符串时使用列表第一项。</param>
    public bool PlayAttackPathToPartTransition(string partId = null)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryResolvePart(partId, out Transform part))
        {
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            Debug.LogError("[AttackPathToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        string normalizedPartId = NormalizePartId(partId, part);
        return PlayAttackPathToPartTransition(part, normalizedPartId);
    }

    /// <summary>
    /// 车辆 → 攻击路径：KJ_Car 溶解隐藏后加载攻击链路连线，并按 nodes 零件名控制显隐。
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
        ApplyAttackPathPartsVisibilityFromCache();
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
    /// 攻击路径 → 车辆：停止路径动画并隐藏攻击路径，显示全部零部件，KJ_Car 溶解显现。
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
        ShowAllParts();
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

        return true;
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

        ApplyAttackPathPartsVisibilityFromCache();

        if (controller != null)
        {
            controller.gameObject.SetActive(true);
            CarVehicleDataController dataController = CarVehicleDataController.Instance;
            if (dataController == null || !dataController.ApplyAttackPathsFromCacheForTransition())
            {
                Debug.LogWarning("[VehicleToAttackPath] 无攻击链路缓存或加载失败，跳过连线绘制。");
            }
        }

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

    private bool PlayAttackPathToPartTransition(Transform targetPart, string targetPartId)
    {
        if (targetPart == null)
        {
            return false;
        }

        KillSequence();
        StopAndHideAttackPath();
        RestoreAllPartsToInitialState();
        ApplyCachedPoseFromInitialState(targetPart);
        targetPart.gameObject.SetActive(true);
        _activePart = targetPart;
        _lastPartName = targetPart.name;
        _lastPartId = targetPartId;

        ResolveKjCarReference();
        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        _isTransitioning = true;
        EventManager.Instance?.TriggerAttackPathToPartTransitionStarted(_lastPartName, _lastPartId);

        _sequence = DOTween.Sequence();
        // 第一段零件动画 + 镜头短恢复（并行）
        AppendMoveToTarget(_sequence, targetPart, _firstTarget, _firstMoveDuration);
        JoinAttackPathCameraShortRestoreTween(_sequence);
        // 第二段零件动画 + 镜头跟随（并行）
        AppendMoveToTarget(_sequence, targetPart, _secondTarget, _secondMoveDuration);
        JoinPartCameraFollowTween(_sequence);
        _sequence.OnComplete(CompleteAttackPathToPartTransition);
        return true;
    }

    private void CompleteAttackPathToPartTransition()
    {
        _isTransitioning = false;
        EventManager.Instance?.TriggerAttackPathToPartTransitionCompleted(_lastPartName ?? string.Empty, _lastPartId ?? string.Empty);
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

    private GridLine ResolveGridLine()
    {
        if (_gridLine == null)
        {
            _gridLine = FindFirstObjectByType<GridLine>(FindObjectsInactive.Include);
        }

        return _gridLine;
    }

    /// <summary>进入攻击路径级：按 AttackChainData.nodes 零件名显隐；无名单则显示全部。</summary>
    private void ApplyAttackPathPartsVisibilityFromCache()
    {
        List<string> partNames = CarVehicleDataStore.Instance.BuildAttackChainNodePartNames();
        ShowPartsByNames(partNames);
        // 仅攻击链路级显示零件名；隐藏零件上文字由零件 SetActive 自然带掉
        SetPartNameLabelsVisible(true);
    }

    private void StopAndHideAttackPath()
    {
        HideAttackPathControllerImmediate();
        SetPartNameLabelsVisible(false);
    }

    /// <summary>开关零部件名称文字（零件级/车辆级隐藏，攻击链路级显示）。</summary>
    public void SetPartNameLabelsVisible(bool visible)
    {
        PartNameLabelGenerator generator = ResolvePartNameLabelGenerator();
        if (generator == null)
        {
            return;
        }

        generator.SetAllLabelsVisible(visible);
    }

    private PartNameLabelGenerator ResolvePartNameLabelGenerator()
    {
        if (_partNameLabelGenerator != null)
        {
            return _partNameLabelGenerator;
        }

        _partNameLabelGenerator = FindFirstObjectByType<PartNameLabelGenerator>(FindObjectsInactive.Include);
        return _partNameLabelGenerator;
    }

    /// <summary>仅停止并隐藏攻击路径控制器物体，不影响零部件显隐。</summary>
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
        Tween cameraTween = BuildAttackPathCameraMoveTween(_attackPathCameraTarget, _attackPathCameraMoveDuration);
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    /// <summary>与溶解显现并行：相机还原到过渡前缓存位姿。</summary>
    private void JoinAttackPathCameraRestoreTween(Sequence sequence)
    {
        Tween cameraTween = BuildAttackPathCameraRestoreTween(_attackPathCameraMoveDuration);
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    /// <summary>与第一段零件动画并行：镜头短恢复到进入攻击路径前的缓存位姿。</summary>
    private void JoinAttackPathCameraShortRestoreTween(Sequence sequence)
    {
        Tween cameraTween = BuildAttackPathCameraRestoreTween(_attackPathToPartCameraRestoreDuration);
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    /// <summary>与第二段零件动画并行：镜头跟随移动到零件观察位姿。</summary>
    private void JoinPartCameraFollowTween(Sequence sequence)
    {
        Tween cameraTween = BuildAttackPathCameraMoveTween(_partCameraFollowTarget, _secondMoveDuration);
        if (cameraTween != null)
        {
            sequence.Join(cameraTween);
        }
    }

    private Tween BuildAttackPathCameraMoveTween(Transform target, float duration)
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || target == null)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(target.position, duration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(target.rotation, duration).SetEase(_attackPathCameraEase));
        return moveSequence;
    }

    private Tween BuildAttackPathCameraRestoreTween(float duration)
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || !_hasCachedAttackPathCameraPose)
        {
            return null;
        }

        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(_cachedAttackPathCameraPosition, duration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(_cachedAttackPathCameraRotation, duration).SetEase(_attackPathCameraEase));
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
        CarModelDissolveController carModelDissolve = CarModelDissolveController.Instance;
        _kjCarRoot = carModelDissolve != null ? carModelDissolve.KjCarRoot : null;
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
