using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆态势数据控制：同参并发请求防护状态 + 攻击链路，全部成功后覆盖缓存。
/// 已在车辆级时立即开 UI 轮播；非车辆级时仅缓存，由 CarPanelManager 在进入车辆级后重启轮播。
/// </summary>
[DisallowMultipleComponent]
public class CarVehicleDataController : MonoBehaviour
{
    private static CarVehicleDataController _instance;

    public static CarVehicleDataController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CarVehicleDataController>(FindObjectsInactive.Include);
            }

            return _instance;
        }
    }

    [Header("引用（可留空，运行时查找）")]
    [SerializeField] private CarPanelManager _carPanelManager;
    [SerializeField] private AttackPathController _attackPathController;

    [Header("请求默认参数")]
    [SerializeField] private string _defaultEncryptVin = PartProtectionStatusRequest.DefaultEncryptVin;
    [SerializeField] private string _defaultStartTime = "";
    [SerializeField] private string _defaultEndTime = "2026-06-30 23:00:00";

    private bool _isRequesting;
    private int _pendingCount;
    private bool _partOk;
    private bool _attackOk;
    private PartProtectionStatusResponse _pendingPartResponse;
    private AttackChainResponse _pendingAttackResponse;
    private string _activeEncryptVin;
    private string _activeStartTime;
    private string _activeEndTime;
    private Action<bool, string> _onBatchCompleted;

    public bool IsRequesting => _isRequesting;
    public CarVehicleDataStore Store => CarVehicleDataStore.Instance;

    /// <summary>双接口均成功并覆盖缓存后触发。</summary>
    public event Action CacheAppliedAndUiMaybeShown;

    private void Awake()
    {
        _instance = this;
        if (_carPanelManager == null)
        {
            _carPanelManager = CarPanelManager.Instance;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>用默认参数请求双接口。</summary>
    public void RequestDefault(Action<bool, string> onCompleted = null)
    {
        Request(_defaultEncryptVin, _defaultStartTime, _defaultEndTime, onCompleted);
    }

    /// <summary>同参并发请求防护状态与攻击链路；均成功才覆盖缓存并尝试刷车辆 UI。</summary>
    public void Request(
        string encryptVin,
        string startTime,
        string endTime,
        Action<bool, string> onCompleted = null)
    {
        if (_isRequesting)
        {
            onCompleted?.Invoke(false, "已有请求进行中。");
            Debug.LogWarning("[CarVehicleDataController] 已有请求进行中，忽略。");
            return;
        }

        _isRequesting = true;
        _onBatchCompleted = onCompleted;
        _activeEncryptVin = string.IsNullOrWhiteSpace(encryptVin) ? _defaultEncryptVin : encryptVin.Trim();
        _activeStartTime = startTime ?? string.Empty;
        _activeEndTime = string.IsNullOrWhiteSpace(endTime) ? _defaultEndTime : endTime.Trim();
        _pendingCount = 2;
        _partOk = false;
        _attackOk = false;
        _pendingPartResponse = null;
        _pendingAttackResponse = null;

        Debug.Log(
            $"[CarVehicleDataController] 开始双接口请求 | vin={_activeEncryptVin} | " +
            $"start={_activeStartTime} | end={_activeEndTime}");

        PartProtectionStatusApi.Request(
            _activeEncryptVin,
            _activeStartTime,
            _activeEndTime,
            OnPartProtectionCompleted);

        AttackChainApi.Request(
            _activeEncryptVin,
            _activeStartTime,
            _activeEndTime,
            OnAttackChainCompleted);
    }

    /// <summary>跳过 HTTP：直接应用本地解析后的双响应（测试用）。</summary>
    public bool ApplyLocalResponses(
        PartProtectionStatusResponse partProtection,
        AttackChainResponse attackChain,
        out string errorMessage,
        string encryptVin = null,
        string startTime = null,
        string endTime = null)
    {
        errorMessage = null;
        if (partProtection == null || !partProtection.IsSuccess)
        {
            errorMessage = "防护状态响应无效。";
            return false;
        }

        if (attackChain == null || !attackChain.IsSuccess)
        {
            errorMessage = "攻击链路响应无效。";
            return false;
        }

        string vin = string.IsNullOrWhiteSpace(encryptVin) ? _defaultEncryptVin : encryptVin.Trim();
        string start = startTime ?? string.Empty;
        string end = string.IsNullOrWhiteSpace(endTime) ? _defaultEndTime : endTime.Trim();

        CarVehicleDataStore.Instance.Replace(vin, start, end, partProtection, attackChain);
        OnCacheApplied();
        return true;
    }

    /// <summary>从本地 JSON 字符串应用双接口成功逻辑。</summary>
    public bool ApplyLocalJson(
        string partProtectionJson,
        string attackChainJson,
        out string errorMessage)
    {
        if (!PartProtectionStatusApi.TryParseResponse(partProtectionJson, out PartProtectionStatusResponse part, out string partError))
        {
            errorMessage = $"防护状态 JSON：{partError}";
            return false;
        }

        if (!AttackChainApi.TryParseResponse(attackChainJson, out AttackChainResponse attack, out string attackError))
        {
            errorMessage = $"攻击链路 JSON：{attackError}";
            return false;
        }

        return ApplyLocalResponses(part, attack, out errorMessage);
    }

    private void OnPartProtectionCompleted(HttpRequestResult result, PartProtectionStatusResponse response)
    {
        _partOk = result != null && result.IsSuccess && response != null && response.IsSuccess;
        _pendingPartResponse = _partOk ? response : null;
        if (!_partOk)
        {
            Debug.LogWarning("[CarVehicleDataController] 防护状态接口失败。");
        }

        CompleteOne();
    }

    private void OnAttackChainCompleted(HttpRequestResult result, AttackChainResponse response)
    {
        _attackOk = result != null && result.IsSuccess && response != null && response.IsSuccess;
        _pendingAttackResponse = _attackOk ? response : null;
        if (!_attackOk)
        {
            Debug.LogWarning("[CarVehicleDataController] 攻击链路接口失败。");
        }

        CompleteOne();
    }

    private void CompleteOne()
    {
        _pendingCount--;
        if (_pendingCount > 0)
        {
            return;
        }

        _isRequesting = false;
        if (!_partOk || !_attackOk)
        {
            string error = "双接口未全部成功，未覆盖缓存。";
            Debug.LogWarning($"[CarVehicleDataController] {error}");
            _onBatchCompleted?.Invoke(false, error);
            _onBatchCompleted = null;
            return;
        }

        CarVehicleDataStore.Instance.Replace(
            _activeEncryptVin,
            _activeStartTime,
            _activeEndTime,
            _pendingPartResponse,
            _pendingAttackResponse);

        OnCacheApplied();
        _onBatchCompleted?.Invoke(true, null);
        _onBatchCompleted = null;
    }

    /// <summary>
    /// 缓存写入后：车辆级立即轮播；攻击路径级立即加载链路；否则仅缓存待进入对应级别后展示。
    /// </summary>
    private void OnCacheApplied()
    {
        bool vehicleShown = TryShowVehicleUiFromCache();
        bool attackShown = TryShowAttackPathsFromCache();
        CacheAppliedAndUiMaybeShown?.Invoke();

        if (!vehicleShown && HasCarouselCache() && !IsVehicleLevel())
        {
            Debug.Log("[CarVehicleDataController] 零部件数据已缓存，待进入 VehicleLevel 后自动轮播。");
        }

        if (!attackShown && HasAttackPathCache() && !IsAttackPathLevel())
        {
            Debug.Log("[CarVehicleDataController] 攻击链路数据已缓存，待进入 AttackPathLevel 后自动展示。");
        }
    }

    /// <summary>
    /// 已在车辆级且有缓存时：通知 CarPanelManager 打开 UI 并重启零部件轮播。
    /// </summary>
    public bool TryShowVehicleUiFromCache()
    {
        if (!HasCarouselCache())
        {
            return false;
        }

        if (!IsVehicleLevel())
        {
            return false;
        }

        if (_carPanelManager == null)
        {
            _carPanelManager = CarPanelManager.Instance;
        }

        if (_carPanelManager == null)
        {
            Debug.LogWarning("[CarVehicleDataController] 未找到 CarPanelManager。");
            return false;
        }

        bool opened = _carPanelManager.StartPartMessageCarouselFromCache();
        if (opened)
        {
            Debug.Log("[CarVehicleDataController] 已通知 CarPanelManager 开始/重启零部件轮播。");
        }

        return opened;
    }

    /// <summary>
    /// 已在攻击路径级且有攻击链路缓存时：IP 映射为零件名并绘制全部连线。
    /// </summary>
    public bool TryShowAttackPathsFromCache()
    {
        if (!IsAttackPathLevel())
        {
            return false;
        }

        return ApplyAttackPathsFromCache();
    }

    /// <summary>
    /// 车辆→攻击路径过渡完成时调用：不校验级别（此时 GameManager 尚未切到 AttackPathLevel）。
    /// </summary>
    public bool ApplyAttackPathsFromCacheForTransition()
    {
        return ApplyAttackPathsFromCache();
    }

    private bool ApplyAttackPathsFromCache()
    {
        if (!HasAttackPathCache())
        {
            return false;
        }

        AttackPathController controller = ResolveAttackPathController();
        if (controller == null)
        {
            Debug.LogWarning("[CarVehicleDataController] 未找到 AttackPathController。");
            return false;
        }

        List<AttackChainPathEntry> entries = Store.BuildAttackPathEntries();
        if (entries.Count == 0)
        {
            Debug.LogWarning("[CarVehicleDataController] 攻击链路缓存无有效映射条目。");
            return false;
        }

        controller.gameObject.SetActive(true);
        int added = controller.ApplyPartLinks(entries);
        if (added > 0)
        {
            Debug.Log($"[CarVehicleDataController] 已加载 {added} 条攻击路径。");
        }

        VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
        if (transition != null)
        {
            transition.ShowPartsByNames(Store.BuildAttackChainNodePartNames());
            transition.SetPartNameLabelsVisible(true);
        }

        return added > 0;
    }

    private AttackPathController ResolveAttackPathController()
    {
        if (_attackPathController == null)
        {
            _attackPathController = FindFirstObjectByType<AttackPathController>(FindObjectsInactive.Include);
        }

        return _attackPathController;
    }

    private static bool HasAttackPathCache()
    {
        CarVehicleDataStore store = CarVehicleDataStore.Instance;
        return store.HasCache && store.HasAttackPathEntries();
    }

    private static bool IsAttackPathLevel()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentState == GameManager.ControlState.AttackPathLevel;
    }

    private static bool HasCarouselCache()
    {
        CarVehicleDataStore store = CarVehicleDataStore.Instance;
        return store.HasCache && store.BuildPartSlides().Count > 0;
    }

    private static bool IsVehicleLevel()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentState == GameManager.ControlState.VehicleLevel;
    }
}
