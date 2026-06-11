using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 鼠标点击拖拽：绕 Y 轴左右旋转目标物体。点击区域由 Inspector 手动挂载的 GameObject（需带 Collider）决定。
/// </summary>
[DisallowMultipleComponent]
public class MouseDragYawRotate : MonoBehaviour
{
    [Header("旋转目标")]
    [SerializeField] private Transform _rotateTarget;

    [Header("射线检测")]
    [Tooltip("可拖拽点击区域（需带 Collider），如 CarCollider")]
    [SerializeField] private GameObject _dragHitObject;
    [SerializeField] private Camera _raycastCamera;
    [SerializeField] private LayerMask _raycastLayerMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float _raycastMaxDistance = 500f;
    [SerializeField] private bool _ignorePointerOverUI = true;

    [Header("旋转")]
    [SerializeField] private float _yawSensitivity = 3f;
    [SerializeField] private bool _useWorldSpace = true;
    [Tooltip("旋转跟随平滑时间（秒），越小越跟手")]
    [SerializeField] private float _rotationSmoothTime = 0.08f;

    private bool _isDragging;
    private Quaternion _targetRotation;
    private Quaternion startRotation;
    private bool _hasTargetRotation;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        if (_rotateTarget == null)
        {
            _rotateTarget = transform;
        }

        if (_raycastCamera == null)
        {
            _raycastCamera = Camera.main;
        }
        startRotation = _rotateTarget.rotation;

        SyncTargetRotation();
    }

    private void Update()
    {
        if (_rotateTarget == null || _raycastCamera == null || _dragHitObject == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDrag();
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
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
        if (_ignorePointerOverUI && IsPointerOverUI())
        {
            return;
        }

        Ray ray = _raycastCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _raycastLayerMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (!IsValidColliderHit(hit))
        {
            return;
        }

        _isDragging = true;
        SyncTargetRotation();
    }

    private bool IsValidColliderHit(RaycastHit hit)
    {
        Transform hitTransform = hit.collider.transform;
        Transform dragRoot = _dragHitObject.transform;
        return hitTransform == dragRoot || hitTransform.IsChildOf(dragRoot);
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

    private void SyncTargetRotation()
    {
        if (_rotateTarget == null)
        {
            return;
        }

        _targetRotation = startRotation;
        _hasTargetRotation = true;
    }

    private void ResetTargetRotation()
    {
        _targetRotation = startRotation;
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

    /// <summary>运行时切换是否允许拖拽旋转。</summary>
    public void SetDragEnabled(bool enabled)
    {
        if (!enabled)
        {
            _isDragging = false;
            SyncTargetRotation();
        }
            SyncTargetRotation();

        this.enabled = enabled;
    }
}
