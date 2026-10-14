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
    [Tooltip("空则使用当日 00:00:00")]
    [SerializeField] private string _defaultStartTime = "";
    [Tooltip("已废弃：不填/空 endTime 一律当前时间，不再使用本字段。")]
    [SerializeField] private string _defaultEndTime = "";

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
    /// <summary>当前批次号；作废/新请求时递增，旧回调因不匹配而丢弃。</summary>
    private int _activeBatchId;
    private int _batchSeq;

    public bool IsRequesting => _isRequesting;

    /// <summary>当前进行中或最近一次请求的 encryptVin（未请求过可为空）。</summary>
    public string ActiveEncryptVin => _activeEncryptVin;

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

    /// <summary>用默认参数请求双接口（endTime 固定为当前时间）。</summary>
    public void RequestDefault(Action<bool, string> onCompleted = null)
    {
        Request(_defaultEncryptVin, _defaultStartTime, null, onCompleted);
    }

    /// <summary>
    /// 若有进行中的双接口请求：作废其回调与结果（旧 HTTP 回包忽略），再按新参数发起。
    /// 威胁下钻同省换 Vin 时使用，保证一定用新 Vin 访问后端。
    /// </summary>
    public void RequestReplacingPending(
        string encryptVin,
        string startTime,
        string endTime,
        Action<bool, string> onCompleted = null)
    {
        if (_isRequesting)
        {
            CancelPendingRequest("RequestReplacingPending");
        }

        Request(encryptVin, startTime, endTime, onCompleted);
    }

    /// <summary>作废进行中的请求：旧回调收到失败；迟到的 HTTP 回包因 batchId 不匹配而忽略。</summary>
    public void CancelPendingRequest(string reason = null)
    {
        if (!_isRequesting && _onBatchCompleted == null)
        {
            return;
        }

        _activeBatchId = ++_batchSeq;
        Action<bool, string> oldCallback = _onBatchCompleted;
        _onBatchCompleted = null;
        _isRequesting = false;
        _pendingCount = 0;
        _partOk = false;
        _attackOk = false;
        _pendingPartResponse = null;
        _pendingAttackResponse = null;

        string reasonText = string.IsNullOrWhiteSpace(reason) ? "未说明" : reason.Trim();
        LogManager.LogBackendWarning(
            $"[CarVehicleDataController] 已作废进行中的车辆请求 | reason={reasonText} | " +
            $"vin={_activeEncryptVin}");

        oldCallback?.Invoke(false, "请求已作废（换 Vin 或新请求覆盖）。");
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
            LogManager.LogBackendWarning("[CarVehicleDataController] 已有请求进行中，忽略。");
            return;
        }

        _isRequesting = true;
        _onBatchCompleted = onCompleted;
        _activeEncryptVin = string.IsNullOrWhiteSpace(encryptVin) ? _defaultEncryptVin : encryptVin.Trim();
        _activeStartTime = BackendDateTimeTool.ResolveStartTimeOrToday(
            string.IsNullOrWhiteSpace(startTime) ? _defaultStartTime : startTime);
        // 不填/空串一律当前时间，不再回落 Inspector _defaultEndTime（场景曾写死 06-30）。
        _activeEndTime = BackendDateTimeTool.ResolveEndTimeOrNow(endTime);
        _pendingCount = 2;
        _partOk = false;
        _attackOk = false;
        _pendingPartResponse = null;
        _pendingAttackResponse = null;
        int batchId = ++_batchSeq;
        _activeBatchId = batchId;

        LogManager.LogBackend(
            $"[CarVehicleDataController] 开始双接口请求 | vin={_activeEncryptVin} | " +
            $"start={_activeStartTime} | end={_activeEndTime} | batch={batchId}");

        PartProtectionStatusApi.Request(
            _activeEncryptVin,
            _activeStartTime,
            _activeEndTime,
            (result, response) => OnPartProtectionCompleted(batchId, result, response));

        AttackChainApi.Request(
            _activeEncryptVin,
            _activeStartTime,
            _activeEndTime,
            (result, response) => OnAttackChainCompleted(batchId, result, response));
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
        string start = BackendDateTimeTool.ResolveStartTimeOrToday(
            string.IsNullOrWhiteSpace(startTime) ? _defaultStartTime : startTime);
        string end = BackendDateTimeTool.ResolveEndTimeOrNow(endTime);

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

    private void OnPartProtectionCompleted(
        int batchId,
        HttpRequestResult result,
        PartProtectionStatusResponse response)
    {
        if (batchId != _activeBatchId)
        {
            LogManager.LogBackend(
                $"[CarVehicleDataController] 忽略过期防护状态回包 | batch={batchId} | active={_activeBatchId}");
            return;
        }

        _partOk = result != null && result.IsSuccess && response != null && response.IsSuccess;
        _pendingPartResponse = _partOk ? response : null;
        if (!_partOk)
        {
            LogManager.LogBackendWarning("[CarVehicleDataController] 防护状态接口失败。");
        }

        CompleteOne(batchId);
    }

    private void OnAttackChainCompleted(
        int batchId,
        HttpRequestResult result,
        AttackChainResponse response)
    {
        if (batchId != _activeBatchId)
        {
            LogManager.LogBackend(
                $"[CarVehicleDataController] 忽略过期攻击链路回包 | batch={batchId} | active={_activeBatchId}");
            return;
        }

        _attackOk = result != null && result.IsSuccess && response != null && response.IsSuccess;
        _pendingAttackResponse = _attackOk ? response : null;
        if (!_attackOk)
        {
            LogManager.LogBackendWarning("[CarVehicleDataController] 攻击链路接口失败。");
        }

        CompleteOne(batchId);
    }

    private void CompleteOne(int batchId)
    {
        if (batchId != _activeBatchId)
        {
            return;
        }

        _pendingCount--;
        if (_pendingCount > 0)
        {
            return;
        }

        _isRequesting = false;
        if (!_partOk || !_attackOk)
        {
            string error = "双接口未全部成功，未覆盖缓存。";
            LogManager.LogBackendWarning($"[CarVehicleDataController] {error}");
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
    /// 缓存写入后：先按 partType 刷显示名（防护→攻击链路，后到覆盖），再刷 UI/链路。
    /// </summary>
    private void OnCacheApplied()
    {
        VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
        transition?.ApplyDisplayNamesFromVehicleDataStore(Store);

        bool vehicleShown = TryShowVehicleUiFromCache();
        bool attackShown = TryShowAttackPathsFromCache();
        CacheAppliedAndUiMaybeShown?.Invoke();

        if (!vehicleShown && HasCarouselCache() && !IsVehicleLevel())
        {
            LogManager.LogBackend("[CarVehicleDataController] 零部件数据已缓存，待进入 VehicleLevel 后自动轮播。");
        }

        if (!attackShown && HasAttackPathCache() && !IsAttackPathLevel())
        {
            LogManager.LogBackend("[CarVehicleDataController] 攻击链路数据已缓存，待进入 AttackPathLevel 后自动展示。");
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

        if (!CarPanelManager.IsDisplaySupportedOnCurrentPlatform)
        {
            LogManager.LogBackend("[CarVehicleDataController] 当前平台不展示 CarPanel/GridLine，跳过车辆 UI。");
            return false;
        }

        if (_carPanelManager == null)
        {
            _carPanelManager = CarPanelManager.Instance;
        }

        if (_carPanelManager == null)
        {
            LogManager.LogBackendWarning("[CarVehicleDataController] 未找到 CarPanelManager。");
            return false;
        }

        bool opened = _carPanelManager.StartPartMessageCarouselFromCache();
        if (opened)
        {
            LogManager.LogBackend("[CarVehicleDataController] 已通知 CarPanelManager 开始/重启零部件轮播。");
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
            LogManager.LogBackendWarning("[CarVehicleDataController] 未找到 AttackPathController。");
            return false;
        }

        List<AttackChainPathEntry> entries = Store.BuildAttackPathEntries();
        if (entries.Count == 0)
        {
            LogManager.LogBackendWarning("[CarVehicleDataController] 攻击链路缓存无有效映射条目。");
            return false;
        }

        controller.gameObject.SetActive(true);
        int added = controller.ApplyPartLinks(entries);
        if (added > 0)
        {
            LogManager.LogBackend($"[CarVehicleDataController] 已加载 {added} 条攻击路径。");
        }

        VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
        if (transition != null)
        {
            List<int> partTypes = Store.BuildAttackChainNodePartTypes();
            if (partTypes.Count > 0)
            {
                transition.ShowPartsByPartTypes(partTypes);
            }
            else
            {
                transition.ShowPartsByNames(Store.BuildAttackChainNodePartNames());
            }

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
