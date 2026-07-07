using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebGL 宿主页面与 Unity 双向通信，公开方法与 <see cref="AndroidMessage"/> 对齐。
/// HTML 通过 unityInstance.SendMessage("WebGLAPI", methodName, arg) 调用。
/// 注意：须在 createUnityInstance().then 回调内赋值 unityInstance 后再调用；
/// 参考 Assets/Plugins/Web/WebJs/WebGLEmbedSample.html。
/// </summary>
public class WebGLAPI : MonoBehaviour
{
    public const string BridgeObjectName = "WebGLAPI";

    public static WebGLAPI Instance { get; private set; }

    [Header("Demo（可选）")]
    [SerializeField] private Text showMessageText;

    [SerializeField] private string lastHostMessage = "";

    public string LastHostMessage => lastHostMessage;

    public event Action<string> OnHostMessageReceived;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CallHTMLHandler(string methodName, string message);
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

    public void CallAndroidShowToast(string message)
    {
        CallHost("onUnityShowToast", message ?? string.Empty);
    }

    public void CallAndroidUpdateNativeTitle(string message)
    {
        CallHost("onUnityUpdateNativeTitle", message ?? string.Empty);
    }

    public void CallAndroidRequestDataSync(string message)
    {
        CallHost("onUnityRequestDataSync", message ?? string.Empty);
    }

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
        });
        CallHost("onUnityControlStateTransition", json);
    }

    private void CallHost(string method, string arg)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            CallHTMLHandler(method, arg);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebGLAPI] {method} failed: {e}");
        }
#else
        Debug.Log($"[WebGLAPI] Editor mock {method}: {arg}");
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

    public void OnAndroidNotifyA(string message)
    {
        HandleFromHost("A", message);
    }

    public void OnAndroidNotifyB(string message)
    {
        HandleFromHost("B", message);
    }

    public void OnDataSyncResult(string message)
    {
        HandleFromHost("SyncResult", message);
    }

    private void HandleFromHost(string channel, string message)
    {
        lastHostMessage = $"[{channel}] {message}";
        Debug.Log("[WebGLAPI] " + lastHostMessage);
        OnHostMessageReceived?.Invoke(lastHostMessage);
    }


    /// <summary>
    /// 宿主调用：按 JSON 参数执行层级跳转。
    /// unityInstance.SendMessage("WebGLAPI", "TransitionToControlState", json);
    /// </summary>
    /// <param name="json">
    /// 字段（区分大小写）：
    /// targetState(int, 必填) 目标级别 0~5：0 地球、1 国家、2 省级、3 车辆、4 零件、5 攻击路径；
    /// provinceName(string, 可选) 省名，如「山东」；
    /// provinceModuleName(string, 可选) 省级 3D 板块 GameObject 名；
    /// partName(string, 可选) 车辆零件 GameObject 名；
    /// partId(string, 可选) 业务零部件ID，仅零件 → 零件切换时生效；
    /// useInstantTransition(bool, 可选) 是否跳过过渡动画，默认 false。
    ///
    /// JSON 示例 1 — 跳到省级并指定省名与板块：
    /// {"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}
    ///
    /// JSON 示例 2 — 仅跳到车辆级（其余走默认）：
    /// {"targetState":3}
    ///
    /// JSON 示例 3 — 跳到零件级：
    /// {"targetState":4,"partName":"Group1575","partId":"PART-1575"}
    ///
    /// HTML 调用示例：
    /// unityInstance.SendMessage('WebGLAPI', 'TransitionToControlState',
    ///   '{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}');
    /// </param>
    public void TransitionToControlState(string json)
    {
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
            NormalizeOptionalString(request.partName),
            NormalizeOptionalString(request.partId),
            request.useInstantTransition);

        if (!ok)
        {
            Debug.LogWarning($"[WebGLAPI] TransitionToControlState 启动失败: {json}");
        }
    }

    public void TransitionToNextControlState()
    {
        if (!MapApi.Instance.TransitionToNextControlState())
        {
            Debug.LogWarning("[WebGLAPI] TransitionToNextControlState 启动失败。");
        }
    }

    public void TransitionToPreviousControlState()
    {
        if (!MapApi.Instance.TransitionToPreviousControlState())
        {
            Debug.LogWarning("[WebGLAPI] TransitionToPreviousControlState 启动失败。");
        }
    }

    /// <summary>宿主调用：开启/关闭四个大屏自动轮播。json 示例：{"enabled":true}</summary>
    public void SetBigScreenAutoCarouselEnabled(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[WebGLAPI] SetBigScreenAutoCarouselEnabled: JSON 为空。");
            return;
        }

        BigScreenAutoCarouselRequest request = JsonUtility.FromJson<BigScreenAutoCarouselRequest>(json);
        if (!MapApi.Instance.SetBigScreenAutoCarouselEnabled(request.enabled))
        {
            Debug.LogWarning($"[WebGLAPI] SetBigScreenAutoCarouselEnabled 失败: {json}");
        }
    }

    private static string NormalizeOptionalString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    #endregion

    #region Demo（历史测试接口，可保留兼容）

    private void Update()
    {
       

    }

   

    
    public void ShowMessage(string msg)
    {
        msg = StringTool.Base64ToString(msg);
        Debug.Log("[WebGLAPI] " + msg);
        if (showMessageText != null)
        {
            showMessageText.text = msg;
        }
    }

    [Obsolete("请使用 jslib CallHTMLHandler / CallAndroidShowToast 等接口向宿主发消息。")]
    public void SendMessageToJavaScript(string message)
    {
        CallHost("receiveMessageFromUnity", message ?? string.Empty);
    }

    #endregion
}
