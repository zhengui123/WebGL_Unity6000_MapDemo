#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 创建「车辆态势数据测试」Demo UI 面板（样式对齐原 Demo 菜单面板）。
/// </summary>
public static class CarVehicleDataUIDemoBuilder
{
    public const string PanelName = "CarVehicleDataTestPanel";
    public const string MenuLabel = "车辆态势数据测试";
    public const string UiTitle = "车辆态势数据测试";

    private const string DemoUiRootName = "DemoGameStateUIRoot";
    private const string DemoUiFontPath = "Assets/Font/GeourceAltCHT-Medium.ttf";
    private const float PanelWidth = 360f;
    private const float BackButtonHeight = 32f;

    [MenuItem("Tools/Demo/创建车辆态势数据测试 Demo UI")]
    public static void CreateStandaloneDemoUi()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[CarVehicleDataUIDemoBuilder] 场景中未找到 Canvas。");
            return;
        }

        EnsureEventSystem();
        Transform parent = canvas.transform;
        Transform demoRoot = canvas.transform.Find(DemoUiRootName);
        if (demoRoot != null)
        {
            parent = demoRoot;
        }

        Transform existing = parent.Find(PanelName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        DefaultControls.Resources resources = CreateUiResources();
        Font font = AssetDatabase.LoadAssetAtPath<Font>(DemoUiFontPath);
        DemoGameStateUINavigator navigator = parent.GetComponent<DemoGameStateUINavigator>();

        GameObject panel = CreatePanel(
            parent,
            resources,
            ControlStateJumpPanelLayout.CreateDefault(),
            navigator,
            font);
        panel.SetActive(true);

        Undo.RegisterCreatedObjectUndo(panel, "Create Car Vehicle Data Test Panel");
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[CarVehicleDataUIDemoBuilder] 车辆态势数据测试 Demo UI 已创建。");
    }

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

        CarVehicleDataController controller = panel.AddComponent<CarVehicleDataController>();
        CarVehicleDataUIDemo demo = panel.AddComponent<CarVehicleDataUIDemo>();

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        SetButtonText(backButtonGo, "返回", demoUiFont, 14);

        y -= BackButtonHeight + 8f;
        CreateLabel(panel.transform, "Title", UiTitle, demoUiFont, 16f, ref y, 28f);

        InputField vin = CreateLabeledInput(
            panel.transform, resources, demoUiFont, "EncryptVin", PartProtectionStatusRequest.DefaultEncryptVin, ref y);
        InputField start = CreateLabeledInput(
            panel.transform, resources, demoUiFont, "StartTime", string.Empty, ref y);
        InputField end = CreateLabeledInput(
            panel.transform, resources, demoUiFont, "EndTime", "2026-06-30 23:00:00", ref y);

        Button httpBtn = CreateButton(
            panel.transform, resources, demoUiFont, "RequestHttpButton", "请求双接口(HTTP)", ref y);
        Button localBtn = CreateButton(
            panel.transform, resources, demoUiFont, "ApplyLocalJsonButton", "应用本地JSON", ref y);
        Button showBtn = CreateButton(
            panel.transform, resources, demoUiFont, "ShowUiFromCacheButton", "从缓存打开车辆UI", ref y);
        Button closeBtn = CreateButton(
            panel.transform, resources, demoUiFont, "CloseCarUiButton", "关闭车辆UI/停止轮播", ref y);

        Text result = CreateLabel(panel.transform, "ResultText", "就绪", demoUiFont, 13f, ref y, 120f);
        result.alignment = TextAnchor.UpperLeft;

        SerializedObject so = new SerializedObject(demo);
        so.FindProperty("_controller").objectReferenceValue = controller;
        so.FindProperty("_encryptVinInput").objectReferenceValue = vin;
        so.FindProperty("_startTimeInput").objectReferenceValue = start;
        so.FindProperty("_endTimeInput").objectReferenceValue = end;
        so.FindProperty("_resultText").objectReferenceValue = result;
        so.FindProperty("_requestHttpButton").objectReferenceValue = httpBtn;
        so.FindProperty("_applyLocalJsonButton").objectReferenceValue = localBtn;
        so.FindProperty("_showUiFromCacheButton").objectReferenceValue = showBtn;
        so.FindProperty("_closeCarUiButton").objectReferenceValue = closeBtn;
        so.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        so.FindProperty("_navigator").objectReferenceValue = navigator;
        so.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    private static Text CreateLabel(Transform parent, string name, string text, Font font, float fontSize, ref float y, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, height);
        Text t = go.GetComponent<Text>();
        t.text = text;
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = Mathf.RoundToInt(fontSize);
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        y -= height + 8f;
        return t;
    }

    private static InputField CreateLabeledInput(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        string label,
        string defaultValue,
        ref float y)
    {
        CreateLabel(parent, label + "Label", label, font, 12f, ref y, 18f);
        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = label + "Input";
        inputGo.transform.SetParent(parent, false);
        RectTransform rect = inputGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, 28f);
        InputField field = inputGo.GetComponent<InputField>();
        field.text = defaultValue ?? string.Empty;
        Text placeholder = field.placeholder as Text;
        if (placeholder != null && font != null)
        {
            placeholder.font = font;
        }

        if (field.textComponent != null && font != null)
        {
            field.textComponent.font = font;
            field.textComponent.fontSize = 13;
        }

        y -= 36f;
        return field;
    }

    private static Button CreateButton(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        string name,
        string label,
        ref float y)
    {
        GameObject buttonGo = DefaultControls.CreateButton(resources);
        buttonGo.name = name;
        buttonGo.transform.SetParent(parent, false);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-24f, 32f);
        SetButtonText(buttonGo, label, font, 14);
        y -= 40f;
        return buttonGo.GetComponent<Button>();
    }

    private static void SetButtonText(GameObject buttonGo, string label, Font font, int fontSize)
    {
        Text text = buttonGo.GetComponentInChildren<Text>();
        if (text == null)
        {
            return;
        }

        text.text = label;
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = fontSize;
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

    private static DefaultControls.Resources CreateUiResources()
    {
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }
}
#endif
