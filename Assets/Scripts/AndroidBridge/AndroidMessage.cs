using UnityEngine;

/// <summary>
/// Android → Unity 层级跳转请求体（JSON 字段名需与此一致）。
/// </summary>
[System.Serializable]
public struct TransitionToControlStateRequest
{
    /// <summary>目标操控级别：0 地球级、1 国家级、2 省级、3 车辆级、4 零件级、5 攻击路径级。</summary>
    public int targetState;

    /// <summary>（可空）省级行政区名称，用于省级 ↔ 车辆阶段（如「山东」）。</summary>
    public string provinceName;

    /// <summary>（可空）3D 板块模块名（场景中 GameObject 名）。</summary>
    public string provinceModuleName;

    /// <summary>（可空）车辆零件 GameObject 名。</summary>
    public string partName;

    /// <summary>（可空）是否瞬时过渡（各过渡动画时长临时置 0）。</summary>
    public bool useInstantTransition;
}

/// <summary>
/// Unity → Android 操控级别跳转通知（JSON 字段名需与此一致）。
/// </summary>
[System.Serializable]
public struct ControlStateTransitionNotify
{
    /// <summary>起始操控级别：0~5。</summary>
    public int from;

    /// <summary>目标操控级别：0~5。</summary>
    public int to;
}

/// <summary>Android → Unity 大屏自动轮播开关。</summary>
[System.Serializable]
public struct BigScreenAutoCarouselRequest
{
    public bool enabled;
}

/// <summary>
/// Unity 与 Android 宿主双向通信。场景内需有名为 AndroidBridge 的物体并挂载本脚本。
/// </summary>
public class AndroidMessage : MonoBehaviour
{
    public const string BridgeObjectName = "AndroidBridge";

    public static AndroidMessage Instance { get; private set; }

    [SerializeField] private string lastAndroidMessage = "";

    public string LastAndroidMessage => lastAndroidMessage;

    public event System.Action<string> OnAndroidMessageReceived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (gameObject.name != BridgeObjectName)
            gameObject.name = BridgeObjectName;
    }

    private void OnEnable()
    {
        // 订阅各级别跳转开始事件，通知 Android from → to
        SubscribeControlStateEvents();
    }

    private void OnDisable()
    {
        UnsubscribeControlStateEvents();
    }

    #region Unity → Android（对应 MainActivity 三个测试方法）

    public void CallAndroidShowToast(string message)
    {
        CallActivity("onUnityShowToast", message ?? "");
    }

    public void CallAndroidUpdateNativeTitle(string message)
    {
        CallActivity("onUnityUpdateNativeTitle", message ?? "");
    }

    public void CallAndroidRequestDataSync(string message)
    {
        CallActivity("onUnityRequestDataSync", message ?? "");
    }

    /// <summary>通知 Android 操控级别跳转开始：from → to（JSON，级别 0~5）。</summary>
    public void CallAndroidControlStateTransition(int fromState, int toState)
    {
        if (!TryValidateControlState(fromState, nameof(CallAndroidControlStateTransition)) ||
            !TryValidateControlState(toState, nameof(CallAndroidControlStateTransition)))
        {
            return;
        }

        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = fromState,
            to = toState,
        });
        CallActivity("onUnityControlStateTransition", json);
    }

    private void CallActivity(string method, string arg)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call(method, arg);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AndroidMessage] {method} failed: {e}");
        }
#else
        Debug.Log($"[AndroidMessage] Editor mock {method}: {arg}");
