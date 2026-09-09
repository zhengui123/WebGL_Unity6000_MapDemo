using System;
using UnityEngine;

/// <summary>
/// 场景 UI 语言切换管理：Inspector 可改当前语言；对外 API / 宿主可调用切换。
/// 仅影响场景正式面板固定文案与关联表现（如高德瓦片语言），不翻译后端数据与 Demo 菜单。
/// </summary>
[DisallowMultipleComponent]
public class LanguageManager : UnitySingle<LanguageManager>
{
    [Header("当前语言")]
    [SerializeField] private UiLanguage _currentLanguage = UiLanguage.Chinese;

    [Header("调试")]
    [Tooltip("编辑器下修改上方语言时立即广播刷新（Play Mode）。")]
    [SerializeField] private bool _applyOnValidate = true;

    /// <summary>语言变更后触发（含 Start 首次广播）。</summary>
    public static event Action<UiLanguage> OnLanguageChanged;

    private UiLanguage _lastBroadcastLanguage;
    private bool _hasBroadcast;

    public UiLanguage CurrentLanguage => _currentLanguage;

    public override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        // 延后到 Start，避免其它组件尚未订阅就广播
        BroadcastLanguage(force: true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!_applyOnValidate || !Application.isPlaying)
        {
            return;
        }

        if (_hasBroadcast && _lastBroadcastLanguage == _currentLanguage)
        {
            return;
        }

        BroadcastLanguage(force: true);
    }
#endif

    /// <summary>切换语言；相同语言不重复广播。</summary>
    public bool SetLanguage(UiLanguage language)
    {
        if (_currentLanguage == language)
        {
            return true;
        }

        _currentLanguage = language;
        LogManager.LogFeature($"[LanguageManager] UI 语言 → {_currentLanguage}");
        BroadcastLanguage(force: true);
        return true;
    }

    /// <summary>
    /// 按语言码切换。支持：zh / zh-CN / chinese / en / en-US / english（大小写不敏感）。
    /// </summary>
    public bool SetLanguageByCode(string languageCode)
    {
        if (!TryParseLanguageCode(languageCode, out UiLanguage language))
        {
            LogManager.LogFeatureWarning($"[LanguageManager] 无法识别语言码：{languageCode}");
            return false;
        }

        return SetLanguage(language);
    }

    public string GetText(string key)
    {
        return UiLocaleTable.Get(key, _currentLanguage);
    }

    /// <summary>取文案；经单例 <see cref="Instance"/> 读取当前语言表。</summary>
    public static string Get(string key)
    {
        return Instance.GetText(key);
    }

    public static bool TryParseLanguageCode(string languageCode, out UiLanguage language)
    {
        language = UiLanguage.Chinese;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        string code = languageCode.Trim().ToLowerInvariant().Replace('_', '-');
        switch (code)
        {
            case "zh":
            case "zh-cn":
            case "zh-hans":
            case "cn":
            case "chinese":
            case "中文":
                language = UiLanguage.Chinese;
                return true;
            case "en":
            case "en-us":
            case "en-gb":
            case "english":
            case "eng":
                language = UiLanguage.English;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 从宿主参数解析语言码：支持纯字符串 <c>en-US</c> 或 JSON <c>{"language":"en-US"}</c>。
    /// </summary>
    public static bool TryExtractLanguageCodeFromHostArg(string arg, out string languageCode)
    {
        languageCode = null;
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        string trimmed = arg.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            SetUiLanguageRequest request = JsonUtility.FromJson<SetUiLanguageRequest>(trimmed);
            if (string.IsNullOrWhiteSpace(request.language))
            {
                return false;
            }

            languageCode = request.language.Trim();
            return true;
        }

        languageCode = trimmed;
        return true;
    }

    private void BroadcastLanguage(bool force)
    {
        if (!force && _hasBroadcast && _lastBroadcastLanguage == _currentLanguage)
        {
            return;
        }

        _lastBroadcastLanguage = _currentLanguage;
        _hasBroadcast = true;
        OnLanguageChanged?.Invoke(_currentLanguage);
    }

    [ContextMenu("切换为中文")]
    private void ContextSetChinese()
    {
        SetLanguage(UiLanguage.Chinese);
    }

    [ContextMenu("切换为英文")]
    private void ContextSetEnglish()
    {
        SetLanguage(UiLanguage.English);
    }
}
