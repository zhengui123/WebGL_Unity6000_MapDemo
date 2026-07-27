using System;
using UnityEngine;
using UnityEngine.Serialization;
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
    [Tooltip("板块全国视图下相机本地坐标")]
    [SerializeField] private Vector3 plateViewLocalPos = new Vector3(0f, 800f, 0f);
    [Tooltip("关闭「手动板块位置」时：将 AllPlateMap 沿相机前方放置的距离")]
    [SerializeField] private float plateMapCenterDistance = 800f;

    [Header("AllPlateMap 初始位置")]
    [Tooltip("开启：使用下方手动局部坐标重置 AllPlateMap（仅国内）；关闭：沿相机前方自动生成世界位置")]
    [SerializeField] private bool _useManualPlateMapPosition = false;
    [Tooltip("开启手动时写入 AllPlateMap 的局部坐标（相对父节点，不改旋转/缩放）；仅国内使用")]
    [FormerlySerializedAs("_manualPlateMapWorldPosition")]
    [SerializeField] private Vector3 _manualPlateMapLocalPosition = Vector3.zero;
    [Tooltip("开启后，国外板块优先使用下方 Config 中的自定义 local；关闭则国外一律相机前方自动")]
    [SerializeField] private bool _useForeignPlateMapPositionConfig = true;
    [Tooltip("国外各大板块 AllPlateMap 局部坐标表；未配置的国外板块走相机前方自动")]
    [SerializeField] private EarthPlateMapPositionConfig _foreignPlateMapPositionConfig;

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
            ApplyPlateMapInitialPosition();
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

    /// <summary>
    /// 重置 AllPlateMap 位置。
    /// 无参时按 <see cref="WorldMapRegionContext"/> 当前板块同步（国内外）。
    /// </summary>
    public void ApplyPlateMapInitialPosition()
    {
        string plateCode = null;
        if (WorldMapRegionContext.IsInitialized)
        {
            plateCode = WorldMapRegionContext.PlateCode;
        }

        ApplyPlateMapInitialPosition(plateCode);
    }

    /// <param name="plateCode">国外大板块 code（如 EAST_ASIA）；空或 "0" 视为国内。</param>
    public void ApplyPlateMapInitialPosition(string plateCode)
    {
        if (plateMapObj == null)
        {
            return;
        }

        if (IsForeignPlateCode(plateCode))
        {
            ApplyForeignPlateMapPosition(plateCode.Trim());
            return;
        }

        ApplyDomesticPlateMapPosition();
    }

    /// <summary>国外板块位置配置资源（编辑器可赋）。</summary>
    public EarthPlateMapPositionConfig ForeignPlateMapPositionConfig
    {
        get => _foreignPlateMapPositionConfig;
        set => _foreignPlateMapPositionConfig = value;
    }

    /// <summary>是否启用国外自定义 AllPlateMap 初始位置（Config）。</summary>
    public bool UseForeignPlateMapPositionConfig
    {
        get => _useForeignPlateMapPositionConfig;
        set => _useForeignPlateMapPositionConfig = value;
    }

    /// <summary>当前 AllPlateMap 物体（编辑器保存坐标用）。</summary>
    public GameObject PlateMapObj => plateMapObj;

    private static bool IsForeignPlateCode(string plateCode)
    {
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            return false;
        }

        string key = plateCode.Trim();
        return !string.Equals(key, "0", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(key, WorldMapRegionCodeTable.DomesticNationalCode, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyForeignPlateMapPosition(string plateCode)
    {
        if (_useForeignPlateMapPositionConfig &&
            _foreignPlateMapPositionConfig != null &&
            _foreignPlateMapPositionConfig.TryGetLocalPosition(plateCode, out Vector3 localPosition))
        {
            plateMapObj.transform.localPosition = localPosition;
            Debug.Log(
                $"[EarthTransition] AllPlateMap 国外配置 | code={plateCode} | local={localPosition}");
            return;
        }

        string reason = !_useForeignPlateMapPositionConfig
            ? "开关已关闭"
            : "未配置";
        ApplyAutoPlateMapPositionAlongCamera(
            $"[EarthTransition] AllPlateMap 国外{reason}，相机前方自动 | code={plateCode}");
    }

    private void ApplyDomesticPlateMapPosition()
    {
        if (_useManualPlateMapPosition)
        {
            plateMapObj.transform.localPosition = _manualPlateMapLocalPosition;
            Debug.Log($"[EarthTransition] AllPlateMap 使用手动局部坐标：{_manualPlateMapLocalPosition}");
            return;
        }

        ApplyAutoPlateMapPositionAlongCamera("[EarthTransition] AllPlateMap 自动初始位置");
    }

    private void ApplyAutoPlateMapPositionAlongCamera(string logPrefix)
    {
        if (mainCameraTransform == null)
        {
            return;
        }

        Vector3 viewCenterWorldPos =
            mainCameraTransform.position + mainCameraTransform.forward * plateMapCenterDistance;
        plateMapObj.transform.position = viewCenterWorldPos;
        Debug.Log($"{logPrefix}：{viewCenterWorldPos}");
    }

    /// <summary>运行时设置 AllPlateMap 手动局部坐标，并开启手动开关（不改旋转/缩放）。</summary>
    public void SetManualPlateMapLocalPosition(Vector3 localPosition)
    {
        _manualPlateMapLocalPosition = localPosition;
        _useManualPlateMapPosition = true;
    }

    public bool UseManualPlateMapPosition
    {
        get => _useManualPlateMapPosition;
        set => _useManualPlateMapPosition = value;
    }

    public Vector3 ManualPlateMapLocalPosition
    {
        get => _manualPlateMapLocalPosition;
        set => _manualPlateMapLocalPosition = value;
    }

    public Vector3 PlateViewLocalPos
    {
        get => plateViewLocalPos;
        set => plateViewLocalPos = value;
    }

    public float PlateMapCenterDistance
    {
        get => plateMapCenterDistance;
        set => plateMapCenterDistance = Mathf.Max(1f, value);
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
        PlateMapDisplayController plateDisplay = PlateMapDisplayController.Instance;
        plateDisplay?.RestoreAllModulesAlphaImmediate();

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
