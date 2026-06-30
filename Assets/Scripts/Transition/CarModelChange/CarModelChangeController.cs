using DG.Tweening;
using UnityEngine;

/// <summary>
/// RealyCar 与 KJ_Car 溶解切换控制器。
/// 隐藏方 DissolveAmount 0→1，显现方 1→0，过渡结束后 SetActive(false) 隐藏消失方。
/// </summary>
[DisallowMultipleComponent]
public class CarModelChangeController : MonoBehaviour
{
    [Header("模型引用（留空则自动查找 Model/Car 下子物体）")]
    [SerializeField] private GameObject _realyCarRoot;
    [SerializeField] private GameObject _kjCarRoot;

    [Header("过渡参数")]
    [SerializeField] private float _transitionDuration = 2f;
    [SerializeField] private Ease _transitionEase = Ease.InOutQuad;
    /// <summary>写入材质 _DissolveNoiseScale，值越大溶解噪声越细密。</summary>
    [SerializeField] private float _dissolveNoiseScale = 12f;

    // 分别管理两车根节点下所有溶解材质，切换时并行 tween
    private readonly CarModelDissolveGroup _realyDissolve = new CarModelDissolveGroup();
    private readonly CarModelDissolveGroup _kjDissolve = new CarModelDissolveGroup();

    private Sequence _sequence;
    private bool _isTransitioning;
    /// <summary>当前是否处于 KJ_Car 显示状态（过渡完成后更新）。</summary>
    private bool _showingKjCar;

    public bool IsTransitioning => _isTransitioning;
    public bool ShowingKjCar => _showingKjCar;

    /// <summary>KJ_Car 根节点（供零件过渡等模块复用，统一在此 Inspector 配置）。</summary>
    public GameObject KjCarRoot
    {
      get
      {
        ResolveReferences();
        return _kjCarRoot;
      }
    }

    private static CarModelChangeController _instance;

    public static CarModelChangeController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CarModelChangeController>();
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

    // private void OnDisable()
    // {
    //       _realyDissolve.SetDissolveAmount(0f);
    //     _kjDissolve.SetDissolveAmount(0f);
    // }
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

    /// <summary>
    /// 播放双向溶解：两车同时 SetActive(true)，隐藏方 0→1、显现方 1→0。
    /// </summary>
    /// <returns>已在过渡中、目标状态相同或缺少材质时返回 false。</returns>
    private bool PlayTransition(bool showKjCar)
    {
        if (_isTransitioning)
        {
            return false;
        }

        ResolveReferences();
        if (_realyCarRoot == null || _kjCarRoot == null)
        {
            Debug.LogError("[CarModelChange] 未找到 RealyCar 或 KJ_Car。");
            return false;
        }

        if (_showingKjCar == showKjCar)
        {
            return false;
        }

        // 每次切换前重新收集材质实例，防止 Renderer 或材质在运行时被替换
        CacheDissolveMaterials();
        ApplyDissolveNoiseScale();

        CarModelDissolveGroup hideGroup = showKjCar ? _realyDissolve : _kjDissolve;
        CarModelDissolveGroup appearGroup = showKjCar ? _kjDissolve : _realyDissolve;
        GameObject hideRoot = showKjCar ? _realyCarRoot : _kjCarRoot;
        GameObject appearRoot = showKjCar ? _kjCarRoot : _realyCarRoot;

        if (hideGroup.MaterialCount == 0 || appearGroup.MaterialCount == 0)
        {
            Debug.LogWarning("[CarModelChange] 未找到带 _DissolveAmount 的材质，请检查子物体材质与 Shader。");
            return false;
        }

        KillSequence();
        _isTransitioning = true;

        hideRoot.SetActive(true);
        appearRoot.SetActive(true);

        // 重置到过渡起点：隐藏方完全可见，显现方完全溶解
        hideGroup.SetDissolveAmount(0f);
        appearGroup.SetDissolveAmount(1f);

        float hideAmount = 0f;
        float appearAmount = 1f;

        _sequence = DOTween.Sequence();
        _sequence.Join(DOTween.To(() => hideAmount, value =>
        {
            hideAmount = value;
            hideGroup.SetDissolveAmount(value);
        }, 1f, _transitionDuration).SetEase(_transitionEase));

        _sequence.Join(DOTween.To(() => appearAmount, value =>
        {
            appearAmount = value;
            appearGroup.SetDissolveAmount(value);
        }, 0f, _transitionDuration).SetEase(_transitionEase));

        _sequence.OnComplete(() => CompleteTransition(showKjCar, hideRoot, appearGroup));
        return true;
    }

    /// <summary>过渡结束：显现方归零溶解值，隐藏方 GameObject 关闭，并广播完成事件。</summary>
    private void CompleteTransition(bool showKjCar, GameObject hideRoot, CarModelDissolveGroup appearGroup)
    {
        _showingKjCar = showKjCar;
        _isTransitioning = false;

        appearGroup.SetDissolveAmount(0f);
        hideRoot.SetActive(false);

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

    /// <summary>
    /// 默认只显示 RealyCar。KJ_Car 虽隐藏但 DissolveAmount=1，
    /// 以便下次 SwitchToKjCar 时从完全溶解状态开始显现。
    /// </summary>
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

    /// <summary>
    /// 查找同时包含 RealyCar、KJ_Car 的 Car 根节点。
    /// 使用 FindObjectsOfTypeAll 以兼容未激活或预制体编辑场景中的对象。
    /// </summary>
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
