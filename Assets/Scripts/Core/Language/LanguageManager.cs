using System;
using UnityEngine;

/// <summary>
/// 场景 UI 语言切换管理：Inspector 可改当前语言；对外 API / 宿主可调用切换。
/// 仅影响场景正式面板固定文案，不翻译后端数据与 Demo 菜单。
/// </summary>
[DisallowMultipleComponent]
public class LanguageManager : UnitySingle<LanguageManager>
{
    [Header("当前语言")]
    [SerializeField] private UiLanguage _currentLanguage = UiLanguage.Chinese;

    [Header("调试")]
    [Tooltip("编辑器下修改上方语言时立即广播刷新（Play Mode）。")]
    [SerializeField] private bool _applyOnValidate = true;

    /// <summary>语言变更后触发（含启动首次 Set）。</summary>
    public static event Action<UiLanguage> OnLanguageChanged;

    public UiLanguage CurrentLanguage => _currentLanguage;

    public override void Awake()
    {
        base.Awake();
        // 启动时广播一次，让已存在面板按当前语言刷新标签
        OnLanguageChanged?.Invoke(_currentLanguage);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!_applyOnValidate || !Application.isPlaying)
        {
            return;
        }

        OnLanguageChanged?.Invoke(_currentLanguage);
    }
#endif

    /// <summary>切换语言；相同则仍返回 true 且不重复广播。</summary>
    public bool SetLanguage(UiLanguage language)
    {
        if (_currentLanguage == language)
        {
            return true;
        }

        _currentLanguage = language;
        LogManager.LogFeature($"[LanguageManager] UI 语言 → {_currentLanguage}");
        OnLanguageChanged?.Invoke(_currentLanguage);
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

    public static string Get(string key)
    {
        LanguageManager manager = Instance;
        if (manager != null)
        {
            return manager.GetText(key);
        }

        return UiLocaleTable.Get(key, UiLanguage.Chinese);
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
