using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 操控层级输入导航：
/// 省级单击/触摸进入车辆大屏；Escape/Backspace/Android 返回键回到上一级。
/// 双击进下一级已移除；显式接口 <see cref="TryTransitionToNextLevel"/> 仍保留给 MapApi/Android。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ControlStateHierarchyTransitionController))]
public class ControlStateHierarchyInputNavigation : MonoBehaviour
{
    [Header("输入导航（省级单击进车辆 / 返回上一级）")]
    [SerializeField] private bool _enableInputNavigation = true;
    [Tooltip("抬起时相对按下位置的最大位移（像素），超过则不算点击")]
    [SerializeField] private float _clickMaxDragPixels = 12f;
    [SerializeField] private bool _useInstantTransition = false;

    private ControlStateHierarchyTransitionController _transitionController;

    private Vector2 _pointerDownPosition;
    private int _activePointerId = -1;
    private bool _hasActivePointer;

    private void Awake()
    {
        _transitionController = GetComponent<ControlStateHierarchyTransitionController>();
    }

    private void Update()
    {
        if (!_enableInputNavigation)
        {
            return;
        }

        HandleInputNavigation();
    }

    /// <summary>
    /// 进入层级下一级（供 MapApi / Android 显式调用；手势双击已不再触发）。
    /// </summary>
    public bool TryTransitionToNextLevel()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 GameManager，无法进入下一级。");
            return false;
        }

        if (_transitionController == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 ControlStateHierarchyTransitionController。");
            return false;
        }

        GameManager.ControlState current = manager.CurrentState;
        if (!ControlStateHierarchyAdjacency.TryGetNextState(current, out GameManager.ControlState next))
        {
            Debug.Log($"[ControlStateHierarchyInputNavigation] 当前 {current} 无下一级。");
            return false;
        }

        return _transitionController.TransitionToState(_useInstantTransition, next);
    }

    /// <summary>省级单击/触摸：进入车辆大屏。</summary>
    public bool TryEnterVehicleFromProvinceClick()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 GameManager，无法进入车辆大屏。");
            return false;
        }

        if (manager.CurrentState != GameManager.ControlState.ProvinceLevel)
        {
            return false;
        }

        if (_transitionController == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 ControlStateHierarchyTransitionController。");
            return false;
        }

        return _transitionController.TransitionToState(
            _useInstantTransition,
            GameManager.ControlState.VehicleLevel);
    }

    /// <summary>返回上一级；PC 为 Escape/Backspace，Android 为系统返回键（映射为 Escape）。</summary>
    public bool TryTransitionToPreviousLevel()
    {
        return TryTransitionToPreviousLevel(_useInstantTransition);
    }

    /// <summary>返回上一级，可指定是否瞬时过渡。</summary>
    public bool TryTransitionToPreviousLevel(bool useInstantTransition)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 GameManager，无法返回上一级。");
            return false;
        }

        if (_transitionController == null)
        {
            Debug.LogWarning("[ControlStateHierarchyInputNavigation] 未找到 ControlStateHierarchyTransitionController。");
            return false;
        }

        GameManager.ControlState current = manager.CurrentState;
        if (!ControlStateHierarchyAdjacency.TryGetPreviousState(current, out GameManager.ControlState previous))
        {
            Debug.Log($"[ControlStateHierarchyInputNavigation] 当前 {current} 无上一级。");
            return false;
        }

        return _transitionController.TransitionToState(useInstantTransition, previous);
    }

    private void HandleInputNavigation()
    {
        if (_transitionController != null && _transitionController.IsBootstrapping)
        {
            return;
        }

        if (IsPointerOverUi())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            TryTransitionToPreviousLevel();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            BeginPointer(Input.mousePosition, -1);
        }

        if (Input.GetMouseButtonUp(0))
        {
            TryHandlePointerUp(Input.mousePosition, -1);
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
            {
                BeginPointer(touch.position, touch.fingerId);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                TryHandlePointerUp(touch.position, touch.fingerId);
            }
        }
    }

    private void BeginPointer(Vector2 screenPosition, int pointerId)
    {
        _pointerDownPosition = screenPosition;
        _activePointerId = pointerId;
        _hasActivePointer = true;
    }

    private void TryHandlePointerUp(Vector2 screenPosition, int pointerId)
    {
        if (!_hasActivePointer || pointerId != _activePointerId)
        {
            return;
        }

        _hasActivePointer = false;

        if (Vector2.Distance(screenPosition, _pointerDownPosition) > _clickMaxDragPixels)
        {
            return;
        }

        TryEnterVehicleFromProvinceClick();
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (eventSystem.IsPointerOverGameObject(touch.fingerId))
                {
                    return true;
                }
            }

            return false;
        }

        return eventSystem.IsPointerOverGameObject();
    }

    /// <summary>从场景中的层级跳转控制器上查找输入导航组件。</summary>
    public static ControlStateHierarchyInputNavigation FindFromTransitionController()
    {
        ControlStateHierarchyTransitionController controller =
            ControlStateHierarchyTransitionController.Instance;
        if (controller == null)
        {
            return null;
        }

        ControlStateHierarchyInputNavigation navigation =
            controller.GetComponent<ControlStateHierarchyInputNavigation>();
        if (navigation != null)
        {
            return navigation;
        }

        return controller.gameObject.AddComponent<ControlStateHierarchyInputNavigation>();
    }
}

/// <summary>操控层级相邻关系（仅用于输入导航的上一级 / 下一级）。</summary>
internal static class ControlStateHierarchyAdjacency
{
    /// <summary>主干下一级：地球→国家→省→车辆→零件。</summary>
    public static bool TryGetNextState(GameManager.ControlState current, out GameManager.ControlState next)
    {
        switch (current)
        {
            case GameManager.ControlState.EarthLevel:
                next = GameManager.ControlState.CountryLevel;
                return true;
            case GameManager.ControlState.CountryLevel:
                next = GameManager.ControlState.ProvinceLevel;
                return true;
            case GameManager.ControlState.ProvinceLevel:
                next = GameManager.ControlState.VehicleLevel;
                return true;
            case GameManager.ControlState.VehicleLevel:
                next = GameManager.ControlState.PartLevel;
                return true;
            default:
                next = current;
                return false;
        }
    }

    /// <summary>返回上一级；零件/攻击路径均回到车辆级。</summary>
    public static bool TryGetPreviousState(GameManager.ControlState current, out GameManager.ControlState previous)
    {
        switch (current)
        {
            case GameManager.ControlState.CountryLevel:
                previous = GameManager.ControlState.EarthLevel;
                return true;
            case GameManager.ControlState.ProvinceLevel:
                previous = GameManager.ControlState.CountryLevel;
                return true;
            case GameManager.ControlState.VehicleLevel:
                previous = GameManager.ControlState.ProvinceLevel;
                return true;
            case GameManager.ControlState.PartLevel:
            case GameManager.ControlState.AttackPathLevel:
                previous = GameManager.ControlState.VehicleLevel;
                return true;
            default:
                previous = current;
                return false;
        }
    }
}
