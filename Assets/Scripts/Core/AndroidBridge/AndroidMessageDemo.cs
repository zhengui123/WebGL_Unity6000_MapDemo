using UnityEngine;

/// <summary>
/// 模拟 Android 通过 UnitySendMessage 调用 <see cref="AndroidMessage.TransitionToControlState"/> 的示例。
/// </summary>
public class AndroidMessageDemo : MonoBehaviour
{
    [Header("按键触发（Editor 调试用）")]
    [SerializeField] private KeyCode _transitionKey = KeyCode.J;

    [Header("示例 JSON（与 Android 侧传入字符串一致）")]
    [SerializeField] private string _sampleJson =
        "{\"targetState\":2,\"provinceCode\":\"370000\",\"useInstantTransition\":false}";

    [SerializeField] private string _sampleCarYawJson = "{\"yawAngle\":90.0,\"instant\":false}";

    private void Update()
    {
        if (Input.GetKeyDown(_transitionKey))
        {
            CallTransitionToControlStateSample();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            CallSetCarYawRotationSample();
        }
    }

    /// <summary>
    /// 与 Android 调用方式一致：直接传入 JSON 字符串。
    /// UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
    /// </summary>
    [ContextMenu("示例：TransitionToControlState")]
    public void CallTransitionToControlStateSample()
    {
        if (AndroidMessage.Instance == null)
        {
            Debug.LogWarning("[AndroidMessageDemo] 场景中未找到 AndroidMessage（AndroidBridge）。");
            return;
        }

        Debug.Log($"[AndroidMessageDemo] 模拟 Android 调用 TransitionToControlState: {_sampleJson}");
        AndroidMessage.Instance.TransitionToControlState(_sampleJson);
    }

    /// <summary>
    /// UnityPlayer.UnitySendMessage("AndroidBridge", "SetCarYawRotation", json);
    /// </summary>
    [ContextMenu("示例：SetCarYawRotation")]
    public void CallSetCarYawRotationSample()
    {
        if (AndroidMessage.Instance == null)
        {
            Debug.LogWarning("[AndroidMessageDemo] 场景中未找到 AndroidMessage（AndroidBridge）。");
            return;
        }

        Debug.Log($"[AndroidMessageDemo] 模拟 Android 调用 SetCarYawRotation: {_sampleCarYawJson}");
        AndroidMessage.Instance.SetCarYawRotation(_sampleCarYawJson);
    }
}
