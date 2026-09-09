using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中 GaodeMap（Online Maps + 高德瓦片）的控制脚本。
/// 随 <see cref="LanguageManager"/> 切换高德瓦片 <c>lang</c>（中文 zh_cn / 英文 en）。
/// 切换前等待瓦片空闲；无瓦片时只改 URL 不 Reset，避免 WebGL Dictionary 竞态。
/// </summary>
[DisallowMultipleComponent]
public class GaodeMapController : MonoBehaviour
{
    private const string DefaultChineseTileUrl =
        "https://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}";

    private const string DefaultEnglishTileUrl =
        "https://webrd01.is.autonavi.com/appmaptile?lang=en&size=1&scale=1&style=8&x={x}&y={y}&z={z}";

    [Header("引用（留空则自动查找场景中的 OnlineMaps）")]
    [SerializeField] private OnlineMaps _onlineMaps;

    [Header("瓦片语言 URL（随 UI 语言切换）")]
    [SerializeField] private string _chineseCustomProviderUrl = DefaultChineseTileUrl;
    [SerializeField] private string _englishCustomProviderUrl = DefaultEnglishTileUrl;

    [Header("瓦片切换")]
    [Tooltip("等待当前瓦片加载完成的最长时间（秒）；超时仍切换，避免一直卡住。")]
    [SerializeField] private float _tileIdleWaitTimeoutSeconds = 2f;

    private static GaodeMapController _instance;

    private bool _started;
    private UiLanguage? _pendingLanguage;
    private Coroutine _waitTilesIdleCoroutine;
    private Coroutine _deferredInitialApplyCoroutine;

