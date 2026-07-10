using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 脚本启用后，鼠标左键拖拽即可绕 Y 轴旋转目标；启用时记录初始旋转，支持重置。
/// </summary>
[DisallowMultipleComponent]
public class MouseDragYawRotate : MonoBehaviour
{
    private const float NotifyAngleThreshold = 0.5f;

    [Header("旋转目标")]
    [SerializeField] private Transform _rotateTarget;

    [Header("旋转")]
    [SerializeField] private float _yawSensitivity = 3f;
    [SerializeField] private bool _useWorldSpace = true;
    [Tooltip("旋转跟随平滑时间（秒），越小越跟手")]
    [SerializeField] private float _rotationSmoothTime = 0.08f;
    [Tooltip("指针在 UI 上时不响应拖拽")]
    [SerializeField] private bool _ignorePointerOverUI = true;

    private bool _isDragging;
    private bool _isApiSmoothing;
    private Quaternion _startRotation;
    private Quaternion _targetRotation;
    private bool _hasTargetRotation;
    private float _lastNotifiedYaw = float.NaN;

    public bool IsDragging => _isDragging;

    /// <summary>当前 Y 轴角度（0~360）。</summary>
    public float YawAngle => GetYawFromRotation(GetCurrentRotation());

    /// <summary>Yaw 变化回调：(角度 0~360, 是否拖拽中)。</summary>
    public event Action<float, bool> OnYawAngleChanged;

    private void Awake()
    {
        if (_rotateTarget == null)
        {
            _rotateTarget = transform;
        }
    }

    private void OnEnable()
    {
        if (_rotateTarget == null)
        {
            _rotateTarget = transform;
        }

        CacheStartRotation();
        EndDrag();
    }

    private void OnDisable()
    {
        EndDrag();
    }

    //同步测试
    // public bool isOpenMouse = true;
    private void Update()
    {
        //同步测试    
        // if(!isOpenMouse)
        // return;

        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDrag();
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
            return;
        }

