using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大屏自动轮播 Demo：开启/关闭四个大屏的定时轮播。
/// </summary>
[DisallowMultipleComponent]
public class DemoBigScreenCarouselUIDemo : MonoBehaviour
{
    [SerializeField] private Text _statusLabel;
    [SerializeField] private Text _countdownLabel;
    [SerializeField] private Button _enableButton;
    [SerializeField] private Button _disableButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_enableButton != null)
        {
            _enableButton.onClick.AddListener(OnEnableButtonClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.AddListener(OnDisableButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_enableButton != null)
        {
            _enableButton.onClick.RemoveListener(OnEnableButtonClicked);
        }

        if (_disableButton != null)
        {
            _disableButton.onClick.RemoveListener(OnDisableButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        RefreshStatusLabel();
        RefreshCountdownLabel();
    }

    private void Update()
    {
        RefreshStatusLabel();
        RefreshCountdownLabel();
    }

    private void OnEnableButtonClicked()
    {
        if (!TrySetCarouselEnabled(true))
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 开启自动轮播失败。");
        }
    }

    private void OnDisableButtonClicked()
    {
        if (!TrySetCarouselEnabled(false))
        {
            Debug.LogWarning("[DemoBigScreenCarouselUIDemo] 关闭自动轮播失败。");
        }
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

    private void RefreshStatusLabel()
    {
        if (_statusLabel == null)
        {
            return;
        }

        BigScreenCarouselController controller = BigScreenCarouselController.Instance;
        if (controller == null)
        {
            _statusLabel.text = "状态：未找到轮播控制器";
            SetButtonsInteractable(false, false);
            return;
        }

        bool enabled = controller.IsAutoCarouselEnabled;
        _statusLabel.text = enabled
            ? $"状态：已开启（级别 {controller.LevelWaitSeconds:0}s / 部件循环 {controller.PartCycleWaitSeconds:0}s）"
            : "状态：已关闭";

        SetButtonsInteractable(!enabled, enabled);
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

    private static string FormatCountdown(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainSeconds:00}";
    }

    private void SetButtonsInteractable(bool enableButton, bool disableButton)
    {
        if (_enableButton != null)
        {
            _enableButton.interactable = enableButton;
        }

        if (_disableButton != null)
        {
            _disableButton.interactable = disableButton;
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
