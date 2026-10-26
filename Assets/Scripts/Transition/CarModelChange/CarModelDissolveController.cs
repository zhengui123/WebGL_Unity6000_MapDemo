using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// RealyCar 与 KJ_Car 溶解材质切换：隐藏方 DissolveAmount 0→1，显现方 1→0，过渡结束后 SetActive(false)。
/// </summary>
[DisallowMultipleComponent]
public class CarModelDissolveController : MonoBehaviour
{
    /// <summary>
    /// KJ_Car 可见强度：0=完全溶解/隐藏，1=完全显示。
    /// 由溶解量换算：visible = 1 - DissolveAmount。供全息后处理等同步。
    /// </summary>
    public static event Action<float> OnKjCarVisibleStrengthChanged;

    /// <summary>按 KJ 溶解量通知可见强度（DissolveAmount 0=显，1=隐）。</summary>
    public static void NotifyKjCarVisibleStrengthFromDissolve(float dissolveAmount)
    {
        OnKjCarVisibleStrengthChanged?.Invoke(Mathf.Clamp01(1f - dissolveAmount));
    }
    [Header("模型引用（留空则自动查找 Model/Car 下子物体）")]
    [SerializeField] private GameObject _realyCarRoot;
    [SerializeField] private GameObject _kjCarRoot;

    [Header("溶解过渡参数")]
    [SerializeField] private float _transitionDuration = 2f;
    [SerializeField] private Ease _transitionEase = Ease.InOutQuad;
    /// <summary>写入材质 _DissolveNoiseScale，值越大溶解噪声越细密。</summary>
    [SerializeField] private float _dissolveNoiseScale = 12f;

    private readonly CarModelDissolveGroup _realyDissolve = new CarModelDissolveGroup();
    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();

    private Sequence _sequence;
    private bool _isTransitioning;
    private bool _showingKjCar;

    public bool IsTransitioning => _isTransitioning;
    public bool ShowingKjCar => _showingKjCar;

    /// <summary>溶解过渡时长（供全息后处理 effectStrength 等共用）。</summary>
    public float TransitionDuration => _transitionDuration;

    /// <summary>溶解过渡缓动（供全息后处理 effectStrength 等共用）。</summary>
    public Ease TransitionEase => _transitionEase;

    /// <summary>KJ_Car 根节点（供零件过渡等模块复用，统一在此 Inspector 配置）。</summary>
    public GameObject KjCarRoot
    {
        get
        {
            ResolveReferences();
            return _kjCarRoot;
        }
    }

    private static CarModelDissolveController _instance;

