using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 车辆 → 零件过渡：指定零件先 DOTween 到第一目标位姿，再移动到第二目标位姿；
/// 第二阶段与 KJ_Car 溶解隐藏并行执行。
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

    [Header("过渡参数")]
    [SerializeField] private float _firstMoveDuration = 1.2f;
    [SerializeField] private float _secondMoveDuration = 1.5f;
    [SerializeField] private Ease _moveEase = Ease.InOutQuad;
    [SerializeField] private float _kjDissolveDuration = 1.5f;
    [SerializeField] private Ease _kjDissolveEase = Ease.InOutQuad;
    [SerializeField] private float _dissolveNoiseScale = 12f;

    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();
    private Sequence _sequence;
    private bool _isTransitioning;
    private Transform _activePart;

    public bool IsTransitioning => _isTransitioning;
    public Transform ActivePart => _activePart;

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
        _isTransitioning = true;
        _activePart = part;
        part.gameObject.SetActive(true);

        _sequence = DOTween.Sequence();
        AppendMoveToTarget(_sequence, part, _firstTarget, _firstMoveDuration);
        _sequence.AppendCallback(() => BeginSecondPhase(part));
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

    private void BeginSecondPhase(Transform part)
    {
        Tween moveTween = BuildMoveTween(part, _secondTarget, _secondMoveDuration);
        Tween dissolveTween = BuildKjDissolveTween();

        _sequence = DOTween.Sequence();
        if (moveTween != null)
        {
            _sequence.Append(moveTween);
        }

        if (dissolveTween != null)
        {
            _sequence.Join(dissolveTween);
        }
        else if (moveTween == null)
        {
            _sequence.AppendInterval(_secondMoveDuration);
        }

        _sequence.OnComplete(CompleteTransition);
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

    private Tween BuildKjDissolveTween()
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

    private void CompleteTransition()
    {
        _isTransitioning = false;
        _kjDissolve.SetDissolveAmount(1f);

        if (_kjCarRoot != null)
        {
            _kjCarRoot.SetActive(false);
        }
    }

    private void PrepareKjCarDissolve()
    {
        _kjDissolve.CollectFrom(_kjCarRoot, isShareMaterial: true);
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
    [ContextMenu("测试：过渡到列表第一个零件")]
    private void EditorPlayFirstPartTransition()
    {
        PlayTransition();
    }
#endif
}
