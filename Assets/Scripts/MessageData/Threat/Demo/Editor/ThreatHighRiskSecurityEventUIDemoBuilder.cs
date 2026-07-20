#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建威胁态势接口 Demo UI 面板（样式对齐原 Demo 菜单面板）。
/// </summary>
public static class ThreatHighRiskSecurityEventUIDemoBuilder
{
    public const string PanelName = "ThreatHighRiskSecurityEventPanel";
    public const string MenuLabel = "威胁态势接口";
    public const string UiTitle = "威胁态势接口 Demo";

    private const float PanelWidth = 360f;
    private const float RowHeight = 36f;
    private const float LabelWidth = 140f;
    private const float FieldWidth = 200f;
    private const float BackButtonHeight = 32f;
    private const int DemoTextFontSize = ThreatDemoUiStyle.FontSize;
    private const float ResultScrollHeight = 240f;

    /// <summary>由「Tools/Demo/刷新创建全部 Demo UI」统一调用，勿再单独加 MenuItem。</summary>
    public static GameObject CreatePanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Font demoUiFont)
    {
        GameObject panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        SetButtonText(backButtonGo, "返回");

        y -= BackButtonHeight + 8f;
        CreateLabel(panel.transform, UiTitle, 12f, y, PanelWidth - 24f, 28f, DemoTextFontSize, FontStyle.Bold);
        y -= 32f;

        GameObject statusLabelGo = CreateLabelObject(
            panel.transform,
            "StatusLabel",
            "就绪",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight * 3f,
            DemoTextFontSize,
            FontStyle.Normal);
        Text statusLabel = statusLabelGo.GetComponent<Text>();
        y -= RowHeight * 3f + 8f;

        InputField startTimeInput = CreateLabeledInputField(
            panel.transform,
            resources,
            "StartTimeInputRow",
            "开始时间",
            12f,
            y,
            ThreatQueryDefaults.StartTime);
        y -= RowHeight + 8f;

        InputField endTimeInput = CreateLabeledInputField(
            panel.transform,
            resources,
            "EndTimeInputRow",
            "结束时间",
            12f,
            y,
            ThreatQueryDefaults.EndTime);
        y -= RowHeight + 8f;

        float buttonWidth = PanelWidth - 24f;
        GameObject requestNationalButtonGo = CreateFullWidthButton(
            resources, panel.transform, "RequestNationalButton", "请求全国", 12f, y, buttonWidth);
        y -= 40f;
        GameObject refreshButtonGo = CreateFullWidthButton(resources, panel.transform, "RefreshListButton", "刷新列表", 12f, y, buttonWidth);
        y -= 40f;
        GameObject completeAlertButtonGo = CreateFullWidthButton(resources, panel.transform, "CompleteAlertButton", "完成当前告警", 12f, y, buttonWidth);
        y -= 48f;

        ScrollRect resultScroll = CreateResultScrollView(
            panel.transform,
            12f,
            y - ResultScrollHeight,
            buttonWidth,
            ResultScrollHeight,
            out Text resultListText);

        ThreatHighRiskSecurityEventUIDemo uiDemo = panel.AddComponent<ThreatHighRiskSecurityEventUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_startTimeInput").objectReferenceValue = startTimeInput;
        serializedDemo.FindProperty("_endTimeInput").objectReferenceValue = endTimeInput;
        serializedDemo.FindProperty("_requestNationalButton").objectReferenceValue =
            requestNationalButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_refreshListButton").objectReferenceValue = refreshButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_completeAlertButton").objectReferenceValue =
            completeAlertButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_statusLabel").objectReferenceValue = statusLabel;
        serializedDemo.FindProperty("_resultListText").objectReferenceValue = resultListText;
        serializedDemo.FindProperty("_resultScroll").objectReferenceValue = resultScroll;
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ThreatDemoUiStyle.ApplyPanelTree(panel, demoUiFont);
        return panel;
    }

    private static GameObject CreateFullWidthButton(
        DefaultControls.Resources resources,
        Transform parent,
        string name,
        string label,
        float x,
        float y,
        float width)
    {
        GameObject buttonGo = DefaultControls.CreateButton(resources);
        buttonGo.name = name;
        SetupChildRect(buttonGo, parent, x, y, width, 36f);
        SetButtonText(buttonGo, label);
        return buttonGo;
    }

    private static ScrollRect CreateResultScrollView(
        Transform parent,
        float x,
        float y,
        float width,
        float height,
        out Text resultListText)
    {
        GameObject scrollGo = new GameObject(
            "ResultScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 1f);
        scrollRectTransform.anchorMax = new Vector2(0f, 1f);
        scrollRectTransform.pivot = new Vector2(0f, 1f);
        scrollRectTransform.anchoredPosition = new Vector2(x, y + height);
        scrollRectTransform.sizeDelta = new Vector2(width, height);
        scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        SetupFullStretchRect(viewportRect);
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, height);

        GameObject textGo = new GameObject("ResultListText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(contentGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(-12f, 32f);

        resultListText = textGo.GetComponent<Text>();
        resultListText.text = "(暂无数据)";
        ThreatDemoUiStyle.ApplyResultText(resultListText);

        ContentSizeFitter fitter = textGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        return scrollRect;
    }

    private static InputField CreateLabeledInputField(
        Transform parent,
        DefaultControls.Resources resources,
        string rowName,
        string labelText,
        float x,
        float y,
        string defaultValue)
    {
        GameObject row = new GameObject(rowName, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetupChildRect(row, parent, x, y, LabelWidth + FieldWidth + 8f, RowHeight);
        CreateLabel(row.transform, labelText, 0f, 0f, LabelWidth, RowHeight, DemoTextFontSize, FontStyle.Normal);

        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = "InputField";
        inputGo.transform.SetParent(row.transform, false);
        RectTransform inputRect = inputGo.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(0f, 0.5f);
        inputRect.pivot = new Vector2(0f, 0.5f);
        inputRect.anchoredPosition = new Vector2(LabelWidth + 8f, 0f);
        inputRect.sizeDelta = new Vector2(FieldWidth, RowHeight);

        InputField inputField = inputGo.GetComponent<InputField>();
        string resolvedText = defaultValue ?? string.Empty;
        inputField.text = resolvedText;
        ThreatDemoUiStyle.ApplyInputField(inputField);
        return inputField;
    }

    private static void ApplyPanelLayout(RectTransform panelRect, ControlStateJumpPanelLayout.RectLayoutData layout)
    {
        panelRect.anchorMin = layout.AnchorMin;
        panelRect.anchorMax = layout.AnchorMax;
        panelRect.pivot = layout.Pivot;
        panelRect.anchoredPosition = layout.AnchoredPosition;
        panelRect.sizeDelta = layout.SizeDelta;
        panelRect.localScale = layout.LocalScale;
    }

    private static void SetupChildRect(GameObject child, Transform parent, float x, float y, float width, float height)
    {
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetupFullStretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetButtonText(GameObject buttonGo, string text)
    {
        Text label = buttonGo.GetComponentInChildren<Text>();
        if (label == null)
        {
            return;
        }

        label.text = text;
        ThreatDemoUiStyle.ApplyButtonLabel(label);
    }

    private static GameObject CreateLabelObject(
        Transform parent,
        string name,
        string text,
        float x,
        float y,
        float width,
        float height,
        int fontSize,
        FontStyle fontStyle)
    {
        GameObject labelGo = new GameObject(name, typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(parent, false);
        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);

        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.fontStyle = fontStyle;
        ThreatDemoUiStyle.ApplyPanelLabel(label);
        return labelGo;
    }

    private static void CreateLabel(
        Transform parent,
        string text,
        float x,
        float y,
        float width,
        float height,
        int fontSize,
        FontStyle fontStyle)
    {
        CreateLabelObject(parent, "Label", text, x, y, width, height, fontSize, fontStyle);
    }
}
#endif
