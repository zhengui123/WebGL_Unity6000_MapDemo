using UnityEngine;

/// <summary>
/// 挂在相机架（如 CameraPivot）：滚轮沿子相机局部 Y 缩放；由 MapController 在双击旋转结束后调用固定拉近。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("目标相机（子物体）")]
    [SerializeField] private Transform _cameraTransform;

    [Header("缩放（沿相机局部 Y）")]
    [SerializeField] private float _zoomSpeed = 200f;
    [SerializeField] private float _minZoomY = 500f;
    [SerializeField] private float _maxZoomY = 5000f;
    [SerializeField] private float _zoomSmoothTime = 0.1f;

    [Header("双击拉近（固定局部 Y）")]
    [Tooltip("双击旋转对准完成后，相机局部 Y 拉到此固定值")]
    [SerializeField] private float _doubleClickFixedLocalY = 800f;

    private float _targetZoomY;
    private float _currentZoomY;
    private float _zoomVelocity;
    private bool _zoomControlEnabled = true;
    /// <summary>运行时下限（省聚焦可暂时低于 Inspector 的 Min，便于大小省拉开距离）。</summary>
    private float _activeMinZoomY;
    private float _activeMaxZoomY;

    /// <summary>为 false 时暂停滚轮与 SmoothDamp（板块聚焦 DOTween 期间使用）。</summary>
    public bool ZoomControlEnabled
    {
        get => _zoomControlEnabled;
        set => _zoomControlEnabled = value;
    }

    private void Awake()
    {
        if (_cameraTransform == null && transform.childCount > 0)
        {
            _cameraTransform = transform.GetChild(0);
        }

        _activeMinZoomY = _minZoomY;
        _activeMaxZoomY = _maxZoomY;

        if (_cameraTransform != null)
        {
            _targetZoomY = _cameraTransform.localPosition.y;
            _currentZoomY = _targetZoomY;
        }
    }

    private void LateUpdate()
    {
        HandleZoomInput();
        ApplyZoom();
    }

    private void HandleZoomInput()
    {
        if (_cameraTransform == null || !_zoomControlEnabled)
        {
            return;
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) < Mathf.Epsilon)
        {
            return;
        }

        _targetZoomY -= wheel * _zoomSpeed;
        _targetZoomY = Mathf.Clamp(_targetZoomY, _activeMinZoomY, _activeMaxZoomY);
    }

    public float MinZoomY => _activeMinZoomY;
    public float MaxZoomY => _activeMaxZoomY;

    /// <summary>
    /// 当前相机沿局部 Y 的距离（与滚轮缩放一致，越小越近）。
    /// </summary>
    public float CurrentCameraLocalY
    {
        get
        {
            return _cameraTransform != null ? _cameraTransform.localPosition.y : float.MaxValue;
        }
    }

    /// <summary>设置目标缩放局部 Y（供板块模块点击拉近等）。</summary>
    /// <param name="immediate">为 true 时立即到位，否则走 SmoothDamp。</param>
    /// <param name="clampToLimits">为 false 时不钳到默认 Min/Max，并临时放开滚轮下限至该值。</param>
    public void SetTargetZoomY(float localY, bool immediate = false, bool clampToLimits = true)
    {
        if (_cameraTransform == null)
        {
            return;
        }

        if (clampToLimits)
        {
            _targetZoomY = Mathf.Clamp(localY, _activeMinZoomY, _activeMaxZoomY);
        }
        else
        {
            _targetZoomY = localY;
            // 允许省聚焦拉到低于 Inspector Min 后，滚轮仍可从这个高度往外推
            _activeMinZoomY = Mathf.Min(_minZoomY, localY);
            _activeMaxZoomY = Mathf.Max(_maxZoomY, localY);
        }

        if (!immediate)
        {
            return;
        }

        _currentZoomY = _targetZoomY;
        _zoomVelocity = 0f;
        Vector3 localPos = _cameraTransform.localPosition;
        localPos.y = _currentZoomY;
        _cameraTransform.localPosition = localPos;
    }

    /// <summary>还原到 Inspector 配置的缩放上下限（退出省聚焦时调用）。</summary>
    public void ResetZoomLimitOverrides()
    {
        _activeMinZoomY = _minZoomY;
        _activeMaxZoomY = _maxZoomY;
    }

    /// <summary>
    /// 双击旋转结束后由 MapController 调用：将相机拉近到固定的局部 Y。
    /// </summary>
    public void ApplyDoubleClickFixedZoom()
    {
        if (_cameraTransform == null)
        {
            return;
        }

        _targetZoomY = Mathf.Clamp(_doubleClickFixedLocalY, _activeMinZoomY, _activeMaxZoomY);
        _currentZoomY = _targetZoomY;
        _zoomVelocity = 0f;

        Vector3 localPos = _cameraTransform.localPosition;
        localPos.y = _currentZoomY;
        _cameraTransform.localPosition = localPos;
    }

    private void ApplyZoom()
    {
        if (_cameraTransform == null || !_zoomControlEnabled)
        {
            return;
        }

        _currentZoomY = Mathf.SmoothDamp(_currentZoomY, _targetZoomY, ref _zoomVelocity, _zoomSmoothTime);
        Vector3 localPos = _cameraTransform.localPosition;
        localPos.y = _currentZoomY;
        _cameraTransform.localPosition = localPos;
    }
}