#endif
    }

    #endregion

    #region Unity → Android 操控级别事件
    // 级别：0 地球、1 国家、2 省级、3 车辆、4 零件、5 攻击路径
    // 均在对应过渡「开始」时通知 Android：{"from":x,"to":y}

    private void SubscribeControlStateEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnTransitionToPlateMapStarted += HandleTransitionToPlateMapStarted;
        em.OnTransitionToEarthStarted += HandleTransitionToEarthStarted;
        em.OnPlateMapDisplayFocus += HandlePlateMapDisplayFocus;
        em.OnPlateMapRestoreCameraStarted += HandlePlateMapRestoreCameraStarted;
        em.OnPlateToVehicleViewTransitionStarted += HandlePlateToVehicleViewTransitionStarted;
        em.OnVehicleToPlateViewTransitionStarted += HandleVehicleToPlateViewTransitionStarted;
        em.OnVehicleToPartTransitionStarted += HandleVehicleToPartTransitionStarted;
        em.OnVehicleToPartTransitionReverseStarted += HandleVehicleToPartTransitionReverseStarted;
        em.OnVehicleToAttackPathTransitionStarted += HandleVehicleToAttackPathTransitionStarted;
        em.OnAttackPathToVehicleTransitionStarted += HandleAttackPathToVehicleTransitionStarted;
    }

    private void UnsubscribeControlStateEvents()
    {
        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnTransitionToPlateMapStarted -= HandleTransitionToPlateMapStarted;
        em.OnTransitionToEarthStarted -= HandleTransitionToEarthStarted;
        em.OnPlateMapDisplayFocus -= HandlePlateMapDisplayFocus;
        em.OnPlateMapRestoreCameraStarted -= HandlePlateMapRestoreCameraStarted;
        em.OnPlateToVehicleViewTransitionStarted -= HandlePlateToVehicleViewTransitionStarted;
        em.OnVehicleToPlateViewTransitionStarted -= HandleVehicleToPlateViewTransitionStarted;
        em.OnVehicleToPartTransitionStarted -= HandleVehicleToPartTransitionStarted;
        em.OnVehicleToPartTransitionReverseStarted -= HandleVehicleToPartTransitionReverseStarted;
        em.OnVehicleToAttackPathTransitionStarted -= HandleVehicleToAttackPathTransitionStarted;
        em.OnAttackPathToVehicleTransitionStarted -= HandleAttackPathToVehicleTransitionStarted;
    }

    private void NotifyControlStateTransition(GameManager.ControlState from, GameManager.ControlState to)
    {
        CallAndroidControlStateTransition((int)from, (int)to);
    }

    // 0 → 1
    private void HandleTransitionToPlateMapStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.EarthLevel, GameManager.ControlState.CountryLevel);
    }

    // 1 → 0
    private void HandleTransitionToEarthStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.CountryLevel, GameManager.ControlState.EarthLevel);
    }

    // 1 → 2
    private void HandlePlateMapDisplayFocus(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.CountryLevel, GameManager.ControlState.ProvinceLevel);
    }

    // 2 → 1
    private void HandlePlateMapRestoreCameraStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.ProvinceLevel, GameManager.ControlState.CountryLevel);
    }

    // 2 → 3
    private void HandlePlateToVehicleViewTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.ProvinceLevel, GameManager.ControlState.VehicleLevel);
    }

    // 3 → 2
    private void HandleVehicleToPlateViewTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.ProvinceLevel);
    }

    // 3 → 4
    private void HandleVehicleToPartTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.PartLevel);
    }

    // 4 → 3
    private void HandleVehicleToPartTransitionReverseStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.PartLevel, GameManager.ControlState.VehicleLevel);
    }

    // 3 → 5
    private void HandleVehicleToAttackPathTransitionStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.AttackPathLevel);
    }

    // 5 → 3
    private void HandleAttackPathToVehicleTransitionStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.AttackPathLevel, GameManager.ControlState.VehicleLevel);
    }

    private static bool TryValidateControlState(int controlState, string callerName)
    {
        if (System.Enum.IsDefined(typeof(GameManager.ControlState), controlState))
        {
            return true;
        }

        Debug.LogWarning($"[AndroidMessage] {callerName}: 无效的 controlState={controlState}，有效范围为 0~5。");
        return false;
    }

    #endregion

    #region Android → Unity（UnitySendMessage，方法名需一致）

    public void OnAndroidNotifyA(string message)
    {
        HandleFromAndroid("A", message);
    }

    public void OnAndroidNotifyB(string message)
    {
        HandleFromAndroid("B", message);
    }

 

    public void OnDataSyncResult(string message)
    {
        HandleFromAndroid("SyncResult", message);
    }

    private void HandleFromAndroid(string channel, string message)
    {
        lastAndroidMessage = $"[{channel}] {message}";
        Debug.Log("[AndroidMessage] " + lastAndroidMessage);
        OnAndroidMessageReceived?.Invoke(lastAndroidMessage);
    }

    /// <summary>播放地球 → 板块过渡。</summary>
    public void TransitionToPlateMap()
    {
        MapApi.Instance.TransitionToPlateMap();
    }

    /// <summary>播放板块 → 地球过渡。</summary>
    public void TransitionToEarth()
    {
        MapApi.Instance.TransitionToEarth();
    }

    /// <summary>
    /// 聚焦指定板块模块,默认对应场景中 GameObject 名，如 polySurface2）。
    /// </summary>
    public void FocusPlateMapModule(string moduleName)
    {
        MapApi.Instance.FocusPlateMapModule(moduleName);
    }

    /// <summary>还原板块相机至首次聚焦前的位姿。</summary>
    public void RestorePlateMapCamera()
    {
        MapApi.Instance.RestorePlateMapCamera();
    }

    /// <summary>
    /// Android 调用：按 JSON 参数执行层级跳转。
    /// UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
    /// </summary>
    /// <param name="json">
    /// 示例：{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3","partName":"","useInstantTransition":false}
    /// </param>
    public void TransitionToControlState(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[AndroidMessage] TransitionToControlState: JSON 为空。");
            return;
        }

        TransitionToControlStateRequest request = JsonUtility.FromJson<TransitionToControlStateRequest>(json);
        bool ok = MapApi.Instance.TransitionToControlState(
            request.targetState,
            NormalizeOptionalString(request.provinceName),
            NormalizeOptionalString(request.provinceModuleName),
            NormalizeOptionalString(request.partName),
            request.useInstantTransition);

        if (!ok)
        {
            Debug.LogWarning($"[AndroidMessage] TransitionToControlState 启动失败: {json}");
        }
    }

    /// <summary>Android 调用：进入操控层级下一级（等同双击）。</summary>
    public void TransitionToNextControlState()
    {
        if (!MapApi.Instance.TransitionToNextControlState())
        {
            Debug.LogWarning("[AndroidMessage] TransitionToNextControlState 启动失败。");
        }
    }

    /// <summary>Android 调用：返回操控层级上一级（等同系统返回键）。</summary>
    public void TransitionToPreviousControlState()
    {
        if (!MapApi.Instance.TransitionToPreviousControlState())
        {
            Debug.LogWarning("[AndroidMessage] TransitionToPreviousControlState 启动失败。");
        }
    }

    /// <summary>Android 调用：开启/关闭四个大屏自动轮播。json 示例：{"enabled":true}</summary>
    public void SetBigScreenAutoCarouselEnabled(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[AndroidMessage] SetBigScreenAutoCarouselEnabled: JSON 为空。");
            return;
        }

        BigScreenAutoCarouselRequest request = JsonUtility.FromJson<BigScreenAutoCarouselRequest>(json);
        if (!MapApi.Instance.SetBigScreenAutoCarouselEnabled(request.enabled))
        {
            Debug.LogWarning($"[AndroidMessage] SetBigScreenAutoCarouselEnabled 失败: {json}");
        }
    }

    private static string NormalizeOptionalString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
 
    #endregion
}
