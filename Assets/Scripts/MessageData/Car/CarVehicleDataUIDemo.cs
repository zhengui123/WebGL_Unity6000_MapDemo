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
    [SerializeField] private Button _closeCarUiButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        // Controller 挂在场景 Manager 下，不挂在本 Demo 面板上。
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
        Bind(_closeCarUiButton, OnClickCloseCarUi, true);
        Bind(_backButton, OnBackClicked, true);
    }

    private void OnDisable()
    {
        Bind(_requestHttpButton, OnClickRequestHttp, false);
        Bind(_applyLocalJsonButton, OnClickApplyLocalJson, false);
        Bind(_showUiFromCacheButton, OnClickShowUiFromCache, false);
        Bind(_closeCarUiButton, OnClickCloseCarUi, false);
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
            int attackPaths = CarVehicleDataStore.Instance.BuildAttackPathEntries().Count;
            SetResult($"成功覆盖缓存 | firstUnprotected={name} | pendingEvents={events} | attackPaths={attackPaths}");
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
        int slideCount = CarVehicleDataStore.Instance.BuildPartSlides().Count;
        int attackPaths = CarVehicleDataStore.Instance.BuildAttackPathEntries().Count;
        SetResult(
            $"本地 JSON 已应用 | first={name} | slides={slideCount} | attackPaths={attackPaths} | " +
            "车辆级轮播/攻击路径级展示");
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

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CurrentState == GameManager.ControlState.AttackPathLevel)
        {
            bool attackShown = _controller.TryShowAttackPathsFromCache();
            int pathCount = CarVehicleDataStore.Instance.BuildAttackPathEntries().Count;
            SetResult(attackShown
                ? $"已加载 {pathCount} 条攻击路径"
                : "攻击路径未展示（无有效链路或缺少 AttackPathController）");
            return;
        }

        bool shown = _controller.TryShowVehicleUiFromCache();
        SetResult(shown ? "已 OpenCarUI + 零部件轮播（切换时先关再开）" : "未展示（非车辆级或无零部件）");
    }

    private void OnClickCloseCarUi()
    {
        bool closed = MapApi.Instance != null && MapApi.Instance.CloseCarVehicleDataUi();
        SetResult(closed ? "已停止轮播并关闭车辆 UI" : "关闭失败（无 CarPanelManager）");
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