        if (_isDragging && Input.GetMouseButton(0))
        {
            ApplyYawRotation(Input.GetAxis("Mouse X"));
        }
    }

    private void LateUpdate()
    {
        ApplySmoothRotation();
        TryNotifyYawDuringMotion();
    }

    private void TryBeginDrag()
    {
        if (_rotateTarget == null)
        {
            return;
        }

        if (_ignorePointerOverUI && IsPointerOverUI())
        {
            return;
        }

        _isDragging = true;
        _lastNotifiedYaw = float.NaN;
        // 从当前姿态继续累加，避免每次按下跳回初始旋转
        _targetRotation = _rotateTarget.rotation;
        _hasTargetRotation = true;
    }

    /// <summary>结束拖拽，清理按下状态，不影响已旋转到的角度。</summary>
    private void EndDrag()
    {
        if (_isDragging)
        {
            NotifyYawChanged(YawAngle, false);
            Debug.Log($"[MouseDragYawRotate] 拖拽结束, Yaw={YawAngle:F1}°");
        }

        _isDragging = false;
        _lastNotifiedYaw = float.NaN;
    }

    private void ApplyYawRotation(float mouseDeltaX)
    {
        if (Mathf.Abs(mouseDeltaX) < Mathf.Epsilon)
        {
            return;
        }

        float yaw = mouseDeltaX * _yawSensitivity * -1f;
        Quaternion delta = Quaternion.AngleAxis(yaw, _useWorldSpace ? Vector3.up : _rotateTarget.up);
        _targetRotation = _useWorldSpace ? delta * _targetRotation : _targetRotation * delta;
        _hasTargetRotation = true;
    }

    /// <summary>记录启用时的旋转，作为重置基准。</summary>
    private void CacheStartRotation()
    {
        if (_rotateTarget == null)
        {
            return;
        }

        _startRotation = _rotateTarget.rotation;
        _targetRotation = _startRotation;
        _hasTargetRotation = true;
        _lastNotifiedYaw = float.NaN;
    }

    /// <summary>将旋转目标还原为脚本最近一次启用时记录的姿态。</summary>
    public void ResetRotation()
    {
        EndDrag();

        if (_rotateTarget == null)
        {
            return;
        }

        _targetRotation = _startRotation;
        _hasTargetRotation = true;
        _rotateTarget.rotation = _startRotation;
        NotifyYawChanged(YawAngle, false);
        Debug.Log($"[MouseDragYawRotate] 重置旋转, Yaw={YawAngle:F1}°");
    }

    /// <summary>设置 Y 轴旋转角度（0~360）。</summary>
    /// <param name="yawDegrees">目标 Yaw 角度。</param>
    /// <param name="instant">是否立即到位（跳过平滑）；默认 false，走与拖拽相同的 Slerp。</param>
    /// <param name="notify">是否触发 <see cref="OnYawAngleChanged"/>。</param>
    public void SetYawAngle(float yawDegrees, bool instant = false, bool notify = true)
    {
        if (_rotateTarget == null)
        {
            Debug.LogWarning("[MouseDragYawRotate] SetYawAngle 失败：旋转目标为空。");
            return;
        }

        EndDrag();

        float normalizedYaw = NormalizeYaw(yawDegrees);
        SetTargetYaw(normalizedYaw);
        _isApiSmoothing = !instant && _rotationSmoothTime > 0f;

        if (instant || _rotationSmoothTime <= 0f)
        {
            _rotateTarget.rotation = _targetRotation;
            _isApiSmoothing = false;
        }

        Debug.Log($"[MouseDragYawRotate] 设置 Yaw={normalizedYaw:F1}°, instant={instant}");

        if (notify && !_isApiSmoothing)
        {
            _lastNotifiedYaw = float.NaN;
            NotifyYawChanged(YawAngle, false);
        }
        else if (notify)
        {
            _lastNotifiedYaw = float.NaN;
        }
    }

    private void SetTargetYaw(float normalizedYaw)
    {
        float currentYaw = GetYawFromRotation(_rotateTarget.rotation);
        float delta = Mathf.DeltaAngle(currentYaw, normalizedYaw);
        Quaternion deltaRotation = Quaternion.AngleAxis(delta, _useWorldSpace ? Vector3.up : _rotateTarget.up);
        _targetRotation = _useWorldSpace
            ? deltaRotation * _rotateTarget.rotation
            : _rotateTarget.rotation * deltaRotation;
        _hasTargetRotation = true;
    }

    private void ApplySmoothRotation()
    {
        if (_rotateTarget == null || !_hasTargetRotation)
        {
            return;
        }

        if (Quaternion.Angle(_rotateTarget.rotation, _targetRotation) < 0.01f)
        {
            _rotateTarget.rotation = _targetRotation;

            if (_isApiSmoothing)
            {
                _isApiSmoothing = false;
                _lastNotifiedYaw = float.NaN;
                NotifyYawChanged(YawAngle, false);
            }

            return;
        }

        if (_rotationSmoothTime <= 0f)
        {
            _rotateTarget.rotation = _targetRotation;
            return;
        }

        float blend = 1f - Mathf.Exp(-Time.deltaTime / _rotationSmoothTime);
        _rotateTarget.rotation = Quaternion.Slerp(_rotateTarget.rotation, _targetRotation, blend);
    }

    private void TryNotifyYawDuringMotion()
    {
        float currentYaw = GetYawFromRotation(_rotateTarget.rotation);

        if (_isDragging)
        {
            if (float.IsNaN(_lastNotifiedYaw)
                || Mathf.Abs(Mathf.DeltaAngle(_lastNotifiedYaw, currentYaw)) >= NotifyAngleThreshold)
            {
                NotifyYawChanged(currentYaw, true);
            }

            return;
        }

        if (!_isApiSmoothing)
        {
            return;
        }

        if (float.IsNaN(_lastNotifiedYaw)
            || Mathf.Abs(Mathf.DeltaAngle(_lastNotifiedYaw, currentYaw)) >= NotifyAngleThreshold)
        {
            NotifyYawChanged(currentYaw, false);
        }
    }

    private void NotifyYawChanged(float yawAngle, bool isDragging)
    {
        float normalizedYaw = NormalizeYaw(yawAngle);
        _lastNotifiedYaw = normalizedYaw;
        Debug.Log($"[MouseDragYawRotate] Yaw 变化: {normalizedYaw:F1}°, isDragging={isDragging}");
        OnYawAngleChanged?.Invoke(normalizedYaw, isDragging);

        //同步测试
        // if(!isOpenMouse)
        //         return;
        // string json = JsonUtility.ToJson(new SetCarYawRotationRequest
        // {
        //     yawAngle = yawAngle,
        //     instant = isDragging,
        // });
        // Debug.Log($"[DemoAndroidBridgeApiUIDemo] 模拟 Android 调用 SetCarYawRotation: {json}");
        // AndroidMessage.Instance.SetCarYawRotation(json);
    }

    private Quaternion GetCurrentRotation()
    {
        if (_rotateTarget == null)
        {
            return Quaternion.identity;
        }

        return _hasTargetRotation ? _targetRotation : _rotateTarget.rotation;
    }

    private static float GetYawFromRotation(Quaternion rotation)
    {
        float yaw = rotation.eulerAngles.y;
        return NormalizeYaw(yaw);
    }

    private static float NormalizeYaw(float yawDegrees)
    {
        float normalized = yawDegrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>运行时开关拖拽旋转；重新开启时会重新记录当前姿态为初始状态。</summary>
    public void SetDragEnabled(bool enabled)
    {
        if (!enabled)
        {
            EndDrag();
        }

        this.enabled = enabled;
    }
}
