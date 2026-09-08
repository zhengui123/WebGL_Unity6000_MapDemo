using UnityEngine;

/// <summary>
/// 测试日志总控：功能测试 / 后端 HTTP / Web·Android 宿主通信，以及失败类（Warning/Error）独立开关。
/// 场景可挂载本组件以便在 Inspector 调节；未挂载时首次访问会自动创建。
/// </summary>
[DisallowMultipleComponent]
public class LogManager : UnitySingle<LogManager>
{
    public enum Channel
    {
        /// <summary>功能测试、Demo、业务流程联调。</summary>
        Feature = 0,
        /// <summary>后端 HTTP / 签名 / 配置加载。</summary>
        Backend = 1,
        /// <summary>WebGL / Android 宿主双向通信。</summary>
        Host = 2,
    }

    [Header("分类开关（普通 Log）")]
    [SerializeField] private bool _enableFeatureTestLog = true;
    [SerializeField] private bool _enableBackendHttpLog = true;
    [SerializeField] private bool _enableHostBridgeLog = true;

    [Header("失败类开关（Warning / Error）")]
    [Tooltip("关闭后，各分类的 LogWarning/LogError 均不再输出。")]
    [SerializeField] private bool _enableFailureLog = true;

    public bool EnableFeatureTestLog
    {
        get => _enableFeatureTestLog;
        set => _enableFeatureTestLog = value;
    }

    public bool EnableBackendHttpLog
    {
        get => _enableBackendHttpLog;
        set => _enableBackendHttpLog = value;
    }

    public bool EnableHostBridgeLog
    {
        get => _enableHostBridgeLog;
        set => _enableHostBridgeLog = value;
    }

    public bool EnableFailureLog
    {
        get => _enableFailureLog;
        set => _enableFailureLog = value;
    }

    public static bool IsChannelEnabled(Channel channel)
    {
        LogManager mgr = Instance;
        switch (channel)
        {
            case Channel.Feature:
                return mgr._enableFeatureTestLog;
            case Channel.Backend:
                return mgr._enableBackendHttpLog;
            case Channel.Host:
                return mgr._enableHostBridgeLog;
            default:
                return false;
        }
    }

    public static bool IsFailureEnabled => Instance._enableFailureLog;

    public static void Log(Channel channel, string message)
    {
        if (!IsChannelEnabled(channel))
        {
            return;
        }

        Debug.Log(message);
    }

    public static void LogWarning(Channel channel, string message)
    {
        if (!IsChannelEnabled(channel) || !IsFailureEnabled)
        {
            return;
        }

        Debug.LogWarning(message);
    }

    public static void LogError(Channel channel, string message)
    {
        if (!IsChannelEnabled(channel) || !IsFailureEnabled)
        {
            return;
        }

        Debug.LogError(message);
    }

    public static void LogFeature(string message) => Log(Channel.Feature, message);

    public static void LogFeatureWarning(string message) => LogWarning(Channel.Feature, message);

    public static void LogFeatureError(string message) => LogError(Channel.Feature, message);

    public static void LogBackend(string message) => Log(Channel.Backend, message);

    public static void LogBackendWarning(string message) => LogWarning(Channel.Backend, message);

    public static void LogBackendError(string message) => LogError(Channel.Backend, message);

    public static void LogHost(string message) => Log(Channel.Host, message);

    public static void LogHostWarning(string message) => LogWarning(Channel.Host, message);

    public static void LogHostError(string message) => LogError(Channel.Host, message);
}
