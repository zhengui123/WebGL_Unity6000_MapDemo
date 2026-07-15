using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebGL 宿主页面与 Unity 双向通信（跨域 iframe 嵌入模式），公开方法与 <see cref="AndroidMessage"/> 对齐。
/// <para>Unity → 父页面：postMessage { source:"unity-webgl", method, message }。</para>
/// <para>父页面 → Unity：postMessage { source:"webgl-unity-parent", method, arg }，由 jslib 转发 SendMessage("WebGLAPI", ...)。</para>
/// <para>参考 Assets/Plugins/Web/WebJs/vue-parent-demo/ 与 WebGLEmbedIframe.sample.html。</para>
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

    /// <summary>父页面 / WebGL 宿主任意通信到达时触发（method, arg）。</summary>
    public static event Action<string, string> HostCommunicationReceived;

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
    }

    private void OnDisable()
    {
        UnsubscribeControlStateEvents();
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

        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = fromState,
            to = toState,
            partId = partId ?? string.Empty,
            status = ResolveNotifyBigScreenStatus(),
        });
        LogCommunication("→ Host", "onUnityControlStateTransition", $"开始 {fromState}→{toState} | {json}");
        CallHost("onUnityControlStateTransition", json);
    }

    /// <summary>通知宿主操控级别过渡完成：from=-1，to 为已就绪级别（JSON）。</summary>
    public void CallAndroidControlStateTransitionCompleted(int toState, string partId = null)
    {
        if (!TryValidateControlState(toState, nameof(CallAndroidControlStateTransitionCompleted)))
        {
            return;
        }

        string json = JsonUtility.ToJson(new ControlStateTransitionNotify
        {
            from = AndroidMessage.ControlStateTransitionCompletedFrom,
            to = toState,
            partId = partId ?? string.Empty,
            status = ResolveNotifyBigScreenStatus(),
        });
        LogCommunication("→ Host", "onUnityControlStateTransition", $"完成 to={toState} | {json}");
        CallHost("onUnityControlStateTransition", json);
    }

    private void CallHost(string method, string arg)
    {
        if (method != HostReadyMethodName
            && method != "onUnityControlStateTransition")
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

    /// <summary>过渡通知 JSON 中的大屏跳转状态（<see cref="BigScreenStatus"/>）；预留，暂回传 0。</summary>
    private static int ResolveNotifyBigScreenStatus()
    {
        // TODO: 按过渡触发源区分 NormalNavigation / InformationNavigation / ThreatDrillDown
        return (int)BigScreenStatus.NormalNavigation;
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
    /// provinceName(string, 可选) 省名，如「山东」；
    /// provinceModuleName(string, 可选) 省级 3D 板块 GameObject 名；
    /// partId(string, 可选) 业务零部件 ID，用于进入零件级、零件切换、攻击路径 → 零件；
    /// useInstantTransition(bool, 可选) 是否跳过过渡动画，默认 false。
    ///
    /// JSON 示例 1 — 跳到省级并指定省名与板块：
    /// {"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}
    ///
    /// JSON 示例 2 — 仅跳到车辆级（其余走默认）：
    /// {"targetState":3}
    ///
    /// JSON 示例 3 — 跳到零件级：
    /// {"targetState":4,"partId":"PART-1575"}
    ///
    /// HTML 调用示例（同域调试，跨域请用 postMessage）：
    /// unityInstance.SendMessage('WebGLAPI', 'TransitionToControlState',
    ///   '{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}');
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
            NormalizeOptionalString(request.provinceName),
            NormalizeOptionalString(request.provinceModuleName),
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
