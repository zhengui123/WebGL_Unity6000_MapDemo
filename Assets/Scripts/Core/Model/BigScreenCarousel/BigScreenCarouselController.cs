using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 四个大屏自动轮播：综合态势 → 区域态势 → 车辆态势 → 部件态势，循环切换。
/// 开启后按间隔调用 <see cref="ControlStateHierarchyTransitionController.TransitionToState"/> 跳转。
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

  private Coroutine _carouselCoroutine;
  private float _nextSwitchUnscaledTime = -1f;
  private bool _waitingAtPartCycle;

  public bool IsAutoCarouselEnabled => _autoCarouselEnabled;

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

  private void Start()
  {
    if (_autoCarouselEnabled)
    {
      StartCarouselRoutine();
    }
  }

  private void OnDisable()
  {
    StopCarouselRoutine();
  }

  /// <summary>开启或关闭自动轮播；关闭时立即停止计时。</summary>
  public void SetAutoCarouselEnabled(bool enabled)
  {
    if (_autoCarouselEnabled == enabled)
    {
      return;
    }

    _autoCarouselEnabled = enabled;
    if (_autoCarouselEnabled)
    {
      StartCarouselRoutine();
      Debug.Log("[BigScreenCarousel] 自动轮播已开启。");
      return;
    }

    StopCarouselRoutine();
    Debug.Log("[BigScreenCarousel] 自动轮播已关闭。");
  }

  /// <summary>立即切换到轮播序列中的下一个大屏。</summary>
  public bool TryAdvanceToNextScreen()
  {
    bool transitionStarted;
    return TryAdvanceToNextScreen(out transitionStarted);
  }

  private bool TryAdvanceToNextScreen(out bool hierarchyTransitionStarted)
  {
    hierarchyTransitionStarted = false;
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
