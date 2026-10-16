using UnityEngine;

/// <summary>
/// 按操控层级开关背景物体：地球/国家/省级显示，车辆/零部件/攻击链路隐藏。默认开启。
/// 请挂到任意常驻物体，并将背景根拖到 <see cref="_backgroundRoot"/>。
/// </summary>
[DisallowMultipleComponent]
public class BackgroundByControlState : MonoBehaviour
{
    [Tooltip("背景根物体（手动拖拽）")]
    [SerializeField] private GameObject _backgroundRoot;

    private bool _subscribed;

    private void OnEnable()
    {
        Subscribe();
        ApplyVisibility();
    }

    private void Start()
    {
        ApplyVisibility();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnTransitionToPlateMapCompleted += ApplyVisibility;
        em.OnTransitionToEarthCompleted += ApplyVisibility;
        em.OnPlateMapFocusModuleCompleted += ApplyVisibilityNamed;
        em.OnPlateMapRestoreCameraCompleted += ApplyVisibility;
        em.OnPlateToVehicleViewTransitionCompleted += ApplyVisibilityNamed;
        em.OnVehicleToPlateViewTransitionCompleted += ApplyVisibilityNamed;
        em.OnVehicleToPartTransitionCompleted += ApplyVisibilityNamed;
        em.OnVehicleToPartTransitionReverseCompleted += ApplyVisibilityNamed;
        em.OnVehicleToAttackPathTransitionCompleted += ApplyVisibility;
        em.OnAttackPathToVehicleTransitionCompleted += ApplyVisibility;
        em.OnAttackPathToPartTransitionCompleted += ApplyVisibilityAttackPathToPart;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        EventManager em = EventManager.Instance;
        if (em != null)
        {
            em.OnTransitionToPlateMapCompleted -= ApplyVisibility;
            em.OnTransitionToEarthCompleted -= ApplyVisibility;
            em.OnPlateMapFocusModuleCompleted -= ApplyVisibilityNamed;
            em.OnPlateMapRestoreCameraCompleted -= ApplyVisibility;
            em.OnPlateToVehicleViewTransitionCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToPlateViewTransitionCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToPartTransitionCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToPartTransitionReverseCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToAttackPathTransitionCompleted -= ApplyVisibility;
            em.OnAttackPathToVehicleTransitionCompleted -= ApplyVisibility;
            em.OnAttackPathToPartTransitionCompleted -= ApplyVisibilityAttackPathToPart;
        }

        _subscribed = false;
    }

    private void ApplyVisibilityNamed(string _)
    {
        ApplyVisibility();
    }

    private void ApplyVisibilityAttackPathToPart(string _, string __)
    {
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_backgroundRoot == null)
        {
            return;
        }

        bool show = ShouldShow();
        if (_backgroundRoot.activeSelf != show)
        {
            _backgroundRoot.SetActive(show);
        }
    }

    /// <summary>地球（世界地图）、国家、省级显示；车辆/零部件/攻击链路隐藏。无 GameManager 时默认显示。</summary>
    private static bool ShouldShow()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return true;
        }

        GameManager.ControlState state = gm.CurrentState;
        return state != GameManager.ControlState.VehicleLevel
            && state != GameManager.ControlState.PartLevel
            && state != GameManager.ControlState.AttackPathLevel;
    }
}
