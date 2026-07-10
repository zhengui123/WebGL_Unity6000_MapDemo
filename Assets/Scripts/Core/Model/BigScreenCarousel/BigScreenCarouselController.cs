using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 四个大屏自动轮播：综合态势 → 区域态势 → 车辆态势 → 部件态势，循环切换。
/// 开启后按间隔调用 <see cref="ControlStateHierarchyTransitionController.TransitionToState"/> 跳转。
/// Android 真机版本不启用轮播（由 Android 宿主控制大屏切换）。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(210)]
public class BigScreenCarouselController : UnitySingle<BigScreenCarouselController>
{
  [Header("轮播")]
  [SerializeField] private bool _autoCarouselEnabled;
  [Tooltip("每个级别加载完成后的等待时间（秒）")]
  [FormerlySerializedAs("_intervalSeconds")]
  [SerializeField] private float _levelWaitSeconds = 10f;
  [Tooltip("到达部件级别后，整轮循环的等待时间（秒）")]
  [SerializeField] private float _partCycleWaitSeconds = 120f;
  [SerializeField] private bool _useInstantTransition = true;

  [Header("延时开启")]
  [Tooltip("是否启用延时开轮播判定（默认开启）")]
  [SerializeField] private bool _delayedStartFeatureEnabled = true;
  [Tooltip("延时开启轮播的默认等待时间（秒）")]
  [SerializeField] private float _defaultDelayedStartSeconds = 60f;

  private Coroutine _carouselCoroutine;
  private Coroutine _delayedStartCoroutine;
  private float _nextSwitchUnscaledTime = -1f;
  private float _delayedStartTargetUnscaledTime = -1f;
  private float _activeDelayedStartSeconds;
  private bool _waitingAtPartCycle;
  private bool _loggedAndroidCarouselDisabled;

