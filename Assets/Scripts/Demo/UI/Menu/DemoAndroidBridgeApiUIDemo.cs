using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Android 桥接 API Demo：测试车辆 Yaw 设角与旋转回调。
/// </summary>
[DisallowMultipleComponent]
public class DemoAndroidBridgeApiUIDemo : MonoBehaviour
{
    public const string DefaultYawAngle = "90";

    [SerializeField] private Text _callbackStatusLabel;
    [SerializeField] private InputField _yawAngleInput;
    [SerializeField] private Button _setYawButton;
    [SerializeField] private Button _setYaw0Button;
    [SerializeField] private Button _setYaw90Button;
    [SerializeField] private Button _setYaw180Button;
    [SerializeField] private Button _setYaw270Button;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_setYawButton != null)
        {
            _setYawButton.onClick.AddListener(OnSetYawButtonClicked);
        }

        if (_setYaw0Button != null)
        {
            _setYaw0Button.onClick.AddListener(() => CallSetCarYawRotation(0f));
        }

        if (_setYaw90Button != null)
        {
            _setYaw90Button.onClick.AddListener(() => CallSetCarYawRotation(90f));
        }

        if (_setYaw180Button != null)
        {
            _setYaw180Button.onClick.AddListener(() => CallSetCarYawRotation(180f));
        }

        if (_setYaw270Button != null)
        {
            _setYaw270Button.onClick.AddListener(() => CallSetCarYawRotation(270f));
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        if (AndroidMessage.Instance != null)
        {
            AndroidMessage.Instance.OnCarYawRotationNotified += HandleCarYawRotationNotified;
        }

        RefreshCallbackStatusLabel("等待旋转回调...");
    }

    private void OnDisable()
    {
        if (AndroidMessage.Instance != null)
        {
            AndroidMessage.Instance.OnCarYawRotationNotified -= HandleCarYawRotationNotified;
        }
    }

    private void OnDestroy()
    {
        if (_setYawButton != null)
        {
            _setYawButton.onClick.RemoveListener(OnSetYawButtonClicked);
        }

        if (_setYaw0Button != null)
        {
            _setYaw0Button.onClick.RemoveAllListeners();
        }

        if (_setYaw90Button != null)
        {
            _setYaw90Button.onClick.RemoveAllListeners();
        }

        if (_setYaw180Button != null)
        {
            _setYaw180Button.onClick.RemoveAllListeners();
        }

        if (_setYaw270Button != null)
        {
            _setYaw270Button.onClick.RemoveAllListeners();
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnSetYawButtonClicked()
    {
        if (_yawAngleInput == null || string.IsNullOrWhiteSpace(_yawAngleInput.text))
        {
            Debug.LogWarning("[DemoAndroidBridgeApiUIDemo] 请输入 Yaw 角度。");
            return;
        }

        if (!float.TryParse(_yawAngleInput.text.Trim(), out float yawAngle))
        {
            Debug.LogWarning($"[DemoAndroidBridgeApiUIDemo] 无效角度: {_yawAngleInput.text}");
            return;
        }

        CallSetCarYawRotation(yawAngle);
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoAndroidBridgeApiUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void HandleCarYawRotationNotified(CarYawRotationNotify notify)
    {
        string dragText = notify.isDragging ? "拖拽中" : "已松手/API";
        RefreshCallbackStatusLabel($"回调：Yaw={notify.yawAngle:F1}°（{dragText}）");
    }

    private static void CallSetCarYawRotation(float yawAngle)
    {
        if (AndroidMessage.Instance == null)
        {
            Debug.LogWarning("[DemoAndroidBridgeApiUIDemo] 场景中未找到 AndroidMessage（AndroidBridge）。");
            return;
        }

        string json = JsonUtility.ToJson(new SetCarYawRotationRequest
        {
            yawAngle = yawAngle,
            instant = false,
        });
        Debug.Log($"[DemoAndroidBridgeApiUIDemo] 模拟 Android 调用 SetCarYawRotation: {json}");
        AndroidMessage.Instance.SetCarYawRotation(json);
    }

    private void RefreshCallbackStatusLabel(string text)
    {
        if (_callbackStatusLabel != null)
        {
            _callbackStatusLabel.text = text;
        }
    }

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
