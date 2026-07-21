using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本地威胁测试 Demo：注入内嵌 JSON，跑通国家/省级威胁流程（无需真实接口）。
/// </summary>
[DisallowMultipleComponent]
public class ThreatLocalAlertTestUIDemo : MonoBehaviour
{
    [Header("操作")]
    [SerializeField] private Button _injectMultiProvinceButton;
    [SerializeField] private Button _injectSameVinButton;
    [SerializeField] private Button _skipHoldButton;
    [SerializeField] private Button _clearExcludedButton;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Button _resetFlowButton;
    [SerializeField] private Button _backButton;

    [Header("展示")]
    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _resultListText;
    [SerializeField] private ScrollRect _resultScroll;

    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        EnsureReferences();
        BindButtons(true);
    }

    private void OnEnable()
    {
        EnsureReferences();
        EnsureFlowRunnerExists();

        HighRiskSecurityEventDataStore.Instance.DataChanged += HandleDataChanged;
        ThreatProvinceAlertController.ProvinceAlertStarted += HandleProvinceAlertStarted;
        ThreatProvinceAlertController.ProvinceAlertCompleted += HandleProvinceAlertCompleted;
        ThreatProvinceAlertController.AllProvinceAlertsCompleted += HandleAllAlertsCompleted;
        ThreatAlertFlowRunner.ThreatVehicleEntryRequested += HandleVehicleEntryRequested;
        ThreatAlertFlowRunner.ThreatProvinceDrillReserved += HandleDrillReserved;

        ApplyRuntimeTextStyle();
        RefreshStatus("就绪：注入本地 JSON 开始测试威胁流程。");
        RefreshResultList();
    }

    private void OnDisable()
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        if (store != null)
        {
            store.DataChanged -= HandleDataChanged;
        }

        ThreatProvinceAlertController.ProvinceAlertStarted -= HandleProvinceAlertStarted;
        ThreatProvinceAlertController.ProvinceAlertCompleted -= HandleProvinceAlertCompleted;
        ThreatProvinceAlertController.AllProvinceAlertsCompleted -= HandleAllAlertsCompleted;
        ThreatAlertFlowRunner.ThreatVehicleEntryRequested -= HandleVehicleEntryRequested;
        ThreatAlertFlowRunner.ThreatProvinceDrillReserved -= HandleDrillReserved;
    }

    private void OnDestroy()
    {
        BindButtons(false);
    }

    private void BindButtons(bool bind)
    {
        Bind(_injectMultiProvinceButton, OnInjectMultiProvinceClicked, bind);
        Bind(_injectSameVinButton, OnInjectSameVinClicked, bind);
        Bind(_skipHoldButton, OnSkipHoldClicked, bind);
        Bind(_clearExcludedButton, OnClearExcludedClicked, bind);
        Bind(_refreshButton, RefreshResultList, bind);
        Bind(_resetFlowButton, OnResetFlowClicked, bind);
        Bind(_backButton, OnBackClicked, bind);
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

    private void OnInjectMultiProvinceClicked()
    {
        InjectJson(ThreatLocalAlertTestMockJson.BuildMultiProvinceQualifiedJson(), "多省达标（鲁+黑）");
    }

    private void OnInjectSameVinClicked()
    {
        InjectJson(ThreatLocalAlertTestMockJson.BuildSameVinQualifiedJson(), "同 Vin≥3（山东）");
    }

    private void InjectJson(string json, string label)
    {
        EnsureFlowRunnerExists();

        bool wasProcessing = ThreatProvinceAlertController.IsProcessing;

        // 不在此 Reset：处理中只刷新数据与画面；空闲才由 Evaluate 启动新流程。
        if (!HighRiskSecurityEventApi.TryParseAndStoreResponse(json, out _, out string error))
        {
            RefreshStatus($"注入失败（{label}）：{error}");
            Debug.LogWarning($"[ThreatLocalAlertTestUIDemo] 注入失败：{error}\n{json}");
            return;
        }

        string mode = wasProcessing
            ? "处理中已刷新数据/画面（未重入流程）"
            : "空闲已触发告警评估并启动流程";
        RefreshStatus($"已注入 {label}，{mode}。可用「跳过停留」加快 10s/60s。");
        RefreshResultList();
    }

    private void OnSkipHoldClicked()
    {
        ThreatProvinceAlertController.CompleteCurrentProvinceAlert();
        RefreshStatus("已请求跳过当前国家/省级停留。");
    }

    private void OnClearExcludedClicked()
    {
        ThreatExcludedEventIdStore.Clear();
        RefreshStatus($"已清空排除表。当前排除数={ThreatExcludedEventIdStore.Count}");
        RefreshResultList();
    }

    private void OnResetFlowClicked()
    {
        ThreatProvinceAlertController.ResetProcessingState();
        HighRiskSecurityEventDataStore.Instance?.Clear();
        ThreatExcludedEventIdStore.Clear();
        GameManager.Instance?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
        RefreshStatus("已重置流程、缓存与排除表，Playback→Default。");
        RefreshResultList();
    }

    private void OnBackClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[ThreatLocalAlertTestUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void HandleDataChanged()
    {
        RefreshResultList();
    }

    private void HandleProvinceAlertStarted(ThreatProvinceAlertContext context)
    {
        string code = context?.ProvinceCode ?? "-";
        int count = context?.Events?.Count ?? 0;
        RefreshStatus($"省级告警开始：province={code}，事件={count}");
        RefreshResultList();
    }

    private void HandleProvinceAlertCompleted(ThreatProvinceAlertContext context)
    {
        string code = context?.ProvinceCode ?? "-";
        RefreshStatus($"省级告警结束：province={code}");
        RefreshResultList();
    }

    private void HandleAllAlertsCompleted()
    {
        RefreshStatus("威胁流程空闲（无达标省或已跑完）。");
        RefreshResultList();
    }

    private void HandleVehicleEntryRequested(string vin)
    {
        RefreshStatus(
            $"Vin 下钻 | vin={vin} | 车{ThreatAlertSettings.VehicleLevelHoldSeconds:F0}s → " +
            $"攻击链路{ThreatAlertSettings.AttackPathLevelHoldSeconds:F0}s → " +
            $"零件各{ThreatAlertSettings.PartLevelHoldSeconds:F0}s");
        Debug.Log($"[ThreatLocalAlertTestUIDemo] ThreatVehicleEntryRequested vin={vin}");
    }

    private void HandleDrillReserved(ThreatProvinceAlertContext context)
    {
        string code = context?.ProvinceCode ?? "-";
        RefreshStatus($"[预留] 威胁下钻钩子：province={code}");
        Debug.Log($"[ThreatLocalAlertTestUIDemo] ThreatProvinceDrillReserved province={code}");
    }

    private void RefreshResultList()
    {
        HighRiskSecurityEventDataStore store = HighRiskSecurityEventDataStore.Instance;
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine($"Playback={GameManager.Instance?.CurrentPlaybackState}");
        builder.AppendLine($"Control={GameManager.Instance?.CurrentState}");
        builder.AppendLine(
            $"流程={(ThreatProvinceAlertController.IsProcessing ? "进行中" : "空闲")}，" +
            $"当前省={ThreatProvinceAlertController.CurrentProvinceCode ?? "-"}");
        builder.AppendLine($"排除 eventId 数={ThreatExcludedEventIdStore.Count}");
        builder.AppendLine($"缓存事件总数={store?.Count ?? 0}");
        builder.AppendLine($"阈值≥{ThreatAlertSettings.EventsPerProvinceThreshold}，同Vin≥{ThreatAlertSettings.SameVinCountToEnterVehicle}");
        builder.AppendLine(
            $"停留：国家{ThreatAlertSettings.CountryLevelHoldSeconds:F0}s / " +
            $"省{ThreatAlertSettings.ProvinceLevelHoldSeconds:F0}s / " +
            $"车{ThreatAlertSettings.VehicleLevelHoldSeconds:F0}s / " +
            $"攻击链路{ThreatAlertSettings.AttackPathLevelHoldSeconds:F0}s / " +
            $"零件{ThreatAlertSettings.PartLevelHoldSeconds:F0}s（每件）");
        builder.AppendLine("--- 分省 ---");

        if (store != null)
        {
            IReadOnlyList<string> qualified = store.GetProvincesMeetingThreshold(
                ThreatAlertSettings.EventsPerProvinceThreshold);
            builder.Append("达标省：");
            if (qualified == null || qualified.Count == 0)
            {
                builder.AppendLine("(无)");
            }
            else
            {
                builder.AppendLine(string.Join(", ", qualified));
            }

            IReadOnlyList<HighRiskSecurityEventItem> all = store.GetAllEvents();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < all.Count; i++)
            {
                HighRiskSecurityEventItem item = all[i];
                if (item == null || string.IsNullOrWhiteSpace(item.province))
                {
                    continue;
                }

                if (!PlateMapBoundaryDatabase.TryNormalizeProvinceCode(item.province, out string code))
                {
                    code = item.province.Trim();
                }

                counts.TryGetValue(code, out int c);
                counts[code] = c + 1;
            }

            foreach (KeyValuePair<string, int> pair in counts)
            {
                builder.AppendLine($"{pair.Key} → {pair.Value} 条");
            }
        }

        if (_resultListText != null)
        {
            _resultListText.text = builder.ToString();
        }
    }

    private void RefreshStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = message ?? string.Empty;
        }
    }

    private void EnsureFlowRunnerExists()
    {
        if (ThreatAlertFlowRunner.Instance != null)
        {
            return;
        }

        GameObject host = new GameObject("ThreatAlertFlowRunner");
        host.AddComponent<ThreatAlertFlowRunner>();
        DontDestroyOnLoad(host);
        Debug.Log("[ThreatLocalAlertTestUIDemo] 已自动创建 ThreatAlertFlowRunner。");
    }

    private void ApplyRuntimeTextStyle()
    {
        ThreatDemoUiStyle.ApplyPanelLabel(_statusLabel);
        ThreatDemoUiStyle.ApplyResultText(_resultListText);
    }

    private void EnsureReferences()
    {
        if (_injectMultiProvinceButton == null)
        {
            _injectMultiProvinceButton = transform.Find("InjectMultiProvinceButton")?.GetComponent<Button>();
        }

        if (_injectSameVinButton == null)
        {
            _injectSameVinButton = transform.Find("InjectSameVinButton")?.GetComponent<Button>();
        }

        if (_skipHoldButton == null)
        {
            _skipHoldButton = transform.Find("SkipHoldButton")?.GetComponent<Button>();
        }

        if (_clearExcludedButton == null)
        {
            _clearExcludedButton = transform.Find("ClearExcludedButton")?.GetComponent<Button>();
        }

        if (_refreshButton == null)
        {
            _refreshButton = transform.Find("RefreshListButton")?.GetComponent<Button>();
        }

        if (_resetFlowButton == null)
        {
            _resetFlowButton = transform.Find("ResetFlowButton")?.GetComponent<Button>();
        }

        if (_backButton == null)
        {
            _backButton = transform.Find("BackButton")?.GetComponent<Button>();
        }

        if (_statusLabel == null)
        {
            _statusLabel = transform.Find("StatusLabel")?.GetComponent<Text>();
        }

        if (_resultListText == null)
        {
            _resultListText = transform.Find("ResultScrollView/Viewport/Content/ResultListText")?.GetComponent<Text>();
        }

        if (_resultScroll == null)
        {
            _resultScroll = transform.Find("ResultScrollView")?.GetComponent<ScrollRect>();
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
