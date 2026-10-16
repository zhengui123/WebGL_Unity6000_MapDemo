using System;
using UnityEngine;

/// <summary>
/// 按操控层级开关背景物体：地球/国家/省级显示，车辆/零部件/攻击链路隐藏。默认开启。
/// 请挂到任意常驻物体，并将背景根拖到 <see cref="_backgroundRoot"/>。
/// 背景根未指定或已销毁时直接跳过，异常不向外抛，避免打断同事件上的层级跳转。
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
        em.OnPlateMapFocusModuleCompleted += ShowOnProvinceFocusCompleted;
        em.OnPlateMapRestoreCameraCompleted += ApplyVisibility;
        em.OnPlateToVehicleViewTransitionStarted += HideOnProvinceToVehicleStarted;
        em.OnPlateToVehicleViewTransitionCompleted += ApplyVisibilityNamed;
        em.OnVehicleToPlateViewTransitionCompleted += ShowOnProvinceFocusCompleted;
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
            em.OnPlateMapFocusModuleCompleted -= ShowOnProvinceFocusCompleted;
            em.OnPlateMapRestoreCameraCompleted -= ApplyVisibility;
            em.OnPlateToVehicleViewTransitionStarted -= HideOnProvinceToVehicleStarted;
            em.OnPlateToVehicleViewTransitionCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToPlateViewTransitionCompleted -= ShowOnProvinceFocusCompleted;
            em.OnVehicleToPartTransitionCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToPartTransitionReverseCompleted -= ApplyVisibilityNamed;
            em.OnVehicleToAttackPathTransitionCompleted -= ApplyVisibility;
            em.OnAttackPathToVehicleTransitionCompleted -= ApplyVisibility;
            em.OnAttackPathToPartTransitionCompleted -= ApplyVisibilityAttackPathToPart;
        }

        _subscribed = false;
    }

    private void ShowOnProvinceFocusCompleted(string _)
    {
        SafeSetVisible(true);
    }

    private void HideOnProvinceToVehicleStarted(string _)
    {
        SafeSetVisible(false);
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
        SafeSetVisible(ShouldShow());
    }

    /// <summary>背景根不存在或显隐失败时只记日志，不把异常抛给 EventManager 的其它订阅者。</summary>
    private void SafeSetVisible(bool show)
    {
        try
        {
            SetVisible(show);
        }
        catch (Exception exception)
        {
            LogManager.LogFeatureWarning(
                $"[BackgroundByControlState] 背景显隐失败，已忽略，不影响层级跳转：{exception.Message}");
        }
    }

    private void SetVisible(bool show)
    {
        if (_backgroundRoot == null)
        {
            return;
        }

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
