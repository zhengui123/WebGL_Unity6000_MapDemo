#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建「事件溯源」Demo UI 面板。
/// </summary>
public static class SecurityEventDetailUIDemoBuilder
{
    public const string PanelName = "SecurityEventDetailTestPanel";
    public const string MenuLabel = "事件溯源";
    public const string UiTitle = "事件溯源详情";

    private const float PanelWidth = 360f;
    private const float BackButtonHeight = 32f;
    private const float ResultScrollHeight = 200f;
    private const int DemoTextFontSize = ThreatDemoUiStyle.FontSize;

    /// <summary>由「Tools/Demo/刷新创建全部 Demo UI」统一调用。</summary>
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
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        SetButtonText(backButtonGo, "返回");

        y -= BackButtonHeight + 8f;
        CreateLabel(panel.transform, "Title", UiTitle, 12f, y, PanelWidth - 24f, 28f, FontStyle.Bold);
        y -= 32f;

        GameObject statusGo = CreateLabelObject(
            panel.transform, "StatusLabel", "就绪", 12f, y, PanelWidth - 24f, 56f, FontStyle.Normal);
        y -= 64f;

        float buttonWidth = PanelWidth - 24f;
        GameObject eventIdInput = CreateLabeledInput(
            resources, panel.transform, "EventId", "eventId", SecurityEventDetailRequest.DefaultEventId,
            12f, ref y, buttonWidth);
        GameObject startInput = CreateLabeledInput(
            resources, panel.transform, "ProcessStartTime", "processStartTime",
            SecurityEventDetailRequest.DefaultProcessStartTime, 12f, ref y, buttonWidth);
        GameObject endInput = CreateLabeledInput(
            resources, panel.transform, "ProcessEndTime", "processEndTime",
            SecurityEventDetailRequest.DefaultProcessEndTime, 12f, ref y, buttonWidth);
        GameObject tenantInput = CreateLabeledInput(
            resources, panel.transform, "TenantId", "tenantId",
            SecurityEventDetailRequest.DefaultTenantId.ToString(), 12f, ref y, buttonWidth);

        GameObject loadLocalGo = CreateFullWidthButton(
            resources, panel.transform, "LoadLocalJsonButton", "加载本地测试 JSON", 12f, y, buttonWidth);
        y -= 40f;
        GameObject requestGo = CreateFullWidthButton(
            resources, panel.transform, "RequestApiButton", "按图中参数请求接口", 12f, y, buttonWidth);
        y -= 40f;
        GameObject applyGo = CreateFullWidthButton(
            resources, panel.transform, "ApplyToGjPanelButton", "重新应用 GJ/POI", 12f, y, buttonWidth);
        y -= 40f;
        GameObject refreshGo = CreateFullWidthButton(
            resources, panel.transform, "RefreshButton", "刷新状态", 12f, y, buttonWidth);
        y -= 48f;

        ScrollRect resultScroll = CreateResultScrollView(
            panel.transform,
            12f,
            y - ResultScrollHeight,
            buttonWidth,
            ResultScrollHeight,
            out Text resultListText);

        SecurityEventDetailUIDemo uiDemo = panel.AddComponent<SecurityEventDetailUIDemo>();
        SerializedObject so = new SerializedObject(uiDemo);
        so.FindProperty("_eventIdInput").objectReferenceValue = eventIdInput.GetComponent<InputField>();
        so.FindProperty("_processStartTimeInput").objectReferenceValue = startInput.GetComponent<InputField>();
        so.FindProperty("_processEndTimeInput").objectReferenceValue = endInput.GetComponent<InputField>();
        so.FindProperty("_tenantIdInput").objectReferenceValue = tenantInput.GetComponent<InputField>();
        so.FindProperty("_loadLocalJsonButton").objectReferenceValue = loadLocalGo.GetComponent<Button>();
        so.FindProperty("_requestApiButton").objectReferenceValue = requestGo.GetComponent<Button>();
        so.FindProperty("_applyToGjPanelButton").objectReferenceValue = applyGo.GetComponent<Button>();
        so.FindProperty("_refreshButton").objectReferenceValue = refreshGo.GetComponent<Button>();
        so.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        so.FindProperty("_statusLabel").objectReferenceValue = statusGo.GetComponent<Text>();
        so.FindProperty("_resultListText").objectReferenceValue = resultListText;
        so.FindProperty("_navigator").objectReferenceValue = navigator;
        so.ApplyModifiedPropertiesWithoutUndo();

        ThreatDemoUiStyle.ApplyPanelTree(panel, demoUiFont);
        return panel;
    }

    private static GameObject CreateLabeledInput(
        DefaultControls.Resources resources,
        Transform parent,
        string name,
        string label,
        string defaultValue,
        float x,
        ref float y,
        float width)
    {
        CreateLabel(parent, name + "Label", label, x, y, width, 18f, FontStyle.Normal);
        y -= 20f;
        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = name + "Input";
        SetupChildRect(inputGo, parent, x, y, width, 30f);
        InputField field = inputGo.GetComponent<InputField>();
        if (field != null)
        {
            field.text = defaultValue ?? string.Empty;
            if (field.textComponent != null)
            {
                field.textComponent.fontSize = 12;
            }
        }

        y -= 36f;
        return inputGo;
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
            "ResultScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
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
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
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
        label.fontSize = DemoTextFontSize;
        ThreatDemoUiStyle.ApplyPanelLabel(label);
        return labelGo;
    }

    private static void CreateLabel(
        Transform parent,
        string name,
        string text,
        float x,
        float y,
        float width,
        float height,
        FontStyle fontStyle)
    {
        CreateLabelObject(parent, name, text, x, y, width, height, fontStyle);
    }
}
#endif
