using System;
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

    /// <summary>（可空）业务零部件 ID，用于进入零件级、零件切换、攻击路径 → 零件。</summary>
    public string partId;

    /// <summary>（可空）是否瞬时过渡（各过渡动画时长临时置 0）。</summary>
    public bool useInstantTransition;
}

/// <summary>设置默认省请求体（传省 code）。</summary>
[System.Serializable]
public struct DefaultProvinceCodeRequest
{
    /// <summary>省级 adcode，如 330000（浙江）。</summary>
    public string provinceCode;
}

/// <summary>
/// 大屏跳转状态（<see cref="ControlStateTransitionNotify.status"/> 取值）。
/// 表示本次层级过渡由何种大屏业务场景触发，与操控级别、轮播态势类型无关。
/// </summary>
public enum BigScreenStatus
{
    /// <summary>普通跳转（默认状态，包含未区分触发源的常规跳转）。</summary>
    NormalNavigation = 0,

    /// <summary>信息跳转（宿主或用户主动查看信息触发的跳转）。</summary>
    InformationNavigation = 1,

    /// <summary>威胁下钻（威胁态势联动下钻）。</summary>
    ThreatDrillDown = 2,
}

/// <summary>
/// Unity → Android 操控级别跳转通知（JSON 字段名需与此一致）。
/// </summary>
[System.Serializable]
public struct ControlStateTransitionNotify
{
    /// <summary>起始操控级别：0~5；过渡完成通知时为 -1。</summary>
    public int from;

    /// <summary>目标操控级别：0~5。</summary>
    public int to;

    /// <summary>（可空）业务零部件 ID；零件相关过渡完成/切换通知时可带值。</summary>
    public string partId;

    /// <summary>
    /// 大屏跳转状态，取值见 <see cref="BigScreenStatus"/>：
    /// 0 普通跳转、1 信息跳转、2 威胁下钻。
    /// 当前为预留字段，Unity 暂统一回传 0，后续按实际触发源填充。
    /// </summary>
    public int status;
}

/// <summary>Android → Unity 大屏自动轮播开关。</summary>
[System.Serializable]
public struct BigScreenAutoCarouselRequest
{
    public bool enabled;
}

/// <summary>Android / WebGL → Unity：开启车辆热力图指定时段轮询。</summary>
[System.Serializable]
public struct VehicleHeatmapSpecifiedTimePollingRequest
{
    public string startTime;
    public string endTime;
}

/// <summary>Android → Unity 设置车辆 Y 轴旋转角度。</summary>
[System.Serializable]
public struct SetCarYawRotationRequest
{
    /// <summary>目标 Yaw 角度，0~360。</summary>
    public float yawAngle;

    /// <summary>是否立即到位；JSON 省略时 Unity 侧默认 false（平滑旋转）。</summary>
    public bool instant;
}

/// <summary>Unity → Android 车辆 Yaw 旋转变化通知。</summary>
[System.Serializable]
public struct CarYawRotationNotify
{
    /// <summary>当前 Yaw 角度，0~360。</summary>
    public float yawAngle;

    /// <summary>true 表示拖拽中连续回调，false 表示松手或 API 设角。</summary>
    public bool isDragging;
}

/// <summary>
/// Unity 与 Android 宿主双向通信。场景内需有名为 AndroidBridge 的物体并挂载本脚本。
/// </summary>
public class AndroidMessage : MonoBehaviour
{
    public const string BridgeObjectName = "AndroidBridge";

    /// <summary>操控级别过渡完成通知时 <see cref="ControlStateTransitionNotify.from"/> 的约定值。</summary>
    public const int ControlStateTransitionCompletedFrom = -1;

    public static AndroidMessage Instance { get; private set; }

    [SerializeField] private string lastAndroidMessage = "";

    public string LastAndroidMessage => lastAndroidMessage;

    public event System.Action<string> OnAndroidMessageReceived;

    /// <summary>车辆 Yaw 变化已通知 Android（Editor 调试用）。</summary>
    public event System.Action<CarYawRotationNotify> OnCarYawRotationNotified;

