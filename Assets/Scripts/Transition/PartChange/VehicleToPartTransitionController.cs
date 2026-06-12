using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 车辆 → 零件过渡：零件先移动到第一目标（与 KJ_Car 溶解隐藏并行），再移动到第二目标；
/// 倒播则反向执行并令 KJ_Car 溶解显现。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionController : MonoBehaviour
{
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

    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();
    private readonly Dictionary<int, PartInitialState> _partInitialStates = new Dictionary<int, PartInitialState>();
    private Sequence _sequence;
    private bool _isTransitioning;
    private Transform _activePart;
    private string _lastPartName;
    private Vector3 _cachedPartLocalPosition;
    private Quaternion _cachedPartLocalRotation;
    private Vector3 _cachedPartLocalScale;

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

    private void Awake()
    {
        _instance = this;
        ResolveKjCarReference();
        CacheAllPartInitialStates();
    }

    private void OnDestroy()
    {
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

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
        RestoreAllPartsToInitialState();
        ApplyCachedPoseFromInitialState(part);

        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
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
        ResetCarDragRotation();
        _isTransitioning = true;
        _activePart = part;
        _lastPartName = part.name;
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

    /// <summary>重置 Car 物体上的拖拽旋转（MouseDragYawRotate）。</summary>
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

    private void CompleteForwardTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(1f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }

        NotifyTransitionCompleted(isReverse: false);
    }

    private void CompleteReverseTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(0f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(true);
        }

        NotifyTransitionCompleted(isReverse: true);
    }

    private void NotifyTransitionCompleted(bool isReverse)
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

    private void PrepareKjCarDissolve()
    {
        // 使用材质实例，避免修改 sharedMaterial 污染 Part_CarLine 等资源
        _kjDissolve.CollectFrom(_kjCarRoot, isShareMaterial: false);
        _kjDissolve.SetDissolveNoiseScale(_dissolveNoiseScale);
        _kjDissolve.SetDissolveAmount(0f);
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

        _sequence = null;
        _isTransitioning = false;
    }

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
#endif
}
