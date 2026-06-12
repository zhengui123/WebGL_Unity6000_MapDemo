using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 脚本启用后，鼠标左键拖拽即可绕 Y 轴旋转目标；启用时记录初始旋转，支持重置。
/// </summary>
[DisallowMultipleComponent]
public class MouseDragYawRotate : MonoBehaviour
{
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
    private Quaternion _startRotation;
    private Quaternion _targetRotation;
    private bool _hasTargetRotation;

    public bool IsDragging => _isDragging;

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

    private void Update()
    {
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
        // 从当前姿态继续累加，避免每次按下跳回初始旋转
        _targetRotation = _rotateTarget.rotation;
        _hasTargetRotation = true;
    }

    /// <summary>结束拖拽，清理按下状态，不影响已旋转到的角度。</summary>
    private void EndDrag()
    {
        _isDragging = false;
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