    private MouseDragYawRotate _carYawRotate;

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
        TrySubscribeCarYawRotation();
    }

    private void OnDisable()
    {
        UnsubscribeControlStateEvents();
        UnsubscribeCarYawRotation();
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

    /// <summary>通知 Android 车辆 Yaw 旋转变化（JSON）。</summary>
    public void CallAndroidCarYawRotationChanged(float yawAngle, bool isDragging)
    {
        var notify = new CarYawRotationNotify
        {
            yawAngle = NormalizeYawAngle(yawAngle),
            isDragging = isDragging,
        };
        string json = JsonUtility.ToJson(notify);
        Debug.Log($"[AndroidMessage] 车辆 Yaw 回调: {json}");
        OnCarYawRotationNotified?.Invoke(notify);
        CallActivity("onUnityCarYawRotationChanged", json);
    }

    /// <summary>通知 Android 操控级别跳转开始：from → to（JSON，级别 0~5）。</summary>
    public void CallAndroidControlStateTransition(int fromState, int toState, string partId = null)
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
            partId = partId ?? string.Empty,
            status = ResolveNotifyBigScreenStatus(),
        });
        Debug.Log($"[AndroidMessage] 操控级别过渡开始: {fromState} → {toState}, json={json}");
        CallActivity("onUnityControlStateTransition", json);
    }

    /// <summary>通知 Android 操控级别过渡完成：from=-1，to 为已就绪级别（JSON）。</summary>
    public void CallAndroidControlStateTransitionCompleted(int toState, string partId = null)
    {
        if (!TryValidateControlState(toState, nameof(CallAndroidControlStateTransitionCompleted)))
        {
            return;
        }

        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = ControlStateTransitionCompletedFrom,
            to = toState,
            partId = partId ?? string.Empty,
            status = ResolveNotifyBigScreenStatus(),
        });
        Debug.Log($"[AndroidMessage] 操控级别过渡完成: to={toState}, json={json}");
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
        em.OnPartToPartTransitionStarted += HandlePartToPartTransitionStarted;
        em.OnVehicleToAttackPathTransitionStarted += HandleVehicleToAttackPathTransitionStarted;
        em.OnAttackPathToVehicleTransitionStarted += HandleAttackPathToVehicleTransitionStarted;
        em.OnAttackPathToPartTransitionStarted += HandleAttackPathToPartTransitionStarted;

        em.OnTransitionToPlateMapCompleted += HandleTransitionToPlateMapCompleted;
        em.OnTransitionToEarthCompleted += HandleTransitionToEarthCompleted;
        em.OnPlateMapFocusModuleCompleted += HandlePlateMapFocusModuleCompleted;
        em.OnPlateMapRestoreCameraCompleted += HandlePlateMapRestoreCameraCompleted;
        em.OnPlateToVehicleViewTransitionCompleted += HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionCompleted += HandleVehicleToPlateViewTransitionCompleted;
        em.OnVehicleToPartTransitionCompleted += HandleVehicleToPartTransitionCompleted;
        em.OnVehicleToPartTransitionReverseCompleted += HandleVehicleToPartTransitionReverseCompleted;
        em.OnPartToPartTransitionCompleted += HandlePartToPartTransitionCompleted;
        em.OnVehicleToAttackPathTransitionCompleted += HandleVehicleToAttackPathTransitionCompleted;
        em.OnAttackPathToVehicleTransitionCompleted += HandleAttackPathToVehicleTransitionCompleted;
        em.OnAttackPathToPartTransitionCompleted += HandleAttackPathToPartTransitionCompleted;
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
        em.OnPartToPartTransitionStarted -= HandlePartToPartTransitionStarted;
        em.OnVehicleToAttackPathTransitionStarted -= HandleVehicleToAttackPathTransitionStarted;
        em.OnAttackPathToVehicleTransitionStarted -= HandleAttackPathToVehicleTransitionStarted;
        em.OnAttackPathToPartTransitionStarted -= HandleAttackPathToPartTransitionStarted;

        em.OnTransitionToPlateMapCompleted -= HandleTransitionToPlateMapCompleted;
        em.OnTransitionToEarthCompleted -= HandleTransitionToEarthCompleted;
        em.OnPlateMapFocusModuleCompleted -= HandlePlateMapFocusModuleCompleted;
        em.OnPlateMapRestoreCameraCompleted -= HandlePlateMapRestoreCameraCompleted;
        em.OnPlateToVehicleViewTransitionCompleted -= HandlePlateToVehicleViewTransitionCompleted;
        em.OnVehicleToPlateViewTransitionCompleted -= HandleVehicleToPlateViewTransitionCompleted;
        em.OnVehicleToPartTransitionCompleted -= HandleVehicleToPartTransitionCompleted;
        em.OnVehicleToPartTransitionReverseCompleted -= HandleVehicleToPartTransitionReverseCompleted;
        em.OnPartToPartTransitionCompleted -= HandlePartToPartTransitionCompleted;
        em.OnVehicleToAttackPathTransitionCompleted -= HandleVehicleToAttackPathTransitionCompleted;
        em.OnAttackPathToVehicleTransitionCompleted -= HandleAttackPathToVehicleTransitionCompleted;
        em.OnAttackPathToPartTransitionCompleted -= HandleAttackPathToPartTransitionCompleted;
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

    // 4 → 4
    private void HandlePartToPartTransitionStarted(string _, string partId)
    {
        CallAndroidControlStateTransition(
            (int)GameManager.ControlState.PartLevel,
            (int)GameManager.ControlState.PartLevel,
            partId);
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

    // 5 → 4
    private void HandleAttackPathToPartTransitionStarted(string _, string partId)
    {
        CallAndroidControlStateTransition(
            (int)GameManager.ControlState.AttackPathLevel,
            (int)GameManager.ControlState.PartLevel,
            partId);
    }

    private void NotifyControlStateTransitionCompleted(GameManager.ControlState to, string partId = null)
    {
        CallAndroidControlStateTransitionCompleted((int)to, partId);
    }

    private void HandleTransitionToPlateMapCompleted()
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.CountryLevel);
    }

    private void HandleTransitionToEarthCompleted()
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.EarthLevel);
    }

    private void HandlePlateMapFocusModuleCompleted(string _)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.ProvinceLevel);
    }

    private void HandlePlateMapRestoreCameraCompleted()
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.CountryLevel);
    }

    private void HandlePlateToVehicleViewTransitionCompleted(string _)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.VehicleLevel);
    }

    private void HandleVehicleToPlateViewTransitionCompleted(string _)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.ProvinceLevel);
    }

    private void HandleVehicleToPartTransitionCompleted(string _)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.PartLevel, ResolveCurrentPartId());
    }

    private void HandleVehicleToPartTransitionReverseCompleted(string _)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.VehicleLevel);
    }

    private void HandlePartToPartTransitionCompleted(string _, string partId)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.PartLevel, partId);
    }

    private void HandleVehicleToAttackPathTransitionCompleted()
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.AttackPathLevel);
    }

    private void HandleAttackPathToVehicleTransitionCompleted()
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.VehicleLevel);
    }

    private void HandleAttackPathToPartTransitionCompleted(string _, string partId)
    {
        NotifyControlStateTransitionCompleted(GameManager.ControlState.PartLevel, partId);
    }

    private static string ResolveCurrentPartId()
    {
        VehicleToPartTransitionController controller = VehicleToPartTransitionController.Instance;
        if (controller == null)
        {
            return null;
        }

        string partId = controller.LastPartId;
        return string.IsNullOrEmpty(partId) ? null : partId;
    }

    /// <summary>过渡通知 JSON 中的大屏跳转状态（<see cref="BigScreenStatus"/>）；预留，暂回传 0。</summary>
    private static int ResolveNotifyBigScreenStatus()
    {
        // TODO: 按过渡触发源区分 NormalNavigation / InformationNavigation / ThreatDrillDown
        return (int)BigScreenStatus.NormalNavigation;
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
    /// 示例：{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3","useInstantTransition":false}
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
            NormalizeOptionalString(request.partId),
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

    /// <summary>
    /// Android 调用：暂停游戏。
    /// UnitySendMessage("AndroidBridge", "PauseGame", "");
    /// </summary>
    public void PauseGame()
    {
        if (!MapApi.Instance.PauseGame())
        {
            Debug.LogWarning("[AndroidMessage] PauseGame 失败。");
        }
    }

    /// <summary>
    /// Android 调用：恢复游戏。
    /// UnitySendMessage("AndroidBridge", "ResumeGame", "");
    /// </summary>
    public void ResumeGame()
    {
        if (!MapApi.Instance.ResumeGame())
        {
            Debug.LogWarning("[AndroidMessage] ResumeGame 失败。");
        }
    }

    /// <summary>
    /// Android 调用：主动退出威胁下钻（保持当前级别，进入冷却）。
    /// UnitySendMessage("AndroidBridge", "ExitThreatDrill", "");
    /// </summary>
    public void ExitThreatDrill()
    {
        if (!MapApi.Instance.ExitThreatDrill())
        {
            Debug.LogWarning("[AndroidMessage] ExitThreatDrill 失败。");
        }
    }

    /// <summary>
    /// Android 调用：刷新威胁冷却倒计时（仅冷却中有效）。
    /// UnitySendMessage("AndroidBridge", "RefreshThreatCooldown", "");
    /// </summary>
    public void RefreshThreatCooldown()
    {
        if (!MapApi.Instance.RefreshThreatCooldown())
        {
            Debug.LogWarning("[AndroidMessage] RefreshThreatCooldown 失败（可能未在冷却中）。");
        }
    }

    /// <summary>
    /// Android 调用：设置默认省（传省 code，自动查找省名）。
    /// UnitySendMessage("AndroidBridge", "SetDefaultProvinceCode", "330000");
    /// 也可传 JSON：{"provinceCode":"330000"}
    /// </summary>
    public void SetDefaultProvinceCode(string arg)
    {
        string provinceCode = ExtractProvinceCodeArg(arg);
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            Debug.LogWarning("[AndroidMessage] SetDefaultProvinceCode: provinceCode 为空。");
            return;
        }

        if (!MapApi.Instance.SetDefaultProvinceCode(provinceCode))
        {
            Debug.LogWarning($"[AndroidMessage] SetDefaultProvinceCode 失败: {provinceCode}");
        }
    }

    private static string ExtractProvinceCodeArg(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return null;
        }

        string trimmed = arg.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            DefaultProvinceCodeRequest request = JsonUtility.FromJson<DefaultProvinceCodeRequest>(trimmed);
            return NormalizeOptionalString(request.provinceCode);
        }

        return trimmed;
    }

    /// <summary>
    /// Android 调用：关闭车辆 UI（停止零部件轮播 + 关闭连线面板）。
    /// UnitySendMessage("AndroidBridge", "CloseCarUI", "");
    /// </summary>
    public void CloseCarUI()
    {
        MapApi.Instance.CloseCarVehicleDataUi();
    }

    /// <summary>
    /// Android 调用：关闭告警面板 GJ_Panel。
    /// UnitySendMessage("AndroidBridge", "CloseGJPanel", "");
    /// </summary>
    public void CloseGJPanel()
    {
        if (!MapApi.Instance.CloseGJPanel())
        {
            Debug.LogWarning("[AndroidMessage] CloseGJPanel 失败。");
        }
    }

    /// <summary>
    /// Android 调用：开启车辆热力图指定时段轮询（isReplay=true）。
    /// UnitySendMessage("AndroidBridge", "StartVehicleHeatmapSpecifiedTimePolling", json);
    /// json 示例：{"startTime":"2026-06-30 00:00:00","endTime":"2026-06-30 23:00:00"}
    /// </summary>
    public void StartVehicleHeatmapSpecifiedTimePolling(string json)
    {
        Debug.Log($"[AndroidMessage] StartVehicleHeatmapSpecifiedTimePolling 收到: {json}");

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[AndroidMessage] StartVehicleHeatmapSpecifiedTimePolling: JSON 为空。");
            return;
        }

        VehicleHeatmapSpecifiedTimePollingRequest request =
            JsonUtility.FromJson<VehicleHeatmapSpecifiedTimePollingRequest>(json);
        if (!MapApi.Instance.StartVehicleHeatmapSpecifiedTimePolling(request.startTime, request.endTime))
        {
            Debug.LogWarning($"[AndroidMessage] StartVehicleHeatmapSpecifiedTimePolling 失败: {json}");
        }
    }

    /// <summary>
    /// Android 调用：关闭指定时段轮询，恢复默认热力图轮询。
    /// UnitySendMessage("AndroidBridge", "StopVehicleHeatmapSpecifiedTimePolling", "");
    /// </summary>
    public void StopVehicleHeatmapSpecifiedTimePolling()
    {
        if (!MapApi.Instance.StopVehicleHeatmapSpecifiedTimePolling())
        {
            Debug.LogWarning("[AndroidMessage] StopVehicleHeatmapSpecifiedTimePolling 失败。");
        }
    }

    /// <summary>
    /// Android 调用：请求车辆态势双接口（防护状态 + 攻击链路）。
    /// UnitySendMessage("AndroidBridge", "RequestCarVehicleData", json);
    /// json 可传 "" 使用默认参数；示例：
    /// {"encryptVin":"ed49f47afa23e45b18d342767495643c","startTime":"","endTime":"2026-06-30 23:00:00"}
    /// </summary>
    public void RequestCarVehicleData(string json)
    {
        Debug.Log($"[AndroidMessage] RequestCarVehicleData 收到: {json}");

        if (string.IsNullOrWhiteSpace(json))
        {
            if (!MapApi.Instance.RequestCarVehicleData())
            {
                Debug.LogWarning("[AndroidMessage] RequestCarVehicleData 失败（默认参数）。");
            }

            return;
        }

        PartProtectionStatusRequest request = JsonUtility.FromJson<PartProtectionStatusRequest>(json);
        if (request == null)
        {
            Debug.LogWarning($"[AndroidMessage] RequestCarVehicleData: JSON 解析失败 | {json}");
            return;
        }

        if (!MapApi.Instance.RequestCarVehicleData(request.encryptVin, request.startTime, request.endTime))
        {
            Debug.LogWarning($"[AndroidMessage] RequestCarVehicleData 失败: {json}");
        }
    }

    /// <summary>
    /// Android 调用：请求事件溯源详情（getSourceEventDetail）。
    /// UnitySendMessage("AndroidBridge", "RequestSecurityEventDetail", json);
    /// json 可传 "" 使用默认参数；示例：
    /// {"eventId":"123dfdsafffff","processStartTime":"2026-06-30 17:41:23","processEndTime":"2026-06-30 17:41:23","tenantId":1}
    /// </summary>
    public void RequestSecurityEventDetail(string json)
    {
        Debug.Log($"[AndroidMessage] RequestSecurityEventDetail 收到: {json}");

        if (string.IsNullOrWhiteSpace(json))
        {
            if (!MapApi.Instance.RequestSecurityEventDetail())
            {
                Debug.LogWarning("[AndroidMessage] RequestSecurityEventDetail 失败（默认参数）。");
            }

            return;
        }

        SecurityEventDetailRequest request = JsonUtility.FromJson<SecurityEventDetailRequest>(json);
        if (request == null)
        {
            Debug.LogWarning($"[AndroidMessage] RequestSecurityEventDetail: JSON 解析失败 | {json}");
            return;
        }

        if (!MapApi.Instance.RequestSecurityEventDetail(
                request.eventId,
                request.processStartTime,
                request.processEndTime,
                request.tenantId))
        {
            Debug.LogWarning($"[AndroidMessage] RequestSecurityEventDetail 失败: {json}");
        }
    }

    /// <summary>
    /// Android 调用：设置车辆 Y 轴旋转角度。
    /// UnitySendMessage("AndroidBridge", "SetCarYawRotation", json);
    /// json 示例：{"yawAngle":90.0,"instant":false}
    /// </summary>
    public void SetCarYawRotation(string json)
    {
        Debug.Log($"[AndroidMessage] SetCarYawRotation 收到: {json}");

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[AndroidMessage] SetCarYawRotation: JSON 为空。");
            return;
        }

        if (!TryResolveCarYawRotate())
        {
            Debug.LogWarning("[AndroidMessage] SetCarYawRotation 失败：未找到车辆旋转控制器。");
            return;
        }

        SetCarYawRotationRequest request = JsonUtility.FromJson<SetCarYawRotationRequest>(json);
        bool instant = json.Contains("\"instant\"") && request.instant;
        _carYawRotate.SetYawAngle(request.yawAngle, instant, notify: true);
        Debug.Log($"[AndroidMessage] SetCarYawRotation 已应用: yaw={NormalizeYawAngle(request.yawAngle):F1}°, instant={instant}");
    }

    private void TrySubscribeCarYawRotation()
    {
        if (!TryResolveCarYawRotate())
        {
            return;
        }

        _carYawRotate.OnYawAngleChanged -= HandleCarYawAngleChanged;
        _carYawRotate.OnYawAngleChanged += HandleCarYawAngleChanged;
        Debug.Log("[AndroidMessage] 已订阅车辆 Yaw 旋转回调。");
    }

    private void UnsubscribeCarYawRotation()
    {
        if (_carYawRotate == null)
        {
            return;
        }

        _carYawRotate.OnYawAngleChanged -= HandleCarYawAngleChanged;
    }

    private void HandleCarYawAngleChanged(float yawAngle, bool isDragging)
    {
        CallAndroidCarYawRotationChanged(yawAngle, isDragging);
    }

    private bool TryResolveCarYawRotate()
    {
        if (_carYawRotate != null)
        {
            return true;
        }

        CarModelController carModelController = FindFirstObjectByType<CarModelController>();
        if (carModelController != null && carModelController.carModelRotateController != null)
        {
            _carYawRotate = carModelController.carModelRotateController;
            return true;
        }

        _carYawRotate = FindFirstObjectByType<MouseDragYawRotate>();
        return _carYawRotate != null;
    }

    private static float NormalizeYawAngle(float yawDegrees)
    {
        float normalized = yawDegrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static string NormalizeOptionalString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
 
    #endregion
}
