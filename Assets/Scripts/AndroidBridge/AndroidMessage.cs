using UnityEngine;

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

    #region Unity → Android（对应 MainActivity 三个方法）

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

    /// <summary>播放 AllPlateMap → GaodeMap 过渡（可选指定省名）。</summary>
    public void TransitionPlateMapToGaodeMap(string provinceName = null)
    {
        MapApi.Instance.TransitionPlateMapToGaodeMap(provinceName);
    }
    
    /// <summary>倒放 GaodeMap → AllPlateMap 过渡（可选指定省名，用于事件参数）。</summary>
    public void TransitionGaodeMapToPlateMap(string provinceName = null)
    {
        MapApi.Instance.TransitionGaodeMapToPlateMap(provinceName);
    }

    /// <summary>播放 GaodeMap → City-Maker 第二阶段过渡。</summary>
    public void TransitionGaodeMapToCity()
    {
        MapApi.Instance.TransitionGaodeMapToCity();
    }


    /// <summary>倒放 City-Maker → GaodeMap 第二阶段过渡。</summary>
    public void TransitionCityToGaodeMap()
    {
        MapApi.Instance.TransitionCityToGaodeMap();
    }


 
    #endregion
}
