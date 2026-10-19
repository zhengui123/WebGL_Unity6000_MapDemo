using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 车辆 ↔ 零件、车辆 ↔ 攻击路径、零件 ↔ 攻击路径 过渡控制器。
/// 零件过渡：零件移动 + KJ_Car 溶解显隐（KJ_Car 引用统一由 <see cref="CarModelDissolveController"/> 配置）。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionController : MonoBehaviour
{
    #region 字段与属性

    [System.Serializable]
    public struct PartBindingData
    {
        [Tooltip("零部件标号 1~5，与接口 partType 匹配模型")]
        public int partType;
        [Tooltip("显示名默认值；宿主 partId / 接口 partTypeName 会覆盖显示，留空则用 partRoot.name")]
        public string partId;
        public Transform partRoot;
        [Tooltip("攻击链路级该零件的目标位姿（位置/旋转/缩放）；留空则攻击链路过渡中不做该零件位移缩放")]
        public Transform attackPathTarget;
    }

    [Header("零件列表（按 partType 1~5 匹配模型；partId 为显示名）")]
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

    [Header("攻击路径（车辆/零件 ↔ 攻击路径过渡）")]
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
    [Tooltip("攻击链路显示零件移动到各自 attackPathTarget（或收回开局位姿）的时长")]
    [SerializeField] private float _attackPathPartMoveDuration = 1.2f;
    [Tooltip("攻击路径 → 零件：镜头短恢复时长（与第一段零件动画并行）")]
    [SerializeField] private float _attackPathToPartCameraRestoreDuration = 0.6f;
    [Tooltip("攻击路径 → 零件：第二段零件动画时镜头跟随目标位姿；留空则第二段不再移动镜头")]
    [SerializeField] private Transform _partCameraFollowTarget;

    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();
    private GameObject _kjCarRoot;
    private readonly Dictionary<int, PartInitialState> _partInitialStates = new Dictionary<int, PartInitialState>();
    private Sequence _sequence;
    private bool _isTransitioning;
    /// <summary>打断后下一次零件切换跳过旧零件收回，直接播新零件出现。</summary>
    private bool _skipOldPartRetractOnNextPartSwitch;
    private Transform _activePart;
    private string _lastPartName;
    private string _lastPartId;
    private int _lastPartType;
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
    public int LastPartType => _lastPartType;
    public IReadOnlyList<PartBindingData> ConfiguredPartRoots => _partRoots;

    /// <summary>若车辆/零件/攻击路径过渡正在播放，则立即完成并执行完成回调。</summary>
    public void CompleteCurrentTransitionImmediate()
    {
        if (_sequence == null || !_sequence.IsActive())
        {
            return;
        }

        _sequence.Complete(withCallbacks: true);
    }

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
        // 开局即隐藏零部件模型（车辆级也不展示）
        EnsureVehicleLevelVisualsHidden();
    }

    private void OnEnable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionCompleted += HandleEnteredVehicleLevelHideParts;
    }

    private void OnDisable()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionCompleted -= HandleEnteredVehicleLevelHideParts;
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void HandleEnteredVehicleLevelHideParts(string _)
    {
        EnsureVehicleLevelVisualsHidden();
    }

    #endregion

    #region 车辆 ↔ 零件过渡

    /// <summary>
    /// 播放车辆 → 零件过渡。
    /// </summary>
    /// <param name="partId">显示名（宿主 partId = partTypeName）；非空才覆盖该 partType 显示名。</param>
    /// <param name="partType">模型标号 1~5；&lt;=0 时回退用 partId/名称匹配（兼容旧调用）。</param>
    public bool PlayTransition(string partId = null, int partType = 0)
    {
        if (!TryResolvePartForTransition(partType, partId, out Transform part, out int resolvedType))
        {
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            LogManager.LogFeatureError("[VehicleToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        // 前端：partId 非空才改显示名；空/不填则沿用绑定原名
        string displayName = ResolveAndMaybeApplyDisplayName(resolvedType, part, partId);

        // 零件级快速切换：A→B 过渡中再切 C 时打断当前动画，立刻开切 C（跳过 B 收回）。
        if (_isTransitioning && IsPartLevel())
        {
            InterruptInFlightPartTransitionForRetarget();
        }
        else if (_isTransitioning)
        {
            return false;
        }

        if (IsPartLevel() && _activePart == part)
        {
            // 同 partType 再进：打断后可能已全部隐藏，须重新显示并贴到最终展示位，避免零件消失。
            _skipOldPartRetractOnNextPartSwitch = false;
            _isTransitioning = false;
            EnsurePartVisibleAtSecondTarget(part);
            _lastPartName = part.name;
            _lastPartId = displayName;
            _lastPartType = resolvedType;
            LogManager.LogFeature(
                $"[VehicleToPart] 同零件再进：已确保显示并贴第二目标 | part={part.name} | type={resolvedType}");
            EventManager.Instance?.TriggerPartToPartTransitionCompleted(
                _lastPartName,
                _lastPartId);
            return true;
        }

        if (ShouldPlayPartToPartTransition(part))
        {
            // A→B 中途改切 C：skipOldRetract=true，直接从 C 出现（不播 B 收回）。
            bool skipOldRetract = _skipOldPartRetractOnNextPartSwitch;
            _skipOldPartRetractOnNextPartSwitch = false;
            return PlayPartToPartTransition(part, displayName, skipOldRetract, resolvedType);
        }

        _skipOldPartRetractOnNextPartSwitch = false;

        if (IsAttackPathLevel())
        {
            return PlayAttackPathToPartTransition(part, displayName, resolvedType);
        }

        ResolveKjCarReference();

        if (_kjCarRoot == null)
        {
            LogManager.LogFeatureError("[VehicleToPart] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        if (_kjDissolve.MaterialCount == 0)
        {
            LogManager.LogFeatureWarning("[VehicleToPart] KJ_Car 未找到带 _DissolveAmount 的材质。");
        }

        KillSequence();
        StopAndHideAttackPath();
        RestoreAllPartsToInitialState();
        ApplyCachedPoseFromInitialState(part);

        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
        _lastPartId = displayName;
        _lastPartType = resolvedType;
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

        int reverseType = _lastPartType;
        if (!TryResolvePartForTransition(reverseType, partId, out Transform part, out int resolvedType))
        {
            LogManager.LogFeatureError("[VehicleToPart] 倒播失败：未指定零件或找不到对应零件。");
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            LogManager.LogFeatureError("[VehicleToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        if (!TryGetPartInitialState(part, out PartInitialState initialState))
        {
            LogManager.LogFeatureWarning("[VehicleToPart] 未找到零件开局位姿缓存，倒播终点将使用当前位姿。");
            ApplyCachedPoseFromCurrentTransform(part);
        }
        else
        {
            ApplyCachedPoseFromInitialState(part);
        }

        ResolveKjCarReference();
        if (_kjCarRoot == null)
        {
            LogManager.LogFeatureError("[VehicleToPart] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        KillSequence();
        StopAndHideAttackPath();
        ResetCarDragRotation();
        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
        // 倒播：名字非空才覆盖显示名，否则沿用绑定原名
        _lastPartId = ResolveAndMaybeApplyDisplayName(resolvedType, part, partId);
        _lastPartType = resolvedType;
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

    /// <summary>按 partType（1~5）匹配模型；失败返回 false。</summary>
    public bool TryResolvePartByType(int partType, out Transform part)
    {
        part = null;
        if (!PartTypeMatchUtil.IsValidPartType(partType))
        {
            return false;
        }

        if (_partRoots == null || _partRoots.Count == 0)
        {
            LogManager.LogFeatureError("[VehicleToPart] 零件列表为空，请在 Inspector 中挂载。");
            return false;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == null || binding.partType != partType)
            {
                continue;
            }

            part = binding.partRoot;
            return true;
        }

        LogManager.LogFeatureError($"[VehicleToPart] 未在列表中找到 partType={partType} 的零件。");
        return false;
    }

    /// <summary>
    /// 优先 partType 匹配；type 非法时回退 partId/名称（兼容旧宿主仅传显示名）。
    /// </summary>
    public bool TryResolvePartForTransition(
        int partType,
        string partId,
        out Transform part,
        out int resolvedType)
    {
        part = null;
        resolvedType = 0;

        if (PartTypeMatchUtil.IsValidPartType(partType) && TryResolvePartByType(partType, out part))
        {
            resolvedType = partType;
            return true;
        }

        if (!TryResolvePart(partId, out part))
        {
            return false;
        }

        resolvedType = ResolvePartTypeByTransform(part);
        return true;
    }

    /// <summary>按显示名/物体名查找零件；partId 为空时返回第一个有效项（兼容旧调用）。</summary>
    public bool TryResolvePart(string partId, out Transform part)
    {
        part = null;
        if (_partRoots == null || _partRoots.Count == 0)
        {
            LogManager.LogFeatureError("[VehicleToPart] 零件列表为空，请在 Inspector 中挂载。");
            return false;
        }

        if (string.IsNullOrEmpty(partId))
        {
            part = FindFirstValidPart();
            if (part == null)
            {
                LogManager.LogFeatureError("[VehicleToPart] 列表中没有有效的零件 Transform。");
            }

            return part != null;
        }

        string key = partId.Trim();
        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            Transform candidate = binding.partRoot;
            if (candidate == null)
            {
                continue;
            }

            string candidateId = NormalizePartId(binding.partId, candidate);
            if (string.Equals(candidateId, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.name, key, StringComparison.OrdinalIgnoreCase))
            {
                part = candidate;
                return true;
            }
        }

        LogManager.LogFeatureError($"[VehicleToPart] 未在列表中找到 id/名称为「{partId}」的零件。");
        return false;
    }

    /// <summary>partType → GridLine 键（优先 partRoot.name）。</summary>
    public bool TryResolveGridLineKeyByPartType(int partType, out string gridLineKey)
    {
        gridLineKey = null;
        if (!TryResolvePartByType(partType, out Transform part) || part == null)
        {
            return false;
        }

        gridLineKey = part.name;
        return !string.IsNullOrEmpty(gridLineKey);
    }

    /// <summary>将 partType 列表转为 GridLine 零件名列表。</summary>
    public List<string> ResolveGridLineKeysByPartTypes(IReadOnlyList<int> partTypes)
    {
        List<string> keys = new List<string>();
        if (partTypes == null || partTypes.Count == 0)
        {
            return keys;
        }

        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < partTypes.Count; i++)
        {
            if (!TryResolveGridLineKeyByPartType(partTypes[i], out string key)
                || string.IsNullOrEmpty(key)
                || !unique.Add(key))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys;
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
    /// 打断进行中的零件过渡（如 A→B 中途改切 C）：停动画、零件全部回初始隐藏；
    /// 下一次切换跳过旧零件收回，直接从新零件出现动画开始。
    /// </summary>
    private void InterruptInFlightPartTransitionForRetarget()
    {
        KillSequence();
        RestoreAllPartsToInitialState();
        _skipOldPartRetractOnNextPartSwitch = true;
        LogManager.LogFeature(
            $"[VehicleToPart] 已打断零件过渡，下一切换跳过收回直接出现 | from={_activePart?.name ?? "(空)"}");
    }

    /// <summary>
    /// 同零件再进 / 打断后仍切回当前件：强制显示并瞬时贴到第二目标（最终展示位）。
    /// </summary>
    private void EnsurePartVisibleAtSecondTarget(Transform part)
    {
        if (part == null)
        {
            return;
        }

        part.DOKill();
        part.gameObject.SetActive(true);
        if (_secondTarget != null)
        {
            part.position = _secondTarget.position;
            part.rotation = _secondTarget.rotation;
            part.localScale = _secondTarget.localScale;
        }
    }

    /// <summary>
    /// 执行零件→零件切换。
    /// 默认：旧零件收回 + 新零件出现；<paramref name="skipOldRetract"/> 为 true 时旧零件立即隐藏，只播新零件出现。
    /// </summary>
    private bool PlayPartToPartTransition(
        Transform targetPart,
        string targetPartId,
        bool skipOldRetract = false,
        int targetPartType = 0)
    {
        if (_activePart == null)
        {
            return false;
        }

        Transform oldPart = _activePart;

        if (TryGetPartInitialState(targetPart, out PartInitialState targetInitialState))
        {
            targetPart.localPosition = targetInitialState.LocalPosition;
            targetPart.localRotation = targetInitialState.LocalRotation;
            targetPart.localScale = targetInitialState.LocalScale;
        }

        KillSequence();
        StopAndHideAttackPath();

        _isTransitioning = true;
        _activePart = targetPart;
        _lastPartName = targetPart.name;
        _lastPartId = targetPartId;
        _lastPartType = PartTypeMatchUtil.IsValidPartType(targetPartType)
            ? targetPartType
            : ResolvePartTypeByTransform(targetPart);
        EventManager.Instance?.TriggerPartToPartTransitionStarted(_lastPartName, _lastPartId);

        _sequence = DOTween.Sequence();

        if (skipOldRetract)
        {
            // 打断重定向：旧零件立即藏起，只播新零件 初始→first→second。
            if (TryGetPartInitialState(oldPart, out PartInitialState oldInitial))
            {
                oldPart.localPosition = oldInitial.LocalPosition;
                oldPart.localRotation = oldInitial.LocalRotation;
                oldPart.localScale = oldInitial.LocalScale;
            }

            oldPart.gameObject.SetActive(false);
            targetPart.gameObject.SetActive(true);
            AppendMoveToTarget(_sequence, targetPart, _firstTarget, _firstMoveDuration);
            AppendMoveToTarget(_sequence, targetPart, _secondTarget, _secondMoveDuration);
            _sequence.OnComplete(CompletePartToPartTransition);
            LogManager.LogFeature(
                $"[VehicleToPart] 零件切换（跳过收回）| {oldPart.name} → {targetPart.name}");
            return true;
        }

        ApplyCachedPoseFromInitialState(oldPart);
        Vector3 oldInitialPosition = _cachedPartLocalPosition;
        Quaternion oldInitialRotation = _cachedPartLocalRotation;
        Vector3 oldInitialScale = _cachedPartLocalScale;

        // StopAndHideAttackPath 只停攻击路径线，不影响零件显隐；此处再显式激活切换双方。
        oldPart.gameObject.SetActive(true);
        targetPart.gameObject.SetActive(false);

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

        // 零件→车辆：确保攻击链路与零部件对象回到车辆级隐藏态。
        EnsureVehicleLevelVisualsHidden();
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

            // 先写重心网格再隐藏，避免 Prep.Awake 被跳过；首次显示时 fwidth 也不依赖「碰巧先跑」
            EcuWireframeMeshPrep[] preps = part.GetComponentsInChildren<EcuWireframeMeshPrep>(true);
            for (int p = 0; p < preps.Length; p++)
            {
                if (preps[p] != null)
                {
                    preps[p].Prepare();
                }
            }

            part.gameObject.SetActive(false);

            _partInitialStates[part.GetInstanceID()] = new PartInitialState
            {
                LocalPosition = part.localPosition,
                LocalRotation = part.localRotation,
                LocalScale = part.localScale,
                // 玩法开局统一隐藏（勿在 SetActive(false) 后再读 activeSelf）
                IsActive = false
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

    private int ResolvePartTypeByTransform(Transform part)
    {
        if (part == null || _partRoots == null)
        {
            return 0;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == part && PartTypeMatchUtil.IsValidPartType(binding.partType))
            {
                return binding.partType;
            }
        }

        return 0;
    }

    /// <summary>
    /// 入参名字非空：覆盖该 partType 显示名并返回新名；空：不改绑定，返回当前绑定显示名。
    /// </summary>
    private string ResolveAndMaybeApplyDisplayName(int partType, Transform part, string incomingName)
    {
        if (!string.IsNullOrWhiteSpace(incomingName))
        {
            string trimmed = incomingName.Trim();
            if (PartTypeMatchUtil.IsValidPartType(partType))
            {
                TrySetPartDisplayNameByType(partType, trimmed);
            }
            else
            {
                ApplyPartDisplayName(part, trimmed);
            }

            return trimmed;
        }

        return ResolvePartIdByTransform(part);
    }

    /// <summary>将显示名写入零件 3D 标签（不改 GameObject.name，避免打断 GridLine 键）。</summary>
    private void ApplyPartDisplayName(Transform part, string displayName)
    {
        if (part == null || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        PartNameLabelBillboard label = part.GetComponent<PartNameLabelBillboard>();
        if (label == null)
        {
            return;
        }

        label.SetLabelText(displayName.Trim());
    }

    private void SetBindingDisplayName(int partType, string displayName)
    {
        if (_partRoots == null || !PartTypeMatchUtil.IsValidPartType(partType) || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == null || binding.partType != partType)
            {
                continue;
            }

            binding.partId = displayName.Trim();
            _partRoots[i] = binding;
            return;
        }
    }

    private void ApplyDisplayNamesFromProtectionParts(PartProtectionStatusPart[] parts)
    {
        if (parts == null)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartProtectionStatusPart part = parts[i];
            if (part == null
                || string.IsNullOrWhiteSpace(part.partTypeName)
                || !PartTypeMatchUtil.TryNormalizePartType(part.partType, out int partType))
            {
                continue;
            }

            TrySetPartDisplayNameByType(partType, part.partTypeName);
        }
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

            bool changed = false;
            if (string.IsNullOrWhiteSpace(binding.partId))
            {
                binding.partId = binding.partRoot.name;
                changed = true;
            }

            // 未配标号时按列表顺序 1~6 补齐，便于场景迁移
            if (!PartTypeMatchUtil.IsValidPartType(binding.partType))
            {
                int fallback = i + 1;
                if (PartTypeMatchUtil.IsValidPartType(fallback))
                {
                    binding.partType = fallback;
                    changed = true;
                }
            }

            if (changed)
            {
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
            LogManager.LogFeatureWarning("[VehicleToPart] 未找到 GridLine，无法设置零部件显隐。");
            return;
        }

        gridLine.SetPartTransformsVisible(partNames);
    }

    /// <summary>按 partType 列表显示 GridLine 零件（内部转为 partRoot.name）。</summary>
    public void ShowPartsByPartTypes(IReadOnlyList<int> partTypes)
    {
        if (partTypes == null || partTypes.Count == 0)
        {
            ShowPartsByNames(null);
            return;
        }

        ShowPartsByNames(ResolveGridLineKeysByPartTypes(partTypes));
    }

    /// <summary>显示 GridLine 中全部零部件。</summary>
    public void ShowAllParts()
    {
        ShowPartsByNames(null);
    }

    /// <summary>
    /// 攻击路径 → 零件：先隐藏攻击路径，再播放“车辆 → 零件”两段零件动画（不经过车辆界面）。
    /// </summary>
    /// <param name="partId">显示名；非空才覆盖该 partType 显示名，空则沿用绑定原名。</param>
    /// <param name="partType">模型标号 1~5；&lt;=0 时回退显示名匹配。</param>
    public bool PlayAttackPathToPartTransition(string partId = null, int partType = 0)
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryResolvePartForTransition(partType, partId, out Transform part, out int resolvedType))
        {
            return false;
        }

        if (_firstTarget == null || _secondTarget == null)
        {
            LogManager.LogFeatureError("[AttackPathToPart] 未配置第一或第二目标 Transform。");
            return false;
        }

        string displayName = ResolveAndMaybeApplyDisplayName(resolvedType, part, partId);
        return PlayAttackPathToPartTransition(part, displayName, resolvedType);
    }

    /// <summary>
    /// 按 partType 覆盖显示名（写入绑定 partId + 3D 标签，不改 GameObject.name）。
    /// 名字为空/空白时不修改，返回 false。
    /// </summary>
    public bool TrySetPartDisplayNameByType(int partType, string partTypeName)
    {
        if (!PartTypeMatchUtil.IsValidPartType(partType) || string.IsNullOrWhiteSpace(partTypeName))
        {
            return false;
        }

        if (!TryResolvePartByType(partType, out Transform part) || part == null)
        {
            return false;
        }

        string trimmed = partTypeName.Trim();
        SetBindingDisplayName(partType, trimmed);
        ApplyPartDisplayName(part, trimmed);

        if (_activePart == part)
        {
            _lastPartId = trimmed;
            _lastPartType = partType;
        }

        return true;
    }

    /// <summary>
    /// 用车辆态势缓存刷显示名：先防护状态，再攻击链路 nodes（后到的覆盖先到的）；空名跳过。
    /// </summary>
    public void ApplyDisplayNamesFromVehicleDataStore(CarVehicleDataStore store = null)
    {
        store ??= CarVehicleDataStore.Instance;
        if (store == null)
        {
            return;
        }

        PartProtectionStatusData protection = store.PartProtectionStatus?.data;
        ApplyDisplayNamesFromProtectionParts(protection?.unprotectedParts);
        ApplyDisplayNamesFromProtectionParts(protection?.protectedParts);

        AttackChainNode[] nodes = store.AttackChain?.data?.nodes;
        if (nodes == null)
        {
            return;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            AttackChainNode node = nodes[i];
            if (node == null
                || string.IsNullOrWhiteSpace(node.partTypeName)
                || !PartTypeMatchUtil.TryParsePartType(node.partType, out int partType))
            {
                continue;
            }

            TrySetPartDisplayNameByType(partType, node.partTypeName);
        }
    }

    /// <summary>
    /// 车辆 → 攻击路径：KJ_Car 溶解隐藏 + 链路显示零件移向各自 attackPathTarget，完成后加载连线。
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
            LogManager.LogFeatureError("[VehicleToAttackPath] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        if (_kjDissolve.MaterialCount == 0)
        {
            LogManager.LogFeatureWarning("[VehicleToAttackPath] KJ_Car 未找到带 _DissolveAmount 的材质。");
        }

        KillSequence();
        ResetCarDragRotation();
        RestoreAllPartsToInitialState();
        HideAttackPathControllerImmediate();
        List<Transform> displayParts = PrepareAttackPathDisplayPartsAtInitial();
        _activePart = null;
        CacheAttackPathCameraPose();

        _isTransitioning = true;
        EventManager.Instance?.TriggerVehicleToAttackPathTransitionStarted();
        _sequence = DOTween.Sequence();
        AppendCarHideOnlyTween(_sequence);
        JoinAttackPathCameraToTargetTween(_sequence);
        JoinPartsMoveToAttackPathTargets(_sequence, displayParts);
        _sequence.OnComplete(() => CompleteVehicleToAttackPathTransition(controller));
        return true;
    }

    /// <summary>
    /// 零件 → 攻击路径：隐藏零件级焦点，链路显示零件移向各自 attackPathTarget，完成后加载连线。
    /// </summary>
    public bool PlayPartToAttackPathTransition()
    {
        if (_isTransitioning)
        {
            return false;
        }

        if (!TryValidateAttackPathTransition(out AttackPathController controller))
        {
            return false;
        }

        KillSequence();
        ResetCarDragRotation();
        HideAttackPathControllerImmediate();
        SetPartNameLabelsVisible(false);

        ResolveKjCarReference();
        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        List<Transform> displayParts = CollectAttackPathDisplayParts();
        PreparePartsForPartToAttackPath(displayParts);

        _activePart = null;
        CacheAttackPathCameraPose();

        _isTransitioning = true;
        EventManager.Instance?.TriggerVehicleToAttackPathTransitionStarted();
        _sequence = DOTween.Sequence();
        AppendPartsMoveToAttackPathTargets(_sequence, displayParts);
        JoinAttackPathCameraToTargetTween(_sequence);
        _sequence.OnComplete(() => CompleteVehicleToAttackPathTransition(controller));
        return true;
    }

    /// <summary>
    /// 攻击路径 → 车辆：停止路径，链路显示零件收回开局位姿并隐藏，KJ_Car 溶解显现。
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
            LogManager.LogFeatureError("[AttackPathToVehicle] 未找到 KJ_Car。");
            return false;
        }

        PrepareKjCarDissolve();
        KillSequence();
        StopAndHideAttackPath();
        List<Transform> displayParts = CollectAttackPathDisplayParts();
        EnsurePartsActiveForRetract(displayParts);
        ResetCarDragRotation();
        _activePart = null;

        _isTransitioning = true;
        EventManager.Instance?.TriggerAttackPathToVehicleTransitionStarted();
        _kjCarRoot.SetActive(true);
        _kjDissolve.SetDissolveAmount(1f);

        _sequence = DOTween.Sequence();
        AppendPartsMoveToInitialAndHide(_sequence, displayParts, excludePart: null);
        JoinCarAppearTween(_sequence);
        JoinAttackPathCameraRestoreTween(_sequence);
        _sequence.OnComplete(CompleteAttackPathToVehicleTransition);
        return true;
    }

    private bool TryValidateAttackPathTransition(out AttackPathController controller)
    {
        controller = ResolveAttackPathController();
        if (controller == null)
        {
            LogManager.LogFeatureError("[VehicleToAttackPath] 未配置 AttackPathController。");
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

        // 动画被跳过/打断时也保证最终是攻击链路镜头
        SnapAttackPathCameraToTarget();

        // 先落到目标位姿，再按名单显隐，避免 GridLine 另一套引用把「已飞到位」的零件盖回开局态
        List<Transform> displayParts = CollectAttackPathDisplayParts();
        for (int i = 0; i < displayParts.Count; i++)
        {
            SnapPartToAttackPathTargetIfConfigured(displayParts[i]);
        }

        ApplyAttackPathPartsVisibilityFromCache();

        if (controller != null)
        {
            controller.gameObject.SetActive(true);
            CarVehicleDataController dataController = CarVehicleDataController.Instance;
            if (dataController == null || !dataController.ApplyAttackPathsFromCacheForTransition())
            {
                LogManager.LogFeatureWarning("[VehicleToAttackPath] 无攻击链路缓存或加载失败，跳过连线绘制。");
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

        // 攻击链路→车辆：再次确保链路与零部件正确隐藏。
        EnsureVehicleLevelVisualsHidden();
        EventManager.Instance?.TriggerAttackPathToVehicleTransitionCompleted();
    }

    /// <summary>
    /// 进入车辆级展示态：停止攻击链路，还原零件位姿并强制隐藏全部零部件模型，关闭零件名标签。
    /// 省→车 / 零件→车 / 攻击链路→车 均调用；车辆级轮播期间也应保持隐藏。
    /// </summary>
    public void EnsureVehicleLevelVisualsHidden()
    {
        StopAndHideAttackPath();
        RestoreAllPartsToInitialState();
        ForceHideAllPartModels();
        _activePart = null;
        SetPartNameLabelsVisible(false);
    }

    /// <summary>强制隐藏 _partRoots 与 GridLine.start3D 零部件模型（不改位姿）。</summary>
    public void ForceHideAllPartModels()
    {
        if (_partRoots != null)
        {
            for (int i = 0; i < _partRoots.Count; i++)
            {
                Transform part = _partRoots[i].partRoot;
                if (part == null || !part.gameObject.activeSelf)
                {
                    continue;
                }

                part.DOKill();
                part.gameObject.SetActive(false);
            }
        }

        GridLine gridLine = ResolveGridLine();
        gridLine?.HideAllPartTransforms();
    }

    private bool PlayAttackPathToPartTransition(
        Transform targetPart,
        string targetPartId,
        int targetPartType = 0)
    {
        if (targetPart == null)
        {
            return false;
        }

        KillSequence();
        StopAndHideAttackPath();

        List<Transform> displayParts = CollectAttackPathDisplayParts();
        EnsurePartsActiveForRetract(displayParts);
        SnapPartToAttackPathTargetIfConfigured(targetPart);
        targetPart.gameObject.SetActive(true);

        _activePart = targetPart;
        _lastPartName = targetPart.name;
        _lastPartId = targetPartId;
        _lastPartType = PartTypeMatchUtil.IsValidPartType(targetPartType)
            ? targetPartType
            : ResolvePartTypeByTransform(targetPart);

        ResolveKjCarReference();
        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        _isTransitioning = true;
        EventManager.Instance?.TriggerAttackPathToPartTransitionStarted(_lastPartName, _lastPartId);

        _sequence = DOTween.Sequence();
        // 其它链路零件收回开局并隐藏，与目标零件第一段焦点动画并行
        AppendPartsMoveToInitialAndHide(_sequence, displayParts, excludePart: targetPart);
        JoinMoveToTarget(_sequence, targetPart, _firstTarget, _firstMoveDuration);
        JoinAttackPathCameraShortRestoreTween(_sequence);
        // 第二段：目标零件 → second + 镜头跟随
        AppendMoveToTarget(_sequence, targetPart, _secondTarget, _secondMoveDuration);
        JoinPartCameraFollowTween(_sequence);
        _sequence.OnComplete(CompleteAttackPathToPartTransition);
        return true;
    }

    private void CompleteAttackPathToPartTransition()
    {
        _isTransitioning = false;
        // 非目标链路零件应收回隐藏；目标零件保持零件级展示
        List<Transform> displayParts = CollectAttackPathDisplayParts();
        for (int i = 0; i < displayParts.Count; i++)
        {
            Transform part = displayParts[i];
            if (part == null || part == _activePart)
            {
                continue;
            }

            if (TryGetPartInitialState(part, out PartInitialState state))
            {
                part.localPosition = state.LocalPosition;
                part.localRotation = state.LocalRotation;
                part.localScale = state.LocalScale;
            }

            part.gameObject.SetActive(false);
        }

        EventManager.Instance?.TriggerAttackPathToPartTransitionCompleted(
            _lastPartName ?? string.Empty,
            _lastPartId ?? string.Empty);
    }

    /// <summary>
    /// 收集攻击链路应播放位移的零件：优先 nodes.partType 匹配 _partRoots；
    /// 名单匹配到但无法解析目标时，回退为所有已配置 attackPathTarget 的零件。
    /// </summary>
    private List<Transform> CollectAttackPathDisplayParts()
    {
        List<Transform> result = new List<Transform>();
        HashSet<Transform> added = new HashSet<Transform>();

        CarVehicleDataStore store = CarVehicleDataStore.Instance;
        List<int> types = store != null ? store.BuildAttackChainNodePartTypes() : null;

        if (types != null && types.Count > 0)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (!TryResolvePartByType(types[i], out Transform part) || part == null || !added.Add(part))
                {
                    continue;
                }

                result.Add(part);
            }
        }

        // 兼容：无 partType 时仍按 partTypeName / 显示名匹配
        if (result.Count == 0)
        {
            List<string> names = store != null ? store.BuildAttackChainNodePartNames() : null;
            if (names != null && names.Count > 0)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    Transform part = ResolveAttackPathPartRootByName(name.Trim());
                    if (part == null || !added.Add(part))
                    {
                        continue;
                    }

                    result.Add(part);
                }
            }
        }

        int withTarget = CountPartsWithAttackPathTarget(result);
        if (result.Count == 0 || withTarget == 0)
        {
            result.Clear();
            added.Clear();
            int fallbackCount = CollectPartsWithAttackPathTarget(result, added);
            if (fallbackCount > 0)
            {
                LogManager.LogFeatureWarning(
                    "[VehicleToPart] 攻击链路名单未匹配到带 attackPathTarget 的 _partRoots，" +
                    $"已回退为已配置目标的 {fallbackCount} 个零件。");
            }
            else
            {
                LogManager.LogFeatureWarning(
                    "[VehicleToPart] 未收集到可动画零件：请确认 PartBindingData.partType 与接口 " +
                    "partType(1~5) 一致，并在同一行拖好 attackPathTarget。");
            }
        }

        return result;
    }

    /// <summary>仅从 _partRoots 按 partId / 物体名解析零件根（忽略 GridLine）。</summary>
    private Transform ResolveAttackPathPartRootByName(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName) || _partRoots == null)
        {
            return null;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == null)
            {
                continue;
            }

            string id = NormalizePartId(binding.partId, binding.partRoot);
            if (string.Equals(id, partName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.partRoot.name, partName, StringComparison.OrdinalIgnoreCase))
            {
                return binding.partRoot;
            }
        }

        return null;
    }

    private int CountPartsWithAttackPathTarget(List<Transform> parts)
    {
        int count = 0;
        if (parts == null)
        {
            return 0;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null && GetAttackPathTargetForPart(parts[i]) != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CollectPartsWithAttackPathTarget(List<Transform> result, HashSet<Transform> added)
    {
        int count = 0;
        if (_partRoots == null || result == null || added == null)
        {
            return 0;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == null || binding.attackPathTarget == null)
            {
                continue;
            }

            if (!added.Add(binding.partRoot))
            {
                continue;
            }

            result.Add(binding.partRoot);
            count++;
        }

        return count;
    }

    /// <summary>链路显示零件放到开局位姿并激活，供车辆→攻击链路出发。</summary>
    private List<Transform> PrepareAttackPathDisplayPartsAtInitial()
    {
        List<Transform> parts = CollectAttackPathDisplayParts();
        int withTarget = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            Transform part = parts[i];
            if (part == null)
            {
                continue;
            }

            EnsurePartInitialStateCached(part);
            if (TryGetPartInitialState(part, out PartInitialState state))
            {
                part.localPosition = state.LocalPosition;
                part.localRotation = state.LocalRotation;
                part.localScale = state.LocalScale;
            }

            part.gameObject.SetActive(true);
            if (GetAttackPathTargetForPart(part) != null)
            {
                withTarget++;
            }
        }

        LogManager.LogFeature(
            $"[VehicleToPart] 攻击链路零件动画准备 | parts={parts.Count} | withTarget={withTarget} | " +
            $"duration={AttackPathPartMoveDuration:F2}s");
        SetPartNameLabelsVisible(true);
        return parts;
    }

    private void EnsurePartInitialStateCached(Transform part)
    {
        if (part == null || _partInitialStates.ContainsKey(part.GetInstanceID()))
        {
            return;
        }

        _partInitialStates[part.GetInstanceID()] = new PartInitialState
        {
            LocalPosition = part.localPosition,
            LocalRotation = part.localRotation,
            LocalScale = part.localScale,
            IsActive = false,
        };
    }

    /// <summary>零件→攻击链路：当前焦点零件保持现位，其它链路零件从开局位姿出发。</summary>
    private void PreparePartsForPartToAttackPath(List<Transform> displayParts)
    {
        Transform focusPart = _activePart;
        for (int i = 0; i < displayParts.Count; i++)
        {
            Transform part = displayParts[i];
            if (part == null)
            {
                continue;
            }

            if (part == focusPart)
            {
                part.gameObject.SetActive(true);
                continue;
            }

            if (TryGetPartInitialState(part, out PartInitialState state))
            {
                part.localPosition = state.LocalPosition;
                part.localRotation = state.LocalRotation;
                part.localScale = state.LocalScale;
            }

            part.gameObject.SetActive(true);
        }

        // 非链路零件全部隐藏
        if (_partRoots != null)
        {
            HashSet<Transform> displaySet = new HashSet<Transform>(displayParts);
            for (int i = 0; i < _partRoots.Count; i++)
            {
                Transform part = _partRoots[i].partRoot;
                if (part != null && !displaySet.Contains(part))
                {
                    part.gameObject.SetActive(false);
                }
            }
        }

        SetPartNameLabelsVisible(true);
    }

    private void EnsurePartsActiveForRetract(List<Transform> parts)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            Transform part = parts[i];
            if (part == null)
            {
                continue;
            }

            SnapPartToAttackPathTargetIfConfigured(part);
            part.gameObject.SetActive(true);
        }
    }

    private void SnapPartToAttackPathTargetIfConfigured(Transform part)
    {
        Transform target = GetAttackPathTargetForPart(part);
        if (part == null || target == null)
        {
            return;
        }

        part.DOKill();
        part.position = target.position;
        part.rotation = target.rotation;
        part.localScale = target.localScale;
    }

    private Transform GetAttackPathTargetForPart(Transform part)
    {
        if (part == null || _partRoots == null)
        {
            return null;
        }

        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.partRoot == part)
            {
                return binding.attackPathTarget;
            }
        }

        // GridLine 模型与 _partRoots 不是同一引用时：按名称回查 attackPathTarget
        for (int i = 0; i < _partRoots.Count; i++)
        {
            PartBindingData binding = _partRoots[i];
            if (binding.attackPathTarget == null || binding.partRoot == null)
            {
                continue;
            }

            string id = NormalizePartId(binding.partId, binding.partRoot);
            if (string.Equals(id, part.name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(binding.partRoot.name, part.name, StringComparison.OrdinalIgnoreCase))
            {
                return binding.attackPathTarget;
            }
        }

        return null;
    }

    private float AttackPathPartMoveDuration => Mathf.Max(0.01f, _attackPathPartMoveDuration);

    /// <summary>并行：零件移向各自 attackPathTarget（无目标则保持当前姿态）。</summary>
    private void JoinPartsMoveToAttackPathTargets(Sequence sequence, List<Transform> parts)
    {
        AppendOrJoinPartsMoveToAttackPathTargets(sequence, parts, appendFirst: false);
    }

    /// <summary>先 Append 第一段零件移动，其余 Join。</summary>
    private void AppendPartsMoveToAttackPathTargets(Sequence sequence, List<Transform> parts)
    {
        AppendOrJoinPartsMoveToAttackPathTargets(sequence, parts, appendFirst: true);
    }

    private void AppendOrJoinPartsMoveToAttackPathTargets(Sequence sequence, List<Transform> parts, bool appendFirst)
    {
        if (sequence == null || parts == null || parts.Count == 0)
        {
            if (appendFirst && sequence != null)
            {
                sequence.AppendInterval(AttackPathPartMoveDuration);
            }

            return;
        }

        float duration = AttackPathPartMoveDuration;
        bool started = false;
        int tweenCount = 0;

        for (int i = 0; i < parts.Count; i++)
        {
            Transform part = parts[i];
            Transform target = GetAttackPathTargetForPart(part);
            if (part == null)
            {
                continue;
            }

            part.gameObject.SetActive(true);
            if (target == null)
            {
                LogManager.LogFeatureWarning(
                    $"[VehicleToPart] 零件「{part.name}」未配置 attackPathTarget，跳过位移。");
                continue;
            }

            Tween moveTween = BuildMoveTween(part, target, duration);
            if (moveTween == null)
            {
                SnapPartToAttackPathTargetIfConfigured(part);
                continue;
            }

            if (!started)
            {
                // appendFirst：本段自己 Append；否则假定序列已有车辆溶解等，直接 Join
                if (appendFirst)
                {
                    sequence.Append(moveTween);
                }
                else
                {
                    sequence.Join(moveTween);
                }

                started = true;
            }
            else
            {
                sequence.Join(moveTween);
            }

            tweenCount++;
        }

        if (!started && appendFirst)
        {
            sequence.AppendInterval(duration);
        }

        LogManager.LogFeature(
            $"[VehicleToPart] 攻击链路零件位移 | tweens={tweenCount} | appendFirst={appendFirst} | " +
            $"duration={duration:F2}s");
    }

    /// <summary>Append：零件并行回到开局本地位姿后隐藏（可排除某一零件）。</summary>
    private void AppendPartsMoveToInitialAndHide(
        Sequence sequence,
        List<Transform> parts,
        Transform excludePart)
    {
        if (sequence == null)
        {
            return;
        }

        float duration = AttackPathPartMoveDuration;
        Sequence group = DOTween.Sequence();
        bool joined = false;
        List<Transform> toHide = new List<Transform>();

        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                Transform part = parts[i];
                if (part == null || part == excludePart)
                {
                    continue;
                }

                toHide.Add(part);
                if (!TryGetPartInitialState(part, out PartInitialState state))
                {
                    part.gameObject.SetActive(false);
                    continue;
                }

                part.gameObject.SetActive(true);
                Tween moveTween = BuildMoveTweenToPose(
                    part,
                    state.LocalPosition,
                    state.LocalRotation,
                    state.LocalScale,
                    duration);
                if (moveTween != null)
                {
                    group.Join(moveTween);
                    joined = true;
                }
                else
                {
                    part.localPosition = state.LocalPosition;
                    part.localRotation = state.LocalRotation;
                    part.localScale = state.LocalScale;
                }
            }
        }

        if (!joined)
        {
            group.AppendInterval(0.01f);
        }

        group.OnComplete(() =>
        {
            for (int i = 0; i < toHide.Count; i++)
            {
                Transform part = toHide[i];
                if (part != null)
                {
                    part.gameObject.SetActive(false);
                }
            }
        });

        sequence.Append(group);
    }

    private void JoinMoveToTarget(Sequence sequence, Transform part, Transform target, float duration)
    {
        Tween moveTween = BuildMoveTween(part, target, duration);
        if (moveTween != null)
        {
            sequence.Join(moveTween);
        }
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

    /// <summary>进入攻击路径级：按 AttackChainData.nodes 的 partType 显隐；无名单则显示全部。</summary>
    private void ApplyAttackPathPartsVisibilityFromCache()
    {
        List<int> partTypes = CarVehicleDataStore.Instance.BuildAttackChainNodePartTypes();
        if (partTypes.Count > 0)
        {
            ShowPartsByPartTypes(partTypes);
        }
        else
        {
            ShowPartsByNames(CarVehicleDataStore.Instance.BuildAttackChainNodePartNames());
        }

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

    /// <summary>与当前段并行：相机移动到攻击路径目标点位；无法 Join 时改为 Append。</summary>
    private void JoinAttackPathCameraToTargetTween(Sequence sequence)
    {
        if (sequence == null)
        {
            return;
        }

        Tween cameraTween = BuildAttackPathCameraMoveTween(_attackPathCameraTarget, _attackPathCameraMoveDuration);
        if (cameraTween == null)
        {
            // 播不了动画时立刻对齐，避免停在零件/车辆镜头
            SnapAttackPathCameraToTarget();
            return;
        }

        // 序列尚无时间轴时 Join 无效，改为 Append
        if (sequence.Duration(false) <= 0f)
        {
            sequence.Append(cameraTween);
        }
        else
        {
            sequence.Join(cameraTween);
        }
    }

    /// <summary>进入攻击链路完成时：镜头强制对齐到 _attackPathCameraTarget。</summary>
    private void SnapAttackPathCameraToTarget()
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || _attackPathCameraTarget == null)
        {
            return;
        }

        camera.DOKill();
        camera.position = _attackPathCameraTarget.position;
        camera.rotation = _attackPathCameraTarget.rotation;
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
        if (camera == null)
        {
            LogManager.LogFeatureWarning(
                "[VehicleToAttackPath] 未找到攻击链路镜头（请配置 _attackPathCamera，或保证 Camera.main 可用）。");
            return null;
        }

        if (target == null)
        {
            LogManager.LogFeatureWarning(
                "[VehicleToAttackPath] 未配置 _attackPathCameraTarget，跳过镜头动画。");
            return null;
        }

        // 清掉其它系统占用的镜头 Tween，避免新动画不播
        camera.DOKill();
        float moveDuration = Mathf.Max(0.01f, duration);
        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(target.position, moveDuration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(target.rotation, moveDuration).SetEase(_attackPathCameraEase));
        return moveSequence;
    }

    private Tween BuildAttackPathCameraRestoreTween(float duration)
    {
        Transform camera = ResolveAttackPathCamera();
        if (camera == null || !_hasCachedAttackPathCameraPose)
        {
            return null;
        }

        camera.DOKill();
        float moveDuration = Mathf.Max(0.01f, duration);
        Sequence moveSequence = DOTween.Sequence();
        moveSequence.Join(camera.DOMove(_cachedAttackPathCameraPosition, moveDuration).SetEase(_attackPathCameraEase));
        moveSequence.Join(camera.DORotateQuaternion(_cachedAttackPathCameraRotation, moveDuration).SetEase(_attackPathCameraEase));
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
