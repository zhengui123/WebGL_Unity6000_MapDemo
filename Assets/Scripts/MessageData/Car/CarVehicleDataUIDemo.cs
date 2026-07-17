using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 车辆态势数据本地/网络测试面板逻辑。
/// </summary>
[DisallowMultipleComponent]
public class CarVehicleDataUIDemo : MonoBehaviour
{
    [SerializeField] private CarVehicleDataController _controller;
    [SerializeField] private InputField _encryptVinInput;
    [SerializeField] private InputField _startTimeInput;
    [SerializeField] private InputField _endTimeInput;
    [SerializeField] private Text _resultText;
    [SerializeField] private Button _requestHttpButton;
    [SerializeField] private Button _applyLocalJsonButton;
    [SerializeField] private Button _showUiFromCacheButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponent<CarVehicleDataController>();
        }

        if (_controller == null)
        {
            _controller = CarVehicleDataController.Instance;
        }

        if (_backButton == null)
        {
            _backButton = transform.Find("BackButton")?.GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        Bind(_requestHttpButton, OnClickRequestHttp, true);
        Bind(_applyLocalJsonButton, OnClickApplyLocalJson, true);
        Bind(_showUiFromCacheButton, OnClickShowUiFromCache, true);
        Bind(_backButton, OnBackClicked, true);
    }

    private void OnDisable()
    {
        Bind(_requestHttpButton, OnClickRequestHttp, false);
        Bind(_applyLocalJsonButton, OnClickApplyLocalJson, false);
        Bind(_showUiFromCacheButton, OnClickShowUiFromCache, false);
        Bind(_backButton, OnBackClicked, false);
    }

    private void OnClickRequestHttp()
    {
        EnsureController();
        if (_controller == null)
        {
            SetResult("未找到 CarVehicleDataController。");
            return;
        }

        string vin = ReadInput(_encryptVinInput, PartProtectionStatusRequest.DefaultEncryptVin);
        string start = ReadInput(_startTimeInput, string.Empty);
        string end = ReadInput(_endTimeInput, "2026-06-30 23:00:00");
        SetResult("请求中…");
        _controller.Request(vin, start, end, (ok, error) =>
        {
            if (!ok)
            {
                SetResult($"失败：{error}");
                return;
            }

            PartProtectionStatusPart first = CarVehicleDataStore.Instance.GetFirstUnprotectedPart();
            string name = first != null ? first.partTypeName : "-";
            int events = first != null ? first.PendingEventCount : 0;
            SetResult($"成功覆盖缓存 | firstUnprotected={name} | pendingEvents={events}");
        });
    }

    private void OnClickApplyLocalJson()
    {
        EnsureController();
        if (_controller == null)
        {
            SetResult("未找到 CarVehicleDataController。");
            return;
        }

        if (!_controller.ApplyLocalJson(
                CarVehicleDataMockJson.PartProtectionStatusSuccessJson,
                CarVehicleDataMockJson.AttackChainSuccessJson,
                out string error))
        {
            SetResult($"本地 JSON 失败：{error}");
            return;
        }

        PartProtectionStatusPart first = CarVehicleDataStore.Instance.GetFirstUnprotectedPart();
        string name = first != null ? first.partTypeName : "-";
        SetResult($"本地 JSON 已应用 | title/start3D={name} | 若已在车辆级将 OpenCarUI");
    }

    private void OnClickShowUiFromCache()
    {
        EnsureController();
        if (_controller == null)
        {
            SetResult("未找到 CarVehicleDataController。");
            return;
        }

        if (!CarVehicleDataStore.Instance.HasCache)
        {
            SetResult("无缓存，请先请求或应用本地 JSON。");
            return;
        }

        bool shown = _controller.TryShowVehicleUiFromCache();
        SetResult(shown ? "已尝试 OpenCarUI + MessageListPanel" : "未展示（非车辆级或缺少 partTypeName）");
    }

    private void OnBackClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[CarVehicleDataUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void EnsureController()
    {
        if (_controller == null)
        {
            _controller = CarVehicleDataController.Instance;
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

    private void SetResult(string message)
    {
        if (_resultText != null)
        {
            _resultText.text = message ?? string.Empty;
        }

        Debug.Log($"[CarVehicleDataUIDemo] {message}");
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action, bool bind)
    {
        if (button == null)
        {
            return;
        }

        if (bind)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            button.onClick.RemoveListener(action);
        }
    }

    private static string ReadInput(InputField field, string fallback)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.text))
        {
            return fallback;
        }

        return field.text.Trim();
    }
}
