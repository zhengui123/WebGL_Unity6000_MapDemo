using UnityEngine;
using VolumetricFogAndMist;
using DG.Tweening;

public class EarthTransition : UnitySingle<EarthTransition>
{
    #region 序列化字段

    [Header("场景对象")]
    [SerializeField] private GameObject earthObj;
    [SerializeField] private GameObject plateMapObj;
    [SerializeField] private VolumetricFog fogController;

    [Header("相机配置")]
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private Vector3 firstTargetLocalPos = new Vector3(0f, 1200f, 0f);
    [SerializeField] private Vector3 secondTargetLocalPos = new Vector3(0f, 1000f, 0f);
    [Tooltip("板块视图下相机本地坐标（正向最后一步与雾消散同步到达）")]
    [SerializeField] private Vector3 plateViewLocalPos = new Vector3(0f, 800f, 0f);
    [SerializeField] private float plateMapCenterDistance = 800f;

    [Header("动画时长")]
    public float goEarthAnimTime = 1f;
    public float showFogAnimTime = 1f;
    public float showPlateMapAnimTime = 1f;

    [Header("雾效配置")]
    [SerializeField] private float fogPeakDensity = 1.25f;

    #endregion

    #region 运行时状态

    private Sequence _transitionSequence;
    private Vector3 _cachedEarthCameraLocalPos;
    private bool _hasCachedEarthCameraPos;

    /// <summary>地球 ↔ 板块过渡序列是否正在播放。</summary>
    public bool IsTransitioning => _transitionSequence != null && _transitionSequence.IsActive();

    #endregion

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EarthTransition] 场景中存在多个实例，将销毁重复对象。");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        plateMapObj.SetActive(false);
        CacheEarthCameraLocalPos();
    }

    private void OnDestroy()
    {
        KillCurrentSequence();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region 公开入口

    /// <summary>UGUI Button：地球 → 板块。</summary>
    public void TransitionToPlateMap()
    {
        PlayTransition();
    }

    /// <summary>UGUI Button：板块 → 地球。</summary>
    public void TransitionToEarth()
    {
        PlayTransitionReverse();
    }

    #endregion

    #region 正向过渡（地球 → 板块）

    /// <summary>
    /// 顺序：抬高相机 → 俯冲+起雾 → 切板块并居中 → 雾消散并落到板块视图高度。
    /// </summary>
    public void PlayTransition()
    {
        if (!CanPlayTransition())
        {
            return;
        }

        CacheEarthCameraLocalPos();
        BeginSequence();
        ResetFogDensity(0f);
        EventManager.Instance?.TriggerTransitionToPlateMapStarted();

        _transitionSequence.Append(MoveCameraLocal(firstTargetLocalPos, goEarthAnimTime));
        _transitionSequence.Append(AnimateCameraAndFogIn(secondTargetLocalPos, showFogAnimTime));
        _transitionSequence.AppendCallback(SwitchToPlateMapView);
        _transitionSequence.Append(AnimateCameraAndFogOut(plateViewLocalPos, showPlateMapAnimTime));
        _transitionSequence.OnComplete(NotifyTransitionToPlateMapCompleted);
    }

    #endregion

    #region 反向过渡（板块 → 地球）

    /// <summary>
    /// 顺序：从板块视图抬升+起雾 → 切回地球 → 相机继续抬升+雾消散 → 回到进入过渡前的相机位置。
    /// </summary>
    public void PlayTransitionReverse()
    {
        if (!CanPlayTransition())
        {
            return;
        }

        BeginSequence();
        ResetFogDensity(0f);
        EventManager.Instance?.TriggerTransitionToEarthStarted();

        _transitionSequence.Append(AnimateCameraAndFogIn(secondTargetLocalPos, showPlateMapAnimTime));
        _transitionSequence.AppendCallback(SwitchToEarthView);
        _transitionSequence.Append(AnimateCameraAndFogOut(firstTargetLocalPos, showFogAnimTime));
        _transitionSequence.Append(MoveCameraLocal(GetEarthReturnLocalPos(), goEarthAnimTime));
        _transitionSequence.OnComplete(NotifyTransitionToEarthCompleted);
    }

    #endregion

    #region 视图切换

    private void SwitchToPlateMapView()
    {
        if (earthObj != null)
        {
            earthObj.SetActive(false);
        }

        if (plateMapObj != null)
        {
            plateMapObj.SetActive(true);
            CenterPlateMapInView();
        }
    }

    private void SwitchToEarthView()
    {
        if (plateMapObj != null)
        {
            plateMapObj.SetActive(false);
        }

        if (earthObj != null)
        {
            earthObj.SetActive(true);
        }
    }

    private void CenterPlateMapInView()
    {
        Vector3 viewCenterWorldPos = mainCameraTransform.position + mainCameraTransform.forward * plateMapCenterDistance;
        plateMapObj.transform.position = viewCenterWorldPos;
    }

    #endregion

    #region 相机动画

    private Tween MoveCameraLocal(Vector3 targetLocalPos, float duration)
    {
        return mainCameraTransform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.Linear);
    }

    /// <summary>相机移动 + 雾效增强（正向第二步 / 反向第一步）。</summary>
    private Tween AnimateCameraAndFogIn(Vector3 targetLocalPos, float duration)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.Join(MoveCameraLocal(targetLocalPos, duration));
        sequence.Join(FadeFogDensity(fogPeakDensity, duration));
        return sequence;
    }

    /// <summary>相机移动 + 雾效减弱（正向第四步 / 反向第三步）。</summary>
    private Tween AnimateCameraAndFogOut(Vector3 targetLocalPos, float duration)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.Join(MoveCameraLocal(targetLocalPos, duration));
        sequence.Join(FadeFogDensity(0f, duration));
        return sequence;
    }

    #endregion

    #region 雾效

    private Tween FadeFogDensity(float targetDensity, float duration)
    {
        return DOTween.To(
            () => fogController.density,
            value => fogController.density = value,
            targetDensity,
            duration
        ).SetEase(Ease.Linear);
    }

    private void ResetFogDensity(float densityValue)
    {
        fogController.density = densityValue;
    }

    #endregion

    #region 序列管理

    private bool CanPlayTransition()
    {
        Debug.Log("[EarthTransition] CanPlayTransition: " + (mainCameraTransform != null && fogController != null));
        return mainCameraTransform != null && fogController != null;
    }

    private void BeginSequence()
    {
        KillCurrentSequence();
        _transitionSequence = DOTween.Sequence();
    }

    private void KillCurrentSequence()
    {
        if (_transitionSequence != null && _transitionSequence.IsActive())
        {
            _transitionSequence.Kill();
        }

        _transitionSequence = null;
    }

    private static void NotifyTransitionToPlateMapCompleted()
    {
        EventManager eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            return;
        }

        eventManager.TriggerTransitionToPlateMapCompleted();
    }

    private static void NotifyTransitionToEarthCompleted()
    {
        EventManager eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            return;
        }

        eventManager.TriggerTransitionToEarthCompleted();
    }

    private void CacheEarthCameraLocalPos()
    {
        if (mainCameraTransform == null)
        {
            return;
        }

        _cachedEarthCameraLocalPos = mainCameraTransform.localPosition;
        _hasCachedEarthCameraPos = true;
    }

    private Vector3 GetEarthReturnLocalPos()
    {
        return _hasCachedEarthCameraPos ? _cachedEarthCameraLocalPos : mainCameraTransform.localPosition;
    }

    #endregion
}
