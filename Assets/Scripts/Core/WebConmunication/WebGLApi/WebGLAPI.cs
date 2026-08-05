using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebGL 宿主页面与 Unity 双向通信（跨域 iframe 嵌入模式），公开方法与 <see cref="AndroidMessage"/> 对齐。
/// <para>Unity → 父页面：postMessage { source:"unity-webgl", method, message }。</para>
/// <para>父页面 → Unity：postMessage { source:"webgl-unity-parent", method, arg }，由 jslib 转发 SendMessage("WebGLAPI", ...)。</para>
/// <para>接口说明见同目录 <c>WebGL_Iframe_API.md</c> / <c>WebGL_Vue_Communication.md</c>；示例页见 vue-parent-demo。</para>
/// </summary>
public class WebGLAPI : MonoBehaviour
{
    public const string BridgeObjectName = "WebGLAPI";

    /// <summary>Unity → 父页面 postMessage 的 source 字段。</summary>
    public const string PostMessageSourceUnity = "unity-webgl";

    /// <summary>父页面 → Unity postMessage 的 source 字段。</summary>
    public const string PostMessageSourceParent = "webgl-unity-parent";

    /// <summary>Unity 桥接就绪通知（父页面收到后可安全 callUnity）。</summary>
    public const string HostReadyMethodName = "onUnityWebGLReady";

    public static WebGLAPI Instance { get; private set; }

    [Header("Demo（可选）")]
    [SerializeField] private Text showMessageText;

    [Header("通信日志")]
    [SerializeField] private bool _enableCommunicationLog = true;

    [SerializeField] private string lastHostMessage = "";

    public string LastHostMessage => lastHostMessage;

    public event Action<string> OnHostMessageReceived;

    /// <summary>车辆 Yaw 变化已通知宿主（Editor 调试用）。</summary>
    public event Action<CarYawRotationNotify> OnCarYawRotationNotified;

    /// <summary>父页面 / WebGL 宿主任意通信到达时触发（method, arg）。</summary>
    public static event Action<string, string> HostCommunicationReceived;

    private MouseDragYawRotate _carYawRotate;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CallHTMLHandler(string methodName, string message);

    [DllImport("__Internal")]
    private static extern void InitIframePostMessageBridge();

    [DllImport("__Internal")]
    private static extern void FlushIframePendingMessages();
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (gameObject.name != BridgeObjectName)
        {
            gameObject.name = BridgeObjectName;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        LogCommunication("系统", "InitIframePostMessageBridge", "初始化 iframe postMessage 桥接");
        InitIframePostMessageBridge();
#endif
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        NotifyHostReady();
#else
        LogCommunication("系统", "Editor", "非 WebGL 运行环境，通信接口仅输出 mock 日志");
#endif
    }

