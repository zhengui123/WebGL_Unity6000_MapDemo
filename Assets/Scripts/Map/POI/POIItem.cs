using UnityEngine;

/// <summary>
/// UI POI：绑定三维世界坐标与所属板块；板块关闭/淡出时同步隐藏。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class POIItem : MonoBehaviour
{
    private const float PlateFullyOpaqueThreshold = 0.999f;

    [Header("三维绑定")]
    [SerializeField] private Vector3 _worldPosition;

    [Tooltip("开启后持续将三维坐标转换为 Canvas 二维坐标")]
    [SerializeField] private bool _followWorldPosition = true;

    [Header("板块绑定")]
    [Tooltip("所属板块根；关闭或 inactive 时 POI 隐藏")]
    [SerializeField] private Transform _boundPlate;

    [Tooltip("所属显示模块；开始淡出时 POI 立即关闭")]
    [SerializeField] private PlateMapDisplayModule _boundDisplayModule;

    private RectTransform _rectTransform;
    private Canvas _canvas;
    private Camera _uiCamera;
    private CanvasGroup _canvasGroup;
    private bool _visible = true;
    private float _lastPlateAlpha = 1f;
    private bool _hiddenByPlateFade;

    public Vector3 WorldPosition
    {
        get => _worldPosition;
        set => _worldPosition = value;
    }

    public bool FollowWorldPosition
    {
        get => _followWorldPosition;
        set => _followWorldPosition = value;
    }

    public Transform BoundPlate => _boundPlate;

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        CacheRefs();
        if (_followWorldPosition)
        {
            UpdateCanvasPosition();
        }
    }

    private void LateUpdate()
    {
        if (!_followWorldPosition)
        {
            return;
        }

        UpdateCanvasPosition();
    }

    /// <summary>绑定世界坐标，并可立即刷新一次 UI 位置。</summary>
    public void BindWorldPosition(Vector3 worldPosition, bool updateNow = true)
    {
        _worldPosition = worldPosition;
        if (updateNow)
        {
            UpdateCanvasPosition();
        }
    }

    /// <summary>绑定所属板块对象；优先取同物体上的 PlateMapDisplayModule。</summary>
    public void BindPlate(Transform plateRoot)
    {
        _boundPlate = plateRoot;
        _boundDisplayModule = null;
        _hiddenByPlateFade = false;
        _lastPlateAlpha = 1f;
        if (plateRoot != null)
        {
            _boundDisplayModule = plateRoot.GetComponent<PlateMapDisplayModule>();
            if (_boundDisplayModule == null)
            {
                _boundDisplayModule = plateRoot.GetComponentInChildren<PlateMapDisplayModule>(true);
            }

            if (_boundDisplayModule != null)
            {
                _lastPlateAlpha = _boundDisplayModule.CurrentAlpha;
                // 绑定瞬间若已非全不透明，视为已处于淡出/隐藏态
                _hiddenByPlateFade = _lastPlateAlpha < PlateFullyOpaqueThreshold;
            }
        }
    }

    /// <summary>设置跟随开关；开启时立即刷新一次。</summary>
    public void SetFollowWorldPosition(bool follow)
    {
        _followWorldPosition = follow;
        if (follow)
        {
            UpdateCanvasPosition();
        }
    }

    /// <summary>将世界坐标转为所属 Canvas 本地坐标并写入 RectTransform。</summary>
    public void UpdateCanvasPosition()
    {
        CacheRefs();
        if (_rectTransform == null || _canvas == null)
        {
            return;
        }

        if (!IsBoundPlateVisible())
        {
            SetVisible(false);
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(_worldPosition);
        bool behindCamera = screenPoint.z <= 0f;
        if (behindCamera)
        {
            SetVisible(false);
            return;
        }

        Camera eventCamera = ResolveEventCamera();
        RectTransform parentRect = _rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            parentRect = _canvas.transform as RectTransform;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        _rectTransform.anchoredPosition = localPoint;
    }

    /// <summary>
    /// 板块未绑定时视为可见；
    /// 绑定后：inactive 关闭，或板块 alpha 一开始下降（开始淡出）立即关闭；
    /// 仅当 alpha 回到全不透明时再显示。
    /// </summary>
    private bool IsBoundPlateVisible()
    {
        if (_boundPlate == null)
        {
            return true;
        }

        if (!_boundPlate.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (_boundDisplayModule != null)
        {
            float alpha = _boundDisplayModule.CurrentAlpha;
            // 开始淡出：alpha 一旦下降就立刻关 POI，不等待淡出结束
            if (alpha < _lastPlateAlpha - 0.0001f || alpha < PlateFullyOpaqueThreshold)
            {
                _hiddenByPlateFade = true;
            }

            if (alpha >= PlateFullyOpaqueThreshold)
            {
                _hiddenByPlateFade = false;
            }

            _lastPlateAlpha = alpha;
            if (_hiddenByPlateFade)
            {
                return false;
            }
        }

        return true;
    }

    private void CacheRefs()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
        }

        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (_canvas != null && _uiCamera == null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            _uiCamera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
        }
    }

    private Camera ResolveEventCamera()
    {
        if (_canvas == null)
        {
            return null;
        }

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _uiCamera != null ? _uiCamera : Camera.main;
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;
        // 不用 SetActive：隐藏后 LateUpdate 停转，相机转回来无法恢复
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