    public static CarModelDissolveController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CarModelDissolveController>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
        CacheDissolveMaterials();
        ApplyDissolveNoiseScale();
        ApplyInitialVisibility();
    }

    private void OnDestroy()
    {
        _realyDissolve.SetDissolveAmount(0f);
        _kjDissolve.SetDissolveAmount(0f);
        KillSequence();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>RealyCar 溶解消失，KJ_Car 溶解显现。</summary>
    public bool SwitchToKjCar()
    {
        return PlayTransition(showKjCar: true);
    }

    /// <summary>KJ_Car 溶解消失，RealyCar 溶解显现。</summary>
    public bool SwitchToRealyCar()
    {
        return PlayTransition(showKjCar: false);
    }

    /// <summary>若车辆溶解切换正在播放，则立即完成并执行完成回调。</summary>
    public void CompleteCurrentTransitionImmediate()
    {
        if (_sequence == null || !_sequence.IsActive())
        {
            return;
        }

        _sequence.Complete(withCallbacks: true);
    }

    private bool PlayTransition(bool showKjCar)
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_realyCarRoot == null || _kjCarRoot == null)
        {
            LogManager.LogFeatureError("[CarModelDissolve] 未找到 RealyCar 或 KJ_Car。");
            return false;
        }

        if (_showingKjCar == showKjCar)
        {
            return false;
        }

        CacheDissolveMaterials();
        ApplyDissolveNoiseScale();

        CarModelDissolveGroup hideGroup = showKjCar ? _realyDissolve : _kjDissolve;
        CarModelDissolveGroup appearGroup = showKjCar ? _kjDissolve : _realyDissolve;
        GameObject hideRoot = showKjCar ? _realyCarRoot : _kjCarRoot;
        GameObject appearRoot = showKjCar ? _kjCarRoot : _realyCarRoot;

        if (hideGroup.MaterialCount == 0 || appearGroup.MaterialCount == 0)
        {
            LogManager.LogFeatureWarning("[CarModelDissolve] 未找到带 _DissolveAmount 的材质，请检查子物体材质与 Shader。");
            return false;
        }

        KillSequence();
        _isTransitioning = true;

        hideRoot.SetActive(true);
        appearRoot.SetActive(true);

        hideGroup.SetDissolveAmount(0f);
        appearGroup.SetDissolveAmount(1f);
        // 起始：显现方全溶、隐藏方全显 → KJ 可见度与溶解一致
        if (showKjCar)
        {
            NotifyKjCarVisibleStrengthFromDissolve(1f);
        }
        else
        {
            NotifyKjCarVisibleStrengthFromDissolve(0f);
        }

        float hideAmount = 0f;
        float appearAmount = 1f;

        _sequence = DOTween.Sequence();
        _sequence.Join(DOTween.To(() => hideAmount, value =>
        {
            hideAmount = value;
            hideGroup.SetDissolveAmount(value);
            // 切回 Realy：KJ 为隐藏方，溶解 0→1 → 可见度 1→0
            if (!showKjCar)
            {
                NotifyKjCarVisibleStrengthFromDissolve(value);
            }
        }, 1f, _transitionDuration).SetEase(_transitionEase));

        _sequence.Join(DOTween.To(() => appearAmount, value =>
        {
            appearAmount = value;
            appearGroup.SetDissolveAmount(value);
            // 切到 KJ：KJ 为显现方，溶解 1→0 → 可见度 0→1
            if (showKjCar)
            {
                NotifyKjCarVisibleStrengthFromDissolve(value);
            }
        }, 0f, _transitionDuration).SetEase(_transitionEase));

        _sequence.OnComplete(() => CompleteTransition(showKjCar, hideRoot, appearGroup));
        if (_transitionDuration <= 0f)
        {
            _sequence.Complete(withCallbacks: true);
        }

        return true;
    }

    private void CompleteTransition(bool showKjCar, GameObject hideRoot, CarModelDissolveGroup appearGroup)
    {
        _showingKjCar = showKjCar;
        _isTransitioning = false;

        appearGroup.SetDissolveAmount(0f);
        hideRoot.SetActive(false);
        NotifyKjCarVisibleStrengthFromDissolve(showKjCar ? 0f : 1f);

        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        if (showKjCar)
        {
            em.TriggerCarSwitchToKjCarCompleted();
        }
        else
        {
            em.TriggerCarSwitchToRealyCarCompleted();
        }
    }

    private void ApplyInitialVisibility()
    {
        if (_realyCarRoot == null || _kjCarRoot == null)
        {
            return;
        }

        _realyCarRoot.SetActive(true);
        _kjCarRoot.SetActive(false);
        _showingKjCar = false;

        _realyDissolve.SetDissolveAmount(0f);
        _kjDissolve.SetDissolveAmount(1f);
        NotifyKjCarVisibleStrengthFromDissolve(1f);
    }

    private void CacheDissolveMaterials()
    {
        if (_realyCarRoot != null)
        {
            _realyDissolve.CollectFrom(_realyCarRoot);
        }

        if (_kjCarRoot != null)
        {
            _kjDissolve.CollectFrom(_kjCarRoot, true);
        }
    }

    private void ApplyDissolveNoiseScale()
    {
        _realyDissolve.SetDissolveNoiseScale(_dissolveNoiseScale);
        _kjDissolve.SetDissolveNoiseScale(_dissolveNoiseScale);
    }

    private void ResolveReferences()
    {
        if (_realyCarRoot != null && _kjCarRoot != null)
        {
            return;
        }

        Transform carRoot = FindCarRootTransform();
        if (carRoot == null)
        {
            return;
        }

        if (_realyCarRoot == null)
        {
            Transform realy = carRoot.Find("RealyCar");
            if (realy != null)
            {
                _realyCarRoot = realy.gameObject;
            }
        }

        if (_kjCarRoot == null)
        {
            Transform kj = carRoot.Find("KJ_Car");
            if (kj != null)
            {
                _kjCarRoot = kj.gameObject;
            }
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

            if (car.Find("RealyCar") != null && car.Find("KJ_Car") != null)
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
    }

#if UNITY_EDITOR
    [ContextMenu("测试：切换到 KJ_Car")]
    private void EditorSwitchToKjCar()
    {
        SwitchToKjCar();
    }

    [ContextMenu("测试：切换回 RealyCar")]
    private void EditorSwitchToRealyCar()
    {
        SwitchToRealyCar();
    }
#endif
}