    /// <summary>通知父页面 Unity iframe 桥接已就绪，并冲刷排队中的父页面消息。</summary>
    public void NotifyHostReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FlushIframePendingMessages();
#endif
        LogCommunication("→ Host", HostReadyMethodName, string.Empty);
        CallHost(HostReadyMethodName, string.Empty);
    }

    private void LogCommunication(string direction, string method, string payload)
    {
        if (!_enableCommunicationLog)
        {
            return;
        }

        if (string.IsNullOrEmpty(payload))
        {
            Debug.Log($"[WebGLAPI] {direction} | {method}");
            return;
        }

        Debug.Log($"[WebGLAPI] {direction} | {method} | {payload}");
    }

    private void OnEnable()
    {
        SubscribeControlStateEvents();
        TrySubscribeCarYawRotation();
    }

    private void OnDisable()
    {
        UnsubscribeControlStateEvents();
        UnsubscribeCarYawRotation();
    }

    #region Unity → HTML（对应 MainActivity / 父页面 JS 回调）

    // public void CallAndroidShowToast(string message)
    // {
    //     CallHost("onUnityShowToast", message ?? string.Empty);
    // }

    // public void CallAndroidUpdateNativeTitle(string message)
    // {
    //     CallHost("onUnityUpdateNativeTitle", message ?? string.Empty);
    // }

    // public void CallAndroidRequestDataSync(string message)
    // {
    //     CallHost("onUnityRequestDataSync", message ?? string.Empty);
    // }

    /// <summary>通知宿主操控级别跳转开始：from → to（JSON，级别 0~5）。</summary>
    public void CallAndroidControlStateTransition(int fromState, int toState, string partId = null)
    {
        if (!TryValidateControlState(fromState, nameof(CallAndroidControlStateTransition)) ||
            !TryValidateControlState(toState, nameof(CallAndroidControlStateTransition)))
        {
            return;
        }

        string provinceCode = ResolveCurrentProvinceCode();
        string vin = ResolveCurrentVin();
        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = fromState,
            to = toState,
            status = ResolveNotifyBigScreenStatus(),
            provinceCode = provinceCode,
            vin = vin,
            partId = partId ?? string.Empty,
        });
        LogCommunication(
            "→ Host",
            "onUnityControlStateTransition",
            $"开始 {fromState}→{toState}, provinceCode={provinceCode}, vin={vin} | {json}");
        CallHost("onUnityControlStateTransition", json);
    }

    /// <summary>通知宿主操控级别过渡完成：from=-1，to 为已就绪级别（JSON）。</summary>
    public void CallAndroidControlStateTransitionCompleted(int toState, string partId = null)
    {
        if (!TryValidateControlState(toState, nameof(CallAndroidControlStateTransitionCompleted)))
        {
            return;
        }

        string provinceCode = ResolveCurrentProvinceCode();
        string vin = ResolveCurrentVin();
        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = AndroidMessage.ControlStateTransitionCompletedFrom,
            to = toState,
            status = ResolveNotifyBigScreenStatus(),
            provinceCode = provinceCode,
            vin = vin,
            partId = partId ?? string.Empty,
        });
        LogCommunication(
            "→ Host",
            "onUnityControlStateTransition",
            $"完成 to={toState}, provinceCode={provinceCode}, vin={vin} | {json}");
        CallHost("onUnityControlStateTransition", json);
    }

    /// <summary>通知宿主车辆 Yaw 旋转变化（JSON）。</summary>
    public void CallHostCarYawRotationChanged(float yawAngle, bool isDragging)
    {
        var notify = new CarYawRotationNotify
        {
            yawAngle = NormalizeYawAngle(yawAngle),
            isDragging = isDragging,
        };
        string json = JsonUtility.ToJson(notify);
        LogCommunication("→ Host", "onUnityCarYawRotationChanged", json);
        OnCarYawRotationNotified?.Invoke(notify);
        CallHost("onUnityCarYawRotationChanged", json);
    }

    private void CallHost(string method, string arg)
    {
        if (method != HostReadyMethodName
            && method != "onUnityControlStateTransition"
            && method != "onUnityCarYawRotationChanged")
        {
            LogCommunication("→ Host", method, arg);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            CallHTMLHandler(method, arg);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebGLAPI] {method} failed: {e}");
        }
