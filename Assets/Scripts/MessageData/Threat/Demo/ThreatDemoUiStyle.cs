using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 威胁接口 Demo 面板文字配色：深色面板用浅色字，输入框/按钮用深色字。
/// </summary>
public static class ThreatDemoUiStyle
{
    public const int FontSize = 12;

    /// <summary>面板标题、状态、字段名（深色半透明背景上）。</summary>
    public static readonly Color PanelTextColor = Color.white;

    /// <summary>输入框已输入内容（浅色输入框背景上）。</summary>
    public static readonly Color InputTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    /// <summary>输入框占位提示。</summary>
    public static readonly Color InputPlaceholderColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    /// <summary>按钮文字（浅色按钮背景上）。</summary>
    public static readonly Color ButtonTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    /// <summary>结果列表（深色滚动区域内，对齐 HttpApi JSON 区）。</summary>
    public static readonly Color ResultTextColor = new Color(0.85f, 0.92f, 0.85f, 1f);

    /// <summary>流程状态/倒计时（高亮）。</summary>
    public static readonly Color FlowStateTextColor = new Color(1f, 0.88f, 0.45f, 1f);

    public static void ApplyPanelLabel(Text text, Font font = null)
    {
        if (text == null)
        {
            return;
        }

        ApplyFont(text, font);
        text.color = PanelTextColor;
        ApplyWrap(text);
    }

    public static void ApplyInputField(InputField inputField, Font font = null)
    {
        if (inputField == null)
        {
            return;
        }

        if (inputField.textComponent != null)
        {
            ApplyFont(inputField.textComponent, font);
            inputField.textComponent.color = InputTextColor;
            ApplyWrap(inputField.textComponent);
        }

        if (inputField.placeholder is Text placeholder)
        {
            ApplyFont(placeholder, font);
            placeholder.color = InputPlaceholderColor;
            ApplyWrap(placeholder);
        }
    }

    public static void ApplyButtonLabel(Text text, Font font = null)
    {
        if (text == null)
        {
            return;
        }

        ApplyFont(text, font);
        text.color = ButtonTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        ApplyWrap(text);
        text.raycastTarget = false;
    }

    public static void ApplyResultText(Text text, Font font = null)
    {
        if (text == null)
        {
            return;
        }

        ApplyFont(text, font);
        text.color = ResultTextColor;
        text.alignment = TextAnchor.UpperLeft;
        ApplyWrap(text);
        text.raycastTarget = false;
    }

    public static void ApplyFlowStateLabel(Text text, Font font = null)
    {
        if (text == null)
        {
            return;
        }

        ApplyFont(text, font);
        text.color = FlowStateTextColor;
        text.fontStyle = FontStyle.Bold;
        ApplyWrap(text);
        text.raycastTarget = false;
    }

    public static void ApplyPanelTree(GameObject panelRoot, Font font = null)
    {
        if (panelRoot == null)
        {
            return;
        }

        Text[] texts = panelRoot.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (TryApplyAsInputOrPlaceholder(text, font))
            {
                continue;
            }

            if (text.GetComponentInParent<Button>() != null)
            {
                ApplyButtonLabel(text, font);
                continue;
            }

            if (text.name == "ResultListText")
            {
                ApplyResultText(text, font);
                continue;
            }

            if (text.name == "FlowStateLabel")
            {
                ApplyFlowStateLabel(text, font);
                continue;
            }

            ApplyPanelLabel(text, font);
        }

        InputField[] inputFields = panelRoot.GetComponentsInChildren<InputField>(true);
        for (int i = 0; i < inputFields.Length; i++)
        {
            ApplyInputField(inputFields[i], font);
        }
    }

    private static bool TryApplyAsInputOrPlaceholder(Text text, Font font)
    {
        InputField inputField = text.GetComponentInParent<InputField>();
        if (inputField == null)
        {
            return false;
        }

        if (inputField.placeholder == text)
        {
            ApplyFont(text, font);
            text.color = InputPlaceholderColor;
            ApplyWrap(text);
            return true;
        }

        if (inputField.textComponent == text)
        {
            ApplyFont(text, font);
            text.color = InputTextColor;
            ApplyWrap(text);
            return true;
        }

        return false;
    }

    private static void ApplyFont(Text text, Font font)
    {
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = FontSize;
    }

    private static void ApplyWrap(Text text)
    {
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
    }
}
