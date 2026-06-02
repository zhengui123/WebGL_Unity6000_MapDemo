using UnityEngine;

/// <summary>
/// 地图对外 API：触发地球/板块过渡，并可通过 <see cref="EventManager"/> 订阅动画结束事件。
/// </summary>
public class MapApi : UnitySingle<MapApi>
{

    public void Start()
    {
        Debug.Log("MapApi Start");
        EventManager.Instance.OnTransitionToPlateMapCompleted += ()=>{
            Debug.Log("地球 → 板块过渡动画全部播放完毕");
        };
            
        EventManager.Instance.OnTransitionToEarthCompleted += ()=>{
            Debug.Log("板块 → 地球过渡动画全部播放完毕");
        };

        EventManager.Instance.OnPlateMapDisplayFocus += moduleName =>
        {
            Debug.Log("板块模块开始聚焦：" + moduleName);
        };

        EventManager.Instance.OnPlateMapFocusModuleCompleted += moduleName =>
        {
            Debug.Log("板块模块聚焦动画完成：" + moduleName);
        };

        EventManager.Instance.OnPlateMapRestoreCameraCompleted += () =>
        {
            Debug.Log("板块相机还原动画完成");
        };
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TransitionToPlateMap();
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            TransitionToEarth();
        }
    }

    /// <summary>播放地球 → 板块过渡（与EarthTransition.TransitionToPlateMap一致）。</summary>
    public void TransitionToPlateMap()
    {
        EarthTransition earthTransition = EarthTransition.Instance;
        if (earthTransition == null)
        {
            Debug.LogWarning("[MapApi] 未找到 EarthTransition，无法切换到板块地图。");
            return;
        }

        earthTransition.TransitionToPlateMap();
    }

    /// <summary>播放板块 → 地球过渡（与"EarthTransition.TransitionToEarth"一致）。</summary>
    public void TransitionToEarth()
    {
        EarthTransition earthTransition = EarthTransition.Instance;
        if (earthTransition == null)
        {
            Debug.LogWarning("[MapApi] 未找到 EarthTransition，无法切换到地球。");
            return;
        }

        earthTransition.TransitionToEarth();
    }

    /// <summary>
    /// 聚焦指定板块模块（<paramref name="moduleName"/> 默认对应场景中 GameObject 名，如 polySurface3）。
    /// </summary>
    public bool FocusPlateMapModule(string moduleName)
    {
        PlateMapDisplayController controller = PlateMapDisplayController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapDisplayController，无法聚焦模块。");
            return false;
        }

        return controller.FocusModule(moduleName);
    }

    /// <summary>还原板块相机至首次聚焦前的位姿。</summary>
    public bool RestorePlateMapCamera()
    {
        PlateMapDisplayController controller = PlateMapDisplayController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[MapApi] 未找到 PlateMapDisplayController，无法还原相机。");
            return false;
        }

        return controller.RestoreCameraPosition();
    }
}