#endif
    }

    #endregion

    #region Unity → HTML 操控级别事件

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

    private void HandleTransitionToPlateMapStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.EarthLevel, GameManager.ControlState.CountryLevel);
    }

    private void HandleTransitionToEarthStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.CountryLevel, GameManager.ControlState.EarthLevel);
    }

    private void HandlePlateMapDisplayFocus(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.CountryLevel, GameManager.ControlState.ProvinceLevel);
    }

    private void HandlePlateMapRestoreCameraStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.ProvinceLevel, GameManager.ControlState.CountryLevel);
    }

    private void HandlePlateToVehicleViewTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.ProvinceLevel, GameManager.ControlState.VehicleLevel);
    }

    private void HandleVehicleToPlateViewTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.ProvinceLevel);
    }

    private void HandleVehicleToPartTransitionStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.PartLevel);
    }

    private void HandleVehicleToPartTransitionReverseStarted(string _)
    {
        NotifyControlStateTransition(GameManager.ControlState.PartLevel, GameManager.ControlState.VehicleLevel);
    }

    private void HandlePartToPartTransitionStarted(string _, string partId)
    {
        CallAndroidControlStateTransition(
            (int)GameManager.ControlState.PartLevel,
            (int)GameManager.ControlState.PartLevel,
            partId);
    }

    private void HandleVehicleToAttackPathTransitionStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.VehicleLevel, GameManager.ControlState.AttackPathLevel);
    }

    private void HandleAttackPathToVehicleTransitionStarted()
    {
        NotifyControlStateTransition(GameManager.ControlState.AttackPathLevel, GameManager.ControlState.VehicleLevel);
    }

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

    /// <summary>
    /// 解析当前省份/国家 code：优先当前聚焦模块 → 进省缓存 → WorldMap 默认单元。
    /// 国内为省 adcode；国外为大屏国家/区域 code。
    /// </summary>
    private static string ResolveCurrentProvinceCode()
    {
        if (PlateProvinceFocusResolver.TryGetFocusedPlateProvinceCode(out string focusedCode) &&
            !string.IsNullOrWhiteSpace(focusedCode))
        {
            return focusedCode;
        }

        if (PlateProvinceFocusResolver.TryGetCachedProvinceCode(out string cachedCode) &&
            !string.IsNullOrWhiteSpace(cachedCode))
        {
            return cachedCode;
        }

        if (GameManager.Instance != null)
        {
            string code = GameManager.Instance.ResolveProvinceCode(null);
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }
        }

        if (MapApi.Instance != null)
        {
            string code = MapApi.Instance.GetDefaultProvinceCode();
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code;
        }

        return string.Empty;
    }

    /// <summary>解析当前车辆 VIN；优先使用车辆态势最近一次缓存。</summary>
    private static string ResolveCurrentVin()
    {
        CarVehicleDataStore store = CarVehicleDataStore.Instance;
        if (store == null || string.IsNullOrWhiteSpace(store.LastEncryptVin))
        {
            return string.Empty;
        }

        return store.LastEncryptVin;
    }

    /// <summary>
    /// 过渡通知 JSON 中的 <c>status</c>：当前
    /// <see cref="GameManager.BigScreenPlaybackState"/>。
    /// </summary>
    private static int ResolveNotifyBigScreenStatus()
    {
        GameManager gm = GameManager.Instance;
        return gm != null
            ? (int)gm.CurrentPlaybackState
            : (int)GameManager.BigScreenPlaybackState.Default;
    }

    private static bool TryValidateControlState(int controlState, string callerName)
    {
        if (Enum.IsDefined(typeof(GameManager.ControlState), controlState))
        {
            return true;
        }

        Debug.LogWarning($"[WebGLAPI] {callerName}: 无效的 controlState={controlState}，有效范围为 0~5。");
        return false;
    }

    #endregion

    #region HTML → Unity（SendMessage，方法名与 AndroidMessage 一致）


    


    /// <summary>
    /// 宿主（Vue 父页面）调用：按 JSON 参数执行层级跳转。
    /// 跨域 iframe：父页面 postMessage({ source:"webgl-unity-parent", method:"TransitionToControlState", arg: json })。
    /// </summary>
    /// <param name="json">
    /// 字段（区分大小写）：
    /// targetState(int, 必填) 目标级别 0~5：0 地球、1 国家、2 省级、3 车辆、4 零件、5 攻击路径；
    /// provinceCode(string, 可选) 省/国家 code（国内 adcode / 国外 SOC）；
    /// partId(string, 可选) 业务零部件 ID，用于进入零件级、零件切换、攻击路径 → 零件；
    /// useInstantTransition(bool, 可选) 是否跳过过渡动画，默认 false。
    ///
    /// JSON 示例 1 — 跳到省级并指定省 code：
    /// {"targetState":2,"provinceCode":"370000"}
    ///
    /// JSON 示例 2 — 仅跳到车辆级（其余走默认）：
    /// {"targetState":3}
    ///
    /// JSON 示例 3 — 跳到零件级：
    /// {"targetState":4,"partId":"IDC"}
    ///
    /// HTML 调用示例（同域调试，跨域请用 postMessage）：
    /// unityInstance.SendMessage('WebGLAPI', 'TransitionToControlState',
    ///   '{"targetState":2,"provinceCode":"370000"}');
    /// </param>
    public void TransitionToControlState(string json)
    {
        NotifyHostCommunicationReceived(nameof(TransitionToControlState), json);
        LogCommunication("← Host", nameof(TransitionToControlState), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] TransitionToControlState: JSON 为空。");
            return;
        }

        TransitionToControlStateRequest request = JsonUtility.FromJson<TransitionToControlStateRequest>(json);
        bool ok = MapApi.Instance.TransitionToControlState(
            request.targetState,
            NormalizeOptionalString(request.provinceCode),
            NormalizeOptionalString(request.partId),
            request.useInstantTransition);

        if (!ok)
        {
            Debug.LogWarning($"[WebGLAPI] TransitionToControlState 启动失败: {json}");
            return;
        }

        LogCommunication("← Host", nameof(TransitionToControlState), "已接受并启动");
    }

    public void TransitionToNextControlState()
    {
        NotifyHostCommunicationReceived(nameof(TransitionToNextControlState), string.Empty);
        LogCommunication("← Host", nameof(TransitionToNextControlState), string.Empty);

        if (!MapApi.Instance.TransitionToNextControlState())
        {
            Debug.LogWarning("[WebGLAPI] TransitionToNextControlState 启动失败。");
            return;
        }

        LogCommunication("← Host", nameof(TransitionToNextControlState), "已接受并启动");
    }

    public void TransitionToPreviousControlState()
    {
        NotifyHostCommunicationReceived(nameof(TransitionToPreviousControlState), string.Empty);
        LogCommunication("← Host", nameof(TransitionToPreviousControlState), string.Empty);

        if (!MapApi.Instance.TransitionToPreviousControlState())
        {
            Debug.LogWarning("[WebGLAPI] TransitionToPreviousControlState 启动失败。");
            return;
        }

        LogCommunication("← Host", nameof(TransitionToPreviousControlState), "已接受并启动");
    }

    /// <summary>宿主调用：开启/关闭四个大屏自动轮播。json 示例：{"enabled":true}</summary>
    public void SetBigScreenAutoCarouselEnabled(string json)
    {
        NotifyHostCommunicationReceived(nameof(SetBigScreenAutoCarouselEnabled), json);
        LogCommunication("← Host", nameof(SetBigScreenAutoCarouselEnabled), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] SetBigScreenAutoCarouselEnabled: JSON 为空。");
            return;
        }

        BigScreenAutoCarouselRequest request = JsonUtility.FromJson<BigScreenAutoCarouselRequest>(json);
        if (!MapApi.Instance.SetBigScreenAutoCarouselEnabled(request.enabled))
        {
            Debug.LogWarning($"[WebGLAPI] SetBigScreenAutoCarouselEnabled 失败: {json}");
            return;
        }

        LogCommunication("← Host", nameof(SetBigScreenAutoCarouselEnabled), $"enabled={request.enabled}");
    }

    /// <summary>宿主调用：暂停游戏。arg 传 ""。</summary>
    public void PauseGame()
    {
        NotifyHostCommunicationReceived(nameof(PauseGame), string.Empty);
        LogCommunication("← Host", nameof(PauseGame), string.Empty);

        if (!MapApi.Instance.PauseGame())
        {
            Debug.LogWarning("[WebGLAPI] PauseGame 失败。");
            return;
        }

        LogCommunication("← Host", nameof(PauseGame), "已暂停");
    }

    /// <summary>宿主调用：恢复游戏。arg 传 ""。</summary>
    public void ResumeGame()
    {
        NotifyHostCommunicationReceived(nameof(ResumeGame), string.Empty);
        LogCommunication("← Host", nameof(ResumeGame), string.Empty);

        if (!MapApi.Instance.ResumeGame())
        {
            Debug.LogWarning("[WebGLAPI] ResumeGame 失败。");
            return;
        }

        LogCommunication("← Host", nameof(ResumeGame), "已恢复");
    }

    /// <summary>
    /// 宿主调用：主动退出威胁下钻（保持当前级别，进入冷却）。arg 传 ""。
    /// </summary>
    public void ExitThreatDrill()
    {
        LogCommunication("← Host", nameof(ExitThreatDrill), string.Empty);

        if (!MapApi.Instance.ExitThreatDrill())
        {
            Debug.LogWarning("[WebGLAPI] ExitThreatDrill 失败。");
            return;
        }

        LogCommunication("← Host", nameof(ExitThreatDrill), "已退出并进入冷却");
    }

    /// <summary>
    /// 宿主调用：刷新威胁冷却倒计时（仅冷却中有效）。arg 传 ""。
    /// </summary>
    public void RefreshThreatCooldown()
    {
        LogCommunication("← Host", nameof(RefreshThreatCooldown), string.Empty);

        if (!MapApi.Instance.RefreshThreatCooldown())
        {
            Debug.LogWarning("[WebGLAPI] RefreshThreatCooldown 失败（可能未在冷却中）。");
            return;
        }

        LogCommunication("← Host", nameof(RefreshThreatCooldown), "已刷新冷却");
    }

    /// <summary>
    /// 宿主调用：设置世界地图国内外默认并立刻切换。
    /// JSON：{"regionMode":0,"foreignPlateCode":"","defaultUnitCode":"330000"}
    /// regionMode：0=国内，1=国外。
    /// </summary>
    public void SetWorldMapRegionDefaults(string json)
    {
        NotifyHostCommunicationReceived(nameof(SetWorldMapRegionDefaults), json);
        LogCommunication("← Host", nameof(SetWorldMapRegionDefaults), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] SetWorldMapRegionDefaults: JSON 为空。");
            return;
        }

        SetWorldMapRegionDefaultsRequest request =
            JsonUtility.FromJson<SetWorldMapRegionDefaultsRequest>(json);
        bool isForeign = request.regionMode == (int)WorldMapRegionMode.Foreign;
        bool ok = MapApi.Instance.SetWorldMapRegionDefaults(
            isForeign,
            NormalizeOptionalString(request.foreignPlateCode),
            NormalizeOptionalString(request.defaultUnitCode));
        if (!ok)
        {
            Debug.LogWarning($"[WebGLAPI] SetWorldMapRegionDefaults 失败: {json}");
            return;
        }

        LogCommunication("← Host", nameof(SetWorldMapRegionDefaults), "已应用");
    }

    /// <summary>
    /// 宿主调用：运行时合并覆盖 HTTP 默认请求头。
    /// JSON：{"headers":[{"key":"Satoken","value":"新token"},{"key":"X-Tenant-Id","value":"1"},{"key":"Sys-Lang","value":"zh-CN"}]}
    /// 未传入的 key、或 value 为空，不改变该 key 现有值。
    /// </summary>
    public void SetHttpRequestHeaders(string json)
    {
        NotifyHostCommunicationReceived(nameof(SetHttpRequestHeaders), json);
        LogCommunication("← Host", nameof(SetHttpRequestHeaders), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] SetHttpRequestHeaders: JSON 为空。");
            return;
        }

        SetHttpRequestHeadersRequest request = JsonUtility.FromJson<SetHttpRequestHeadersRequest>(json);
        if (!MapApi.Instance.SetHttpRequestHeaders(request.headers))
        {
            Debug.LogWarning($"[WebGLAPI] SetHttpRequestHeaders 失败: {json}");
            return;
        }

        LogCommunication("← Host", nameof(SetHttpRequestHeaders), "已应用");
    }

    /// <summary>宿主调用：关闭车辆 UI（停止零部件轮播 + 关闭连线面板）。arg 传 ""。</summary>
    public void CloseCarUI()
    {
        NotifyHostCommunicationReceived(nameof(CloseCarUI), string.Empty);
        LogCommunication("← Host", nameof(CloseCarUI), string.Empty);

        if (!MapApi.Instance.CloseCarVehicleDataUi())
        {
            Debug.LogWarning("[WebGLAPI] CloseCarUI 失败。");
            return;
        }

        LogCommunication("← Host", nameof(CloseCarUI), "已关闭");
    }

    /// <summary>宿主调用：关闭告警面板 GJ_Panel。arg 传 ""。</summary>
    public void CloseGJPanel()
    {
        NotifyHostCommunicationReceived(nameof(CloseGJPanel), string.Empty);
        LogCommunication("← Host", nameof(CloseGJPanel), string.Empty);

        if (!MapApi.Instance.CloseGJPanel())
        {
            Debug.LogWarning("[WebGLAPI] CloseGJPanel 失败。");
            return;
        }

        LogCommunication("← Host", nameof(CloseGJPanel), "已关闭");
    }

    /// <summary>
    /// 宿主调用：开启车辆热力图指定时段轮询（isReplay=true）。
    /// arg 为 JSON：{"startTime":"...","endTime":"..."}
    /// </summary>
    public void StartVehicleHeatmapSpecifiedTimePolling(string json)
    {
        NotifyHostCommunicationReceived(nameof(StartVehicleHeatmapSpecifiedTimePolling), json);
        LogCommunication("← Host", nameof(StartVehicleHeatmapSpecifiedTimePolling), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] StartVehicleHeatmapSpecifiedTimePolling: JSON 为空。");
            return;
        }

        VehicleHeatmapSpecifiedTimePollingRequest request =
            JsonUtility.FromJson<VehicleHeatmapSpecifiedTimePollingRequest>(json);
        if (!MapApi.Instance.StartVehicleHeatmapSpecifiedTimePolling(request.startTime, request.endTime))
        {
            Debug.LogWarning($"[WebGLAPI] StartVehicleHeatmapSpecifiedTimePolling 失败: {json}");
            return;
        }

        LogCommunication(
            "← Host",
            nameof(StartVehicleHeatmapSpecifiedTimePolling),
            $"已开启 | {request.startTime} ~ {request.endTime}");
    }

    /// <summary>宿主调用：关闭指定时段轮询，恢复默认热力图轮询。arg 传 ""。</summary>
    public void StopVehicleHeatmapSpecifiedTimePolling()
    {
        NotifyHostCommunicationReceived(nameof(StopVehicleHeatmapSpecifiedTimePolling), string.Empty);
        LogCommunication("← Host", nameof(StopVehicleHeatmapSpecifiedTimePolling), string.Empty);

        if (!MapApi.Instance.StopVehicleHeatmapSpecifiedTimePolling())
        {
            Debug.LogWarning("[WebGLAPI] StopVehicleHeatmapSpecifiedTimePolling 失败。");
            return;
        }

        LogCommunication("← Host", nameof(StopVehicleHeatmapSpecifiedTimePolling), "已恢复默认轮询");
    }

    /// <summary>
    /// 宿主调用：主动请求一次车辆热力图（不轮询）。
    /// arg 为 JSON：{"startTime":"...","endTime":"...","isReplay":true}；也可传 ""（默认时间 + isReplay=false）。
    /// </summary>
    public void RequestVehicleHeatmapOnce(string json)
    {
        NotifyHostCommunicationReceived(nameof(RequestVehicleHeatmapOnce), json);
        LogCommunication("← Host", nameof(RequestVehicleHeatmapOnce), json);

        string startTime = null;
        string endTime = null;
        bool isReplay = false;
        if (!string.IsNullOrWhiteSpace(json))
        {
            VehicleHeatmapRequestOnceRequest request =
                JsonUtility.FromJson<VehicleHeatmapRequestOnceRequest>(json);
            startTime = NormalizeOptionalString(request.startTime);
            endTime = NormalizeOptionalString(request.endTime);
            isReplay = request.isReplay;
        }

        if (!MapApi.Instance.RequestVehicleHeatmapOnce(startTime, endTime, isReplay))
        {
            Debug.LogWarning($"[WebGLAPI] RequestVehicleHeatmapOnce 失败: {json}");
            return;
        }

        LogCommunication(
            "← Host",
            nameof(RequestVehicleHeatmapOnce),
            $"已发起单次请求 | start={startTime} end={endTime} isReplay={isReplay}");
    }

    /// <summary>
    /// 宿主调用：请求车辆态势双接口（防护状态 + 攻击链路）。
    /// arg 可传 "" 使用默认参数，或 JSON：
    /// {"encryptVin":"...","startTime":"","endTime":"2026-06-30 23:00:00"}
    /// </summary>
    public void RequestCarVehicleData(string json)
    {
        NotifyHostCommunicationReceived(nameof(RequestCarVehicleData), json);
        LogCommunication("← Host", nameof(RequestCarVehicleData), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            if (!MapApi.Instance.RequestCarVehicleData())
            {
                Debug.LogWarning("[WebGLAPI] RequestCarVehicleData 失败（默认参数）。");
                return;
            }

            LogCommunication("← Host", nameof(RequestCarVehicleData), "已发起（默认参数）");
            return;
        }

        PartProtectionStatusRequest request = JsonUtility.FromJson<PartProtectionStatusRequest>(json);
        if (request == null)
        {
            Debug.LogWarning($"[WebGLAPI] RequestCarVehicleData: JSON 解析失败 | {json}");
            return;
        }

        if (!MapApi.Instance.RequestCarVehicleData(request.encryptVin, request.startTime, request.endTime))
        {
            Debug.LogWarning($"[WebGLAPI] RequestCarVehicleData 失败: {json}");
            return;
        }

        LogCommunication(
            "← Host",
            nameof(RequestCarVehicleData),
            $"已发起 | vin={request.encryptVin} | start={request.startTime} | end={request.endTime}");
    }

    /// <summary>
    /// 宿主调用：请求事件溯源详情（getSourceEventDetail）。
    /// arg 可传 "" 使用默认参数，或 JSON：
    /// {"eventId":"...","processStartTime":"...","processEndTime":"...","tenantId":1}
    /// </summary>
    public void RequestSecurityEventDetail(string json)
    {
        NotifyHostCommunicationReceived(nameof(RequestSecurityEventDetail), json);
        LogCommunication("← Host", nameof(RequestSecurityEventDetail), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            if (!MapApi.Instance.RequestSecurityEventDetail())
            {
                Debug.LogWarning("[WebGLAPI] RequestSecurityEventDetail 失败（默认参数）。");
                return;
            }

            LogCommunication("← Host", nameof(RequestSecurityEventDetail), "已发起（默认参数）");
            return;
        }

        SecurityEventDetailRequest request = JsonUtility.FromJson<SecurityEventDetailRequest>(json);
        if (request == null)
        {
            Debug.LogWarning($"[WebGLAPI] RequestSecurityEventDetail: JSON 解析失败 | {json}");
            return;
        }

        if (!MapApi.Instance.RequestSecurityEventDetail(
                request.eventId,
                request.processStartTime,
                request.processEndTime,
                request.tenantId))
        {
            Debug.LogWarning($"[WebGLAPI] RequestSecurityEventDetail 失败: {json}");
            return;
        }

        LogCommunication(
            "← Host",
            nameof(RequestSecurityEventDetail),
            $"已发起 | eventId={request.eventId} | tenantId={request.tenantId}");
    }

    /// <summary>
    /// 宿主调用：设置车辆 Y 轴旋转角度（联调/测试；正式业务通常监听 onUnityCarYawRotationChanged）。
    /// arg 为 JSON：{"yawAngle":90.0,"instant":false}
    /// </summary>
    public void SetCarYawRotation(string json)
    {
        NotifyHostCommunicationReceived(nameof(SetCarYawRotation), json);
        LogCommunication("← Host", nameof(SetCarYawRotation), json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] SetCarYawRotation: JSON 为空。");
            return;
        }

        if (!TryResolveCarYawRotate())
        {
            Debug.LogWarning("[WebGLAPI] SetCarYawRotation 失败：未找到车辆旋转控制器。");
            return;
        }

        SetCarYawRotationRequest request = JsonUtility.FromJson<SetCarYawRotationRequest>(json);
        bool instant = json.Contains("\"instant\"") && request.instant;
        _carYawRotate.SetYawAngle(request.yawAngle, instant, notify: true);
        LogCommunication(
            "← Host",
            nameof(SetCarYawRotation),
            $"已应用 yaw={NormalizeYawAngle(request.yawAngle):F1}°, instant={instant}");
    }

    /// <summary>宿主调用：地球 → 板块过渡。arg 传 ""。</summary>
    public void TransitionToPlateMap()
    {
        NotifyHostCommunicationReceived(nameof(TransitionToPlateMap), string.Empty);
        LogCommunication("← Host", nameof(TransitionToPlateMap), string.Empty);
        MapApi.Instance.TransitionToPlateMap();
    }

    /// <summary>宿主调用：板块 → 地球过渡。arg 传 ""。</summary>
    public void TransitionToEarth()
    {
        NotifyHostCommunicationReceived(nameof(TransitionToEarth), string.Empty);
        LogCommunication("← Host", nameof(TransitionToEarth), string.Empty);
        MapApi.Instance.TransitionToEarth();
    }

    /// <summary>宿主调用：聚焦指定板块模块（GameObject 名）。</summary>
    public void FocusPlateMapModule(string moduleName)
    {
        NotifyHostCommunicationReceived(nameof(FocusPlateMapModule), moduleName ?? string.Empty);
        LogCommunication("← Host", nameof(FocusPlateMapModule), moduleName);
        MapApi.Instance.FocusPlateMapModule(moduleName);
    }

    /// <summary>宿主调用：还原板块相机。arg 传 ""。</summary>
    public void RestorePlateMapCamera()
    {
        NotifyHostCommunicationReceived(nameof(RestorePlateMapCamera), string.Empty);
        LogCommunication("← Host", nameof(RestorePlateMapCamera), string.Empty);
        MapApi.Instance.RestorePlateMapCamera();
    }

    private void TrySubscribeCarYawRotation()
    {
        if (!TryResolveCarYawRotate())
        {
            return;
        }

        _carYawRotate.OnYawAngleChanged -= HandleCarYawAngleChanged;
        _carYawRotate.OnYawAngleChanged += HandleCarYawAngleChanged;
        LogCommunication("系统", "CarYaw", "已订阅车辆 Yaw 旋转回调");
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
        CallHostCarYawRotationChanged(yawAngle, isDragging);
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

    private void NotifyHostCommunicationReceived(string method, string arg)
    {
        arg ??= string.Empty;
        lastHostMessage = $"{method}|{arg}";
        OnHostMessageReceived?.Invoke(lastHostMessage);
        HostCommunicationReceived?.Invoke(method, arg);
    }

    #endregion

    #region Demo（历史测试接口，可保留兼容）


    #endregion
}
