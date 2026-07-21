#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建「本地威胁测试」Demo UI 面板。
/// </summary>
public static class ThreatLocalAlertTestUIDemoBuilder
{
    public const string PanelName = "ThreatLocalAlertTestPanel";
    public const string MenuLabel = "本地威胁测试";
    public const string UiTitle = "本地威胁测试";

    private const float PanelWidth = 360f;
    private const float BackButtonHeight = 32f;
    private const int DemoTextFontSize = ThreatDemoUiStyle.FontSize;
    private const float ResultScrollHeight = 220f;

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

        GameObject flowStateLabelGo = CreateLabelObject(
            panel.transform,
            "FlowStateLabel",
            "流程：空闲",
            12f,
            y,
            PanelWidth - 24f,
            36f,
            DemoTextFontSize,
            FontStyle.Bold);
        Text flowStateLabel = flowStateLabelGo.GetComponent<Text>();
        flowStateLabel.color = ThreatDemoUiStyle.FlowStateTextColor;
        y -= 40f;

        GameObject statusLabelGo = CreateLabelObject(
            panel.transform,
            "StatusLabel",
            "就绪",
            12f,
            y,
            PanelWidth - 24f,
            72f,
            DemoTextFontSize,
            FontStyle.Normal);
        Text statusLabel = statusLabelGo.GetComponent<Text>();
        y -= 80f;

        float buttonWidth = PanelWidth - 24f;
        GameObject injectMultiGo = CreateFullWidthButton(
            resources, panel.transform, "InjectMultiProvinceButton", "注入多省达标 JSON", 12f, y, buttonWidth);
        y -= 40f;
        GameObject injectVinGo = CreateFullWidthButton(
            resources, panel.transform, "InjectSameVinButton", "注入多省多车 Vin≥3 JSON", 12f, y, buttonWidth);
        y -= 40f;
        GameObject skipHoldGo = CreateFullWidthButton(
            resources, panel.transform, "SkipHoldButton", "跳过停留", 12f, y, buttonWidth);
        y -= 40f;
        GameObject clearExcludedGo = CreateFullWidthButton(
            resources, panel.transform, "ClearExcludedButton", "清空排除 eventId", 12f, y, buttonWidth);
        y -= 40f;
        GameObject refreshGo = CreateFullWidthButton(
            resources, panel.transform, "RefreshListButton", "刷新状态", 12f, y, buttonWidth);
        y -= 40f;
        GameObject resetGo = CreateFullWidthButton(
            resources, panel.transform, "ResetFlowButton", "重置流程/缓存", 12f, y, buttonWidth);
        y -= 48f;

        ScrollRect resultScroll = CreateResultScrollView(
            panel.transform,
            12f,
            y - ResultScrollHeight,
            buttonWidth,
            ResultScrollHeight,
            out Text resultListText);

        ThreatLocalAlertTestUIDemo uiDemo = panel.AddComponent<ThreatLocalAlertTestUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_injectMultiProvinceButton").objectReferenceValue =
            injectMultiGo.GetComponent<Button>();
        serializedDemo.FindProperty("_injectSameVinButton").objectReferenceValue =
            injectVinGo.GetComponent<Button>();
        serializedDemo.FindProperty("_skipHoldButton").objectReferenceValue =
            skipHoldGo.GetComponent<Button>();
        serializedDemo.FindProperty("_clearExcludedButton").objectReferenceValue =
            clearExcludedGo.GetComponent<Button>();
        serializedDemo.FindProperty("_refreshButton").objectReferenceValue =
            refreshGo.GetComponent<Button>();
        serializedDemo.FindProperty("_resetFlowButton").objectReferenceValue =
            resetGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue =
            backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_flowStateLabel").objectReferenceValue = flowStateLabel;
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