  /// <summary>当前平台是否支持大屏自动轮播（Android 真机版本为 false）。</summary>
  public static bool IsCarouselSupportedOnCurrentPlatform
  {
    get
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return false;
#else
      return true;
#endif
    }
  }

  public bool IsAutoCarouselEnabled => _autoCarouselEnabled;

  public bool IsDelayedStartFeatureEnabled =>
    IsCarouselSupportedOnCurrentPlatform && _delayedStartFeatureEnabled;

  /// <summary>是否处于延时开启轮播的等待阶段。</summary>
  public bool IsWaitingDelayedStart => _delayedStartCoroutine != null;

  /// <summary>延时开启轮播剩余秒数；未等待时返回 0。</summary>
  public float RemainingDelayedStartSeconds
  {
    get
    {
      if (!IsWaitingDelayedStart)
      {
        return 0f;
      }

      return Mathf.Max(0f, _delayedStartTargetUnscaledTime - Time.unscaledTime);
    }
  }

  /// <summary>是否处于两次轮播之间的等待倒计时阶段。</summary>
  public bool IsCountingDownToNextSwitch =>
    _autoCarouselEnabled && _nextSwitchUnscaledTime >= 0f;

  /// <summary>当前倒计时是否为部件级整轮循环等待。</summary>
  public bool IsPartCycleCountdown => IsCountingDownToNextSwitch && _waitingAtPartCycle;

  /// <summary>距离下次自动切换剩余秒数；未开启或不在等待阶段时返回 0。</summary>
  public float RemainingSecondsUntilNextSwitch
  {
    get
    {
      if (!IsCountingDownToNextSwitch)
      {
        return 0f;
      }

      return Mathf.Max(0f, _nextSwitchUnscaledTime - Time.unscaledTime);
    }
  }

  public float LevelWaitSeconds
  {
    get => _levelWaitSeconds;
    set => _levelWaitSeconds = Mathf.Max(1f, value);
  }

  public float PartCycleWaitSeconds
  {
    get => _partCycleWaitSeconds;
    set => _partCycleWaitSeconds = Mathf.Max(1f, value);
  }

  public float DefaultDelayedStartSeconds
  {
    get => _defaultDelayedStartSeconds;
    set => _defaultDelayedStartSeconds = Mathf.Max(0f, value);
  }

  private void OnEnable()
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      return;
    }

    WebGLAPI.HostCommunicationReceived += HandleHostCommunicationReceived;
  }

  private void OnDisable()
  {
    if (IsCarouselSupportedOnCurrentPlatform)
    {
      WebGLAPI.HostCommunicationReceived -= HandleHostCommunicationReceived;
    }

    StopCarouselRoutine();
    CancelDelayedStart();
  }

  private void Start()
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      LogAndroidCarouselDisabledOnce();
      return;
    }

    if (_delayedStartFeatureEnabled)
    {
      ScheduleAutoCarouselStart(_defaultDelayedStartSeconds);
      return;
    }

    if (_autoCarouselEnabled)
    {
      BeginAutoCarouselAsEnabled();
    }
  }

  /// <summary>开启或关闭延时开轮播判定功能。</summary>
  public void SetDelayedStartFeatureEnabled(bool enabled)
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      LogAndroidCarouselDisabledOnce();
      return;
    }

    if (_delayedStartFeatureEnabled == enabled)
    {
      return;
    }

    _delayedStartFeatureEnabled = enabled;
    if (!enabled)
    {
      CancelDelayedStart();
      Debug.Log("[BigScreenCarousel] 延时开轮播功能已关闭。");
      return;
    }

    Debug.Log("[BigScreenCarousel] 延时开轮播功能已开启。");
    if (!_autoCarouselEnabled)
    {
      ScheduleAutoCarouselStart(_defaultDelayedStartSeconds);
    }
  }

  /// <summary>开启或关闭自动轮播；开启时若延时功能启用则走延时判定。</summary>
  public void SetAutoCarouselEnabled(bool enabled, bool bypassDelayedStart = false)
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      LogAndroidCarouselDisabledOnce();
      return;
    }

    if (!enabled)
    {
      CancelDelayedStart();
      if (_autoCarouselEnabled)
      {
        _autoCarouselEnabled = false;
        StopCarouselRoutine();
        Debug.Log("[BigScreenCarousel] 自动轮播已关闭。");
      }

      return;
    }

    if (_autoCarouselEnabled)
    {
      return;
    }

    if (_delayedStartFeatureEnabled && !bypassDelayedStart)
    {
      ScheduleAutoCarouselStart(_defaultDelayedStartSeconds);
      return;
    }

    BeginAutoCarouselAsEnabled();
  }

  /// <summary>指定秒数后开启自动轮播。</summary>
  public void ScheduleAutoCarouselStart(float delaySeconds)
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      LogAndroidCarouselDisabledOnce();
      return;
    }

    if (!_delayedStartFeatureEnabled)
    {
      Debug.LogWarning("[BigScreenCarousel] 延时开轮播功能已关闭，改为立即开启。");
      SetAutoCarouselEnabled(true, bypassDelayedStart: true);
      return;
    }

    CancelDelayedStart();

    if (_autoCarouselEnabled)
    {
      _autoCarouselEnabled = false;
      StopCarouselRoutine();
    }

    _activeDelayedStartSeconds = Mathf.Max(0f, delaySeconds);
    if (_activeDelayedStartSeconds <= 0f)
    {
      BeginAutoCarouselAsEnabled();
      return;
    }

    _delayedStartCoroutine = StartCoroutine(DelayedStartRoutine(_activeDelayedStartSeconds));
    Debug.Log($"[BigScreenCarousel] 已预约 {_activeDelayedStartSeconds:0.#}s 后开启轮播。");
  }

  /// <summary>取消尚未开始的延时开启。</summary>
  public void CancelDelayedStart()
  {
    if (_delayedStartCoroutine == null)
    {
      return;
    }

    StopCoroutine(_delayedStartCoroutine);
    _delayedStartCoroutine = null;
    _delayedStartTargetUnscaledTime = -1f;
    Debug.Log("[BigScreenCarousel] 已取消延时开启轮播。");
  }

  /// <summary>
  /// 收到宿主通信：轮播中则暂停并进入延时重开；已在延时等待则重置计时。
  /// </summary>
  public void HandleHostCommunication()
  {
    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      return;
    }

    if (!_delayedStartFeatureEnabled)
    {
      return;
    }

    if (_autoCarouselEnabled)
    {
      _autoCarouselEnabled = false;
      StopCarouselRoutine();
      Debug.Log("[BigScreenCarousel] 收到宿主通信，已暂停自动轮播并进入延时重开判定。");
    }

    float delaySeconds = _activeDelayedStartSeconds > 0f
      ? _activeDelayedStartSeconds
      : _defaultDelayedStartSeconds;
    ScheduleAutoCarouselStart(delaySeconds);
  }

  /// <summary>立即切换到轮播序列中的下一个大屏。</summary>
  public bool TryAdvanceToNextScreen()
  {
    bool transitionStarted;
    return TryAdvanceToNextScreen(out transitionStarted);
  }

  private void HandleHostCommunicationReceived(string method, string arg)
  {
    HandleHostCommunication();
  }

  private void LogAndroidCarouselDisabledOnce()
  {
    if (_loggedAndroidCarouselDisabled)
    {
      return;
    }

    _loggedAndroidCarouselDisabled = true;
    Debug.Log("[BigScreenCarousel] Android 版本不启用自动轮播，由宿主控制大屏切换。");
  }

  private void BeginAutoCarouselAsEnabled()
  {
    _autoCarouselEnabled = true;
    BeginAutoCarousel();
  }

  private void BeginAutoCarousel()
  {
    SetPlaybackStateDefault();
    StartCarouselRoutine();
    Debug.Log("[BigScreenCarousel] 自动轮播已开启，大屏播放状态 → 默认。");
  }

  private static void SetPlaybackStateDefault()
  {
    GameManager manager = GameManager.Instance;
    manager?.SetPlaybackState(GameManager.BigScreenPlaybackState.Default);
  }

  private IEnumerator DelayedStartRoutine(float delaySeconds)
  {
    _delayedStartTargetUnscaledTime = Time.unscaledTime + delaySeconds;
    while (Time.unscaledTime < _delayedStartTargetUnscaledTime)
    {
      yield return null;
    }

    _delayedStartCoroutine = null;
    _delayedStartTargetUnscaledTime = -1f;

    if (_autoCarouselEnabled)
    {
      yield break;
    }

    BeginAutoCarouselAsEnabled();
  }

  private bool TryAdvanceToNextScreen(out bool hierarchyTransitionStarted)
  {
    hierarchyTransitionStarted = false;

    if (!IsCarouselSupportedOnCurrentPlatform)
    {
      return false;
    }

    GameManager manager = GameManager.Instance;
    if (manager == null)
    {
      Debug.LogWarning("[BigScreenCarousel] 未找到 GameManager。");
      return false;
    }

    BigScreenCarouselType current = BigScreenCarouselScreenMap.FromControlState(manager.CurrentState);
    BigScreenCarouselType next = BigScreenCarouselScreenMap.GetNext(current);
    return TryTransitionToScreen(next, out hierarchyTransitionStarted);
  }

  private void StartCarouselRoutine()
  {
    StopCarouselRoutine();
    _carouselCoroutine = StartCoroutine(AutoCarouselRoutine());
  }

  private void StopCarouselRoutine()
  {
    if (_carouselCoroutine == null)
    {
      return;
    }

    StopCoroutine(_carouselCoroutine);
    _carouselCoroutine = null;
    _nextSwitchUnscaledTime = -1f;
    _waitingAtPartCycle = false;
  }

  private IEnumerator AutoCarouselRoutine()
  {
    while (_autoCarouselEnabled)
    {
      bool hierarchyTransitionStarted;
      TryAdvanceToNextScreen(out hierarchyTransitionStarted);

      if (!_autoCarouselEnabled)
      {
        yield break;
      }

      yield return WaitUntilTransitionFullyCompleted(hierarchyTransitionStarted);

      if (!_autoCarouselEnabled)
      {
        yield break;
      }

      BigScreenCarouselType currentScreen = GetCurrentScreenType();
      bool isPartCycleWait = currentScreen == BigScreenCarouselType.Part;
      float waitSeconds = Mathf.Max(
        1f,
        isPartCycleWait ? _partCycleWaitSeconds : _levelWaitSeconds);
      yield return WaitForCountdown(waitSeconds, isPartCycleWait);
    }
  }

  private IEnumerator WaitForCountdown(float waitSeconds, bool isPartCycleWait)
  {
    _waitingAtPartCycle = isPartCycleWait;
    _nextSwitchUnscaledTime = Time.unscaledTime + waitSeconds;
    while (_autoCarouselEnabled && Time.unscaledTime < _nextSwitchUnscaledTime)
    {
      yield return null;
    }

    _nextSwitchUnscaledTime = -1f;
    _waitingAtPartCycle = false;
  }

  private static BigScreenCarouselType GetCurrentScreenType()
  {
    GameManager manager = GameManager.Instance;
    if (manager == null)
    {
      return BigScreenCarouselType.Comprehensive;
    }

    return BigScreenCarouselScreenMap.FromControlState(manager.CurrentState);
  }

  private static IEnumerator WaitUntilTransitionFullyCompleted(bool hierarchyTransitionStarted)
  {
    ControlStateHierarchyTransitionController controller =
      ControlStateHierarchyTransitionController.Instance;
    if (controller == null)
    {
      yield break;
    }

    if (hierarchyTransitionStarted)
    {
      const float bootstrapStartTimeoutSeconds = 3f;
      float elapsed = 0f;
      while (!controller.IsBootstrapping && elapsed < bootstrapStartTimeoutSeconds)
      {
        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      while (controller.IsBootstrapping)
      {
        yield return null;
      }
    }

    while (ControlStateHierarchyTransitionController.IsAnyTransitionAnimationBusy())
    {
      yield return null;
    }
  }

  private bool TryTransitionToScreen(
    BigScreenCarouselType screenType,
    out bool hierarchyTransitionStarted)
  {
    hierarchyTransitionStarted = false;
    ControlStateHierarchyTransitionController controller =
      ControlStateHierarchyTransitionController.Instance;
    if (controller == null)
    {
      Debug.LogWarning("[BigScreenCarousel] 未找到 ControlStateHierarchyTransitionController。");
      return false;
    }

    if (controller.IsBootstrapping)
    {
      Debug.LogWarning("[BigScreenCarousel] 正在跳转中，跳过本次轮播。");
      return false;
    }

    GameManager.ControlState targetState = BigScreenCarouselScreenMap.ToControlState(screenType);
    GameManager manager = GameManager.Instance;
    GameManager.ControlState currentState = manager != null
      ? manager.CurrentState
      : GameManager.ControlState.EarthLevel;

    if (currentState == targetState)
    {
      Debug.Log($"[BigScreenCarousel] 已在 {BigScreenCarouselScreenMap.GetDisplayName(screenType)}，跳过。");
      return true;
    }

    hierarchyTransitionStarted = controller.TransitionToState(_useInstantTransition, targetState);
    if (hierarchyTransitionStarted)
    {
      Debug.Log(
        $"[BigScreenCarousel] 轮播切换：{BigScreenCarouselScreenMap.GetDisplayName(screenType)}（{targetState}）。");
    }

    return hierarchyTransitionStarted;
  }
}
