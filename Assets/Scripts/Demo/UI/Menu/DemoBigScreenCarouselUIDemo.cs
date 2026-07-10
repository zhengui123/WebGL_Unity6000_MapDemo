using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大屏自动轮播 Demo：延时判定开关、立即开/关、播放状态与 WebGL 通信测试。
/// </summary>
[DisallowMultipleComponent]
public class DemoBigScreenCarouselUIDemo : MonoBehaviour
{
    public const string DefaultDelayedStartSeconds = "60";

    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _countdownLabel;
    [SerializeField] private Text _playbackStateLabel;
    [SerializeField] private InputField _delayedStartInput;
    [SerializeField] private Button _enableDelayedFeatureButton;
    [SerializeField] private Button _disableDelayedFeatureButton;
    [SerializeField] private Button _enableButton;
    [SerializeField] private Button _disableButton;
    [SerializeField] private Button _simulateWebGlCommunicationButton;
    [SerializeField] private Button _setDefaultPlaybackStateButton;
    [SerializeField] private Button _setAlertPlaybackStateButton;
    [SerializeField] private Button _setThreatPlaybackStateButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_enableDelayedFeatureButton != null)
        {
            _enableDelayedFeatureButton.onClick.AddListener(OnEnableDelayedFeatureButtonClicked);
        }

        if (_disableDelayedFeatureButton != null)
        {
            _disableDelayedFeatureButton.onClick.AddListener(OnDisableDelayedFeatureButtonClicked);
        }

        if (_enableButton != null)
        {
            _enableButton.onClick.AddListener(OnEnableButtonClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.AddListener(OnDisableButtonClicked);
        }

        if (_simulateWebGlCommunicationButton != null)
        {
            _simulateWebGlCommunicationButton.onClick.AddListener(OnSimulateWebGlCommunicationButtonClicked);
        }

        if (_setDefaultPlaybackStateButton != null)
        {
            _setDefaultPlaybackStateButton.onClick.AddListener(() => SetPlaybackState(GameManager.BigScreenPlaybackState.Default));
        }

        if (_setAlertPlaybackStateButton != null)
        {
            _setAlertPlaybackStateButton.onClick.AddListener(() => SetPlaybackState(GameManager.BigScreenPlaybackState.AlertPositioning));
        }

        if (_setThreatPlaybackStateButton != null)
        {
            _setThreatPlaybackStateButton.onClick.AddListener(() => SetPlaybackState(GameManager.BigScreenPlaybackState.Threat));
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            manager.OnPlaybackStateChanged += HandlePlaybackStateChanged;
        }

        RefreshAllLabels();
    }

    private void OnDisable()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            manager.OnPlaybackStateChanged -= HandlePlaybackStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (_enableDelayedFeatureButton != null)
        {
            _enableDelayedFeatureButton.onClick.RemoveListener(OnEnableDelayedFeatureButtonClicked);
        }

        if (_disableDelayedFeatureButton != null)
        {
            _disableDelayedFeatureButton.onClick.RemoveListener(OnDisableDelayedFeatureButtonClicked);
        }

        if (_enableButton != null)
        {
            _enableButton.onClick.RemoveListener(OnEnableButtonClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.RemoveListener(OnDisableButtonClicked);
        }

        if (_simulateWebGlCommunicationButton != null)
        {
            _simulateWebGlCommunicationButton.onClick.RemoveListener(OnSimulateWebGlCommunicationButtonClicked);
        }

        if (_setDefaultPlaybackStateButton != null)
        {
            _setDefaultPlaybackStateButton.onClick.RemoveAllListeners();
        }

        if (_setAlertPlaybackStateButton != null)
        {
            _setAlertPlaybackStateButton.onClick.RemoveAllListeners();
        }

        if (_setThreatPlaybackStateButton != null)
        {
            _setThreatPlaybackStateButton.onClick.RemoveAllListeners();
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void Update()
    {
        RefreshAllLabels();
    }

    private void HandlePlaybackStateChanged(GameManager.BigScreenPlaybackState state)
    {
        RefreshPlaybackStateLabel(state);
    }

    private void OnEnableDelayedFeatureButtonClicked()
    {
        ApplyDelayedStartSecondsFromInput();
        MapApi mapApi = MapApi.Instance;
        if (mapApi == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 MapApi。");
            return;
        }

        mapApi.SetBigScreenDelayedStartFeatureEnabled(true);
    }

    private void OnDisableDelayedFeatureButtonClicked()
    {
        MapApi mapApi = MapApi.Instance;
        if (mapApi == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 MapApi。");
            return;
        }

        mapApi.SetBigScreenDelayedStartFeatureEnabled(false);
    }

    private void OnEnableButtonClicked()
    {
        MapApi mapApi = MapApi.Instance;
        if (mapApi == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 MapApi。");
            return;
        }

        ApplyDelayedStartSecondsFromInput();
        if (!mapApi.SetBigScreenAutoCarouselEnabled(true, bypassDelayedStart: true))
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 立即开启自动轮播失败。");
        }
    }

    private void OnDisableButtonClicked()
    {
        if (!TrySetCarouselEnabled(false))
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 关闭自动轮播失败。");
        }
    }

    private void OnSimulateWebGlCommunicationButtonClicked()
    {
        MapApi mapApi = MapApi.Instance;
        if (mapApi == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 MapApi。");
            return;
        }

        Debug.Log("[DemoBigScreenCarouselUIDemo] 模拟 WebGL 宿主通信。");
        mapApi.NotifyBigScreenHostCommunication();
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private void ApplyDelayedStartSecondsFromInput()
    {
        float delaySeconds = ParseDelayedStartSeconds();
        if (delaySeconds < 0f)
        {
            return;
        }

        MapApi mapApi = MapApi.Instance;
        mapApi?.SetBigScreenDelayedStartSeconds(delaySeconds);
    }

    private float ParseDelayedStartSeconds()
    {
        if (_delayedStartInput == null || string.IsNullOrWhiteSpace(_delayedStartInput.text))
        {
            return BigScreenCarouselController.Instance != null
                ? BigScreenCarouselController.Instance.DefaultDelayedStartSeconds
                : 60f;
        }

        if (!float.TryParse(_delayedStartInput.text.Trim(), out float delaySeconds))
        {
            Debug.LogWarning($"[DemoBigScreenCarouselUIDemo] 无效延时秒数: {_delayedStartInput.text}");
            return -1f;
        }

        return Mathf.Max(0f, delaySeconds);
    }

    private static void SetPlaybackState(GameManager.BigScreenPlaybackState state)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 GameManager。");
            return;
        }

        manager.SetPlaybackState(state);
    }

    private static bool TrySetCarouselEnabled(bool enabled)
    {
        MapApi mapApi = MapApi.Instance;
        if (mapApi == null)
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 未找到 MapApi。");
            return false;
        }

        return mapApi.SetBigScreenAutoCarouselEnabled(enabled);
    }

    private void RefreshAllLabels()
    {
        RefreshStatusLabel();
        RefreshCountdownLabel();
        RefreshPlaybackStateLabel(GetCurrentPlaybackState());
    }

    private static GameManager.BigScreenPlaybackState GetCurrentPlaybackState()
    {
        GameManager manager = GameManager.Instance;
        return manager != null
            ? manager.CurrentPlaybackState
            : GameManager.BigScreenPlaybackState.Default;
    }

    private void RefreshStatusLabel()
    {
        if (_statusLabel == null)
        {
            return;
        }

        BigScreenCarouselController controller = BigScreenCarouselController.Instance;
        if (controller == null)
        {
            _statusLabel.text = "轮播：未找到控制器";
            SetButtonsInteractable(false, false, false, false, false);
            return;
        }

        string delayedFeatureText = controller.IsDelayedStartFeatureEnabled
            ? "延时判定：开启"
            : "延时判定：关闭";

        if (controller.IsWaitingDelayedStart)
        {
            _statusLabel.text =
                $"{delayedFeatureText} | 等待开轮播（{controller.RemainingDelayedStartSeconds:0.#}s）";
            SetButtonsInteractable(
                !controller.IsDelayedStartFeatureEnabled,
                controller.IsDelayedStartFeatureEnabled,
                true,
                true,
                true);
            return;
        }

        bool enabled = controller.IsAutoCarouselEnabled;
        _statusLabel.text = enabled
            ? $"{delayedFeatureText} | 轮播中（级别 {controller.LevelWaitSeconds:0}s / 部件 {controller.PartCycleWaitSeconds:0}s）"
            : $"{delayedFeatureText} | 轮播：已关闭";

        SetButtonsInteractable(
            !controller.IsDelayedStartFeatureEnabled,
            controller.IsDelayedStartFeatureEnabled,
            !enabled,
            enabled,
            controller.IsDelayedStartFeatureEnabled);
    }

    private void RefreshCountdownLabel()
    {
        if (_countdownLabel == null)
        {
            return;
        }

        BigScreenCarouselController controller = BigScreenCarouselController.Instance;
        if (controller == null)
        {
            _countdownLabel.text = "下次切换：--";
            return;
        }

        if (controller.IsWaitingDelayedStart)
        {
            _countdownLabel.text =
                $"延时开轮播：{FormatCountdown(controller.RemainingDelayedStartSeconds)}";
            return;
        }

        if (!controller.IsAutoCarouselEnabled)
        {
            _countdownLabel.text = "下次切换：--";
            return;
        }

        if (controller.IsCountingDownToNextSwitch)
        {
            string prefix = controller.IsPartCycleCountdown ? "循环等待" : "下次切换";
            _countdownLabel.text =
                $"{prefix}：{FormatCountdown(controller.RemainingSecondsUntilNextSwitch)}";
            return;
        }

        _countdownLabel.text = "下次切换：加载中...";
    }

    private void RefreshPlaybackStateLabel(GameManager.BigScreenPlaybackState state)
    {
        if (_playbackStateLabel == null)
        {
            return;
        }

        _playbackStateLabel.text =
            $"播放状态：{GameManager.GetPlaybackStateDisplayName(state)}";
    }

    private static string FormatCountdown(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainSeconds:00}";
    }

    private void SetButtonsInteractable(
        bool enableDelayedFeatureButton,
        bool disableDelayedFeatureButton,
        bool enableCarouselButton,
        bool disableCarouselButton,
        bool simulateWebGlButton)
    {
        if (_enableDelayedFeatureButton != null)
        {
            _enableDelayedFeatureButton.interactable = enableDelayedFeatureButton;
        }

        if (_disableDelayedFeatureButton != null)
        {
            _disableDelayedFeatureButton.interactable = disableDelayedFeatureButton;
        }

        if (_enableButton != null)
        {
            _enableButton.interactable = enableCarouselButton;
        }

        if (_disableButton != null)
        {
            _disableButton.interactable = disableCarouselButton;
        }

        if (_simulateWebGlCommunicationButton != null)
        {
            _simulateWebGlCommunicationButton.interactable = simulateWebGlButton;
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
