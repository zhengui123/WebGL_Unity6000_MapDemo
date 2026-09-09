using System;

/// <summary>宿主切换场景 UI 语言的请求体。</summary>
[Serializable]
public struct SetUiLanguageRequest
{
    /// <summary>语言码：zh / zh-CN / en / en-US 等。</summary>
    public string language;
}