    public static GaodeMapController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GaodeMapController>();
            }

            return _instance;
        }
    }

    public OnlineMaps OnlineMaps => _onlineMaps;

    private void Awake()
    {
        _instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        LanguageManager.OnLanguageChanged += HandleLanguageChanged;
        OnlineMapsTile.OnAllTilesLoaded += HandleAllTilesLoaded;

        if (_started)
        {
            QueueTileLanguage(ResolveCurrentUiLanguage());
        }
    }

    private void Start()
    {
        _started = true;
        QueueTileLanguage(ResolveCurrentUiLanguage());
        _deferredInitialApplyCoroutine = StartCoroutine(DeferredInitialApply());
    }

    private void OnDisable()
    {
        LanguageManager.OnLanguageChanged -= HandleLanguageChanged;
        OnlineMapsTile.OnAllTilesLoaded -= HandleAllTilesLoaded;
        StopWaitTilesIdleCoroutine();
        StopDeferredInitialApplyCoroutine();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 将地图中心定位到指定 WGS84 经纬度；zoom 不传则保持当前缩放级别。
    /// </summary>
    public void LocateTo(double longitude, double latitude, int? zoom = null)
    {
        if (!TryGetMap(out OnlineMaps map))
        {
            return;
        }

        if (zoom.HasValue)
        {
            map.SetPositionAndZoom(longitude, latitude, zoom.Value);
        }
        else
        {
            map.SetPosition(longitude, latitude);
        }
    }

    public void LocateTo(double longitude, double latitude, int zoom)
    {
        LocateTo(longitude, latitude, (int?)zoom);
    }

    public bool TryGetCenter(out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;

        if (!TryGetMap(out OnlineMaps map))
        {
            return false;
        }

        map.GetPosition(out longitude, out latitude);
        return true;
    }

    public int GetCurrentZoom()
    {
        return TryGetMap(out OnlineMaps map) ? map.zoom : 0;
    }

    /// <summary>请求按 UI 语言切换高德 CustomProviderUrl（空闲或超时后再提交）。</summary>
    public void ApplyTileLanguage(UiLanguage language)
    {
        QueueTileLanguage(language);
        TryApplyPendingTileLanguage(forceAfterTimeout: false);
    }

    private IEnumerator DeferredInitialApply()
    {
        // 等 OnlineMaps 过完首帧初始化，再尝试应用语言瓦片
        yield return null;
        _deferredInitialApplyCoroutine = null;
        TryApplyPendingTileLanguage(forceAfterTimeout: false);
    }

    private void QueueTileLanguage(UiLanguage language)
    {
        _pendingLanguage = language;
    }

    private void HandleLanguageChanged(UiLanguage language)
    {
        QueueTileLanguage(language);
        if (_started)
        {
            TryApplyPendingTileLanguage(forceAfterTimeout: false);
        }
    }

    private void HandleAllTilesLoaded()
    {
        if (!_pendingLanguage.HasValue)
        {
            return;
        }

        TryApplyPendingTileLanguage(forceAfterTimeout: false);
    }

    private void TryApplyPendingTileLanguage(bool forceAfterTimeout)
    {
        if (!_pendingLanguage.HasValue)
        {
            return;
        }

        if (!TryGetMap(out OnlineMaps map))
        {
            return;
        }

        UiLanguage language = _pendingLanguage.Value;
        string url = GetProviderUrl(language);
        if (string.IsNullOrWhiteSpace(url))
        {
            LogManager.LogFeatureWarning("[GaodeMapController] CustomProviderUrl 为空，跳过瓦片语言切换。");
            ClearPendingAndWait();
            return;
        }

        if (UrlsEqual(map.customProviderURL, url))
        {
            ClearPendingAndWait();
            return;
        }

        bool noTiles = HasNoTiles(map);
        bool idle = noTiles || AreTilesIdle(map);

        if (!forceAfterTimeout && !idle)
        {
            EnsureWaitTilesIdleCoroutine();
            return;
        }

        StopWaitTilesIdleCoroutine();
        _pendingLanguage = null;

        map.customProviderURL = url;

        // 尚无瓦片时只改 URL，避免初始化期 Reset 导致 WebGL Dictionary 崩溃
        if (!noTiles && map.tileManager != null)
        {
            map.tileManager.Reset();
        }

        map.RedrawImmediately();
        LogManager.LogFeature(
            $"[GaodeMapController] 瓦片语言 → {language} | lang={(language == UiLanguage.English ? "en" : "zh_cn")}" +
            (noTiles ? " | 无瓦片仅改URL" : string.Empty) +
            (forceAfterTimeout ? " | 超时强制" : string.Empty));
    }

    private void ClearPendingAndWait()
    {
        _pendingLanguage = null;
        StopWaitTilesIdleCoroutine();
    }

    private void EnsureWaitTilesIdleCoroutine()
    {
        if (_waitTilesIdleCoroutine != null)
        {
            return;
        }

        _waitTilesIdleCoroutine = StartCoroutine(WaitTilesIdleTimeout());
    }

    /// <summary>仅超时兜底；正常路径依赖 OnAllTilesLoaded。</summary>
    private IEnumerator WaitTilesIdleTimeout()
    {
        float timeout = Mathf.Max(0.1f, _tileIdleWaitTimeoutSeconds);
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (!_pendingLanguage.HasValue)
            {
                _waitTilesIdleCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _waitTilesIdleCoroutine = null;
        if (!_pendingLanguage.HasValue)
        {
            yield break;
        }

        LogManager.LogFeatureWarning(
            $"[GaodeMapController] 等待瓦片空闲超时（{timeout:0.##}s），强制切换语言瓦片。");
        TryApplyPendingTileLanguage(forceAfterTimeout: true);
    }

    private void StopWaitTilesIdleCoroutine()
    {
        if (_waitTilesIdleCoroutine == null)
        {
            return;
        }

        StopCoroutine(_waitTilesIdleCoroutine);
        _waitTilesIdleCoroutine = null;
    }

    private void StopDeferredInitialApplyCoroutine()
    {
        if (_deferredInitialApplyCoroutine == null)
        {
            return;
        }

        StopCoroutine(_deferredInitialApplyCoroutine);
        _deferredInitialApplyCoroutine = null;
    }

    private static bool HasNoTiles(OnlineMaps map)
    {
        if (map == null || map.tileManager == null)
        {
            return true;
        }

        List<OnlineMapsTile> tiles = map.tileManager.tiles;
        return tiles == null || tiles.Count == 0;
    }

    private static bool AreTilesIdle(OnlineMaps map)
    {
        if (HasNoTiles(map))
        {
            return true;
        }

        List<OnlineMapsTile> tiles = map.tileManager.tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            OnlineMapsTile tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            if (tile.status != OnlineMapsTileStatus.loaded
                && tile.status != OnlineMapsTileStatus.error
                && tile.status != OnlineMapsTileStatus.disposed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool UrlsEqual(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(a.Trim(), b.Trim(), System.StringComparison.Ordinal);
    }

    private string GetProviderUrl(UiLanguage language)
    {
        return language == UiLanguage.English
            ? _englishCustomProviderUrl
            : _chineseCustomProviderUrl;
    }

    private static UiLanguage ResolveCurrentUiLanguage()
    {
        return LanguageManager.Instance.CurrentLanguage;
    }

    private void ResolveReferences()
    {
        if (_onlineMaps != null)
        {
            return;
        }

        _onlineMaps = GetComponent<OnlineMaps>();
        if (_onlineMaps != null)
        {
            return;
        }

        _onlineMaps = GetComponentInChildren<OnlineMaps>(true);
        if (_onlineMaps != null)
        {
            return;
        }

        GameObject gaodeMapRoot = GameObject.Find("GaodeMap");
        if (gaodeMapRoot != null)
        {
            _onlineMaps = gaodeMapRoot.GetComponent<OnlineMaps>();
            if (_onlineMaps != null)
            {
                return;
            }
        }

        _onlineMaps = OnlineMaps.instance;
        if (_onlineMaps != null)
        {
            return;
        }

        _onlineMaps = FindFirstObjectByType<OnlineMaps>();
    }

    private bool TryGetMap(out OnlineMaps map)
    {
        ResolveReferences();
        map = _onlineMaps;
        if (map != null)
        {
            return true;
        }

        LogManager.LogFeatureWarning("[GaodeMapController] 未找到 OnlineMaps 组件。");
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("测试：定位到北京天安门")]
    private void EditorTestLocateBeijing()
    {
        LocateTo(116.397128, 39.916527, 15);
    }

    [ContextMenu("测试：应用中文瓦片")]
    private void EditorTestChineseTiles()
    {
        ApplyTileLanguage(UiLanguage.Chinese);
    }

    [ContextMenu("测试：应用英文瓦片")]
    private void EditorTestEnglishTiles()
    {
        ApplyTileLanguage(UiLanguage.English);
    }
#endif
}
