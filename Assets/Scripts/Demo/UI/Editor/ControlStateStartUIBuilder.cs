#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 --------UI------------- 下 Canvas 中创建 Demo GameState 菜单与操控状态跳转 UI。
/// </summary>
public static class ControlStateStartUIBuilder
{
    private const string UiRootName = "--------UI-------------";
    private const string UiRootObjectName = "DemoGameStateUIRoot";
    private const string MenuPanelName = "DemoGameStateMenuPanel";
    private const string PanelName = "ControlStateJumpPanel";
    private const string HighlightPanelName = "PlateMapHighlightPanel";
    private const string VehicleHeatmapPanelName = "VehicleHeatmapUpdatePanel";
    private const string CarPanelUiPanelName = "CarPanelUIPanel";
    private const string PreviousLevelPanelName = "PreviousLevelPanel";
    private const string BigScreenCarouselPanelName = "BigScreenCarouselPanel";
    private const string HttpApiTestPanelName = "HttpApiTestPanel";
    private const string DemoUiFontPath = "Assets/Font/GeourceAltCHT-Medium.ttf";
    private const string PlateMapHighlightUiTitle = "省级高亮";
    private const string PlateMapHighlightMenuLabel = "省级高亮";
    private const string VehicleHeatmapMenuLabel = "更新车辆热力图";
    private const string VehicleHeatmapUiTitle = "更新车辆热力图";
    private const string CarPanelUiMenuLabel = "车辆 UI 连线";
    private const string CarPanelUiTitle = "车辆 UI 连线";
    private const string PreviousLevelMenuLabel = "返回上个层级";
    private const string PreviousLevelUiTitle = "返回上个层级";
    private const string BigScreenCarouselMenuLabel = "大屏自动轮播";
    private const string BigScreenCarouselUiTitle = "大屏自动轮播";
    private const string HttpApiTestMenuLabel = "接口调用测试";
    private const string HttpApiTestUiTitle = "接口调用测试";
    private const string FpsDisplayToggleRowName = "FpsDisplayToggle";
    private const string FpsDisplayMenuLabel = "显示 FPS";
    private const string FpsOverlayName = "DemoFpsOverlay";
    private const int FpsOverlaySortOrder = 100;
    private const float FpsValueLabelWidth = 96f;
    private const string InstantToggleRowName = "InstantTransitionToggle";
    private const string InstantTogglePath = InstantToggleRowName + "/Toggle";
    private const string InstantToggleCheckmarkPath = InstantTogglePath + "/Checkmark";
    private const float PanelWidth = 360f;
    private const float RowHeight = 36f;
    private const float LabelWidth = 140f;
    private const float FieldWidth = 200f;
    private const float BackButtonHeight = 32f;
    private const float MenuButtonHeight = 40f;
    private const int DropdownItemLabelFontSize = 12;
    private const int InputFieldFontSize = 12;
    private const int HeaderInputFieldFontSize = 10;
    private const float HttpJsonResultAreaHeight = 220f;
    private const float HttpJsonCopyAreaHeight = 120f;
    private const float ResponseLabelMinHeight = 32f;
    private const float ResponseLabelInitialHeight = 120f;
    private const string DropdownItemLabelPath = "Template/Viewport/Content/Item/Item Label";
    private const string ControlStateJumpUiTitle = "界面跳转——跨层级跳转";

    private struct PreservedUiState
    {
        public System.Collections.Generic.Dictionary<string, ControlStateJumpPanelLayout.RectLayoutData> PanelLayouts;
        public ControlStateJumpPanelLayout.RectLayoutData UiRootLayout;
        public Vector2 HttpApiFormScrollOffsetMin;
        public Vector2 HttpApiFormScrollOffsetMax;
        public bool HasUiRootLayout;
        public bool HasHttpApiFormScrollLayout;
        public Sprite InstantToggleCheckmarkSprite;

        public void GetFormScrollOffsets(out Vector2 offsetMin, out Vector2 offsetMax)
        {
            if (HasHttpApiFormScrollLayout
                && HttpApiTestPanelLayout.IsValidOffsets(HttpApiFormScrollOffsetMin, HttpApiFormScrollOffsetMax))
            {
                offsetMin = HttpApiFormScrollOffsetMin;
                offsetMax = HttpApiFormScrollOffsetMax;
                return;
            }

            offsetMin = HttpApiTestPanelLayout.DefaultOffsetMin;
            offsetMax = HttpApiTestPanelLayout.DefaultOffsetMax;
        }

        public ControlStateJumpPanelLayout.RectLayoutData GetPanelLayout(string panelName)
        {
            if (PanelLayouts != null
                && PanelLayouts.TryGetValue(panelName, out ControlStateJumpPanelLayout.RectLayoutData layout))
            {
                return layout;
            }

            if (PanelLayouts != null
                && PanelLayouts.TryGetValue(PanelName, out layout))
            {
                return layout;
            }

            if (PanelLayouts != null
                && PanelLayouts.TryGetValue(MenuPanelName, out layout))
            {
                return layout;
            }

            return ControlStateJumpPanelLayout.CreateDefault();
        }
    }

    private static readonly string[] PreservedPanelNames =
    {
        MenuPanelName,
        PanelName,
        HighlightPanelName,
        VehicleHeatmapPanelName,
        CarPanelUiPanelName,
        PreviousLevelPanelName,
        BigScreenCarouselPanelName,
        HttpApiTestPanelName,
    };

    [MenuItem("Tools/Demo/创建操控状态跳转 UI")]
    public static void CreateControlStateJumpUI()
    {
        if (!TryFindTargetCanvas(out Canvas canvas))
        {
            Debug.LogError($"[ControlStateStartUIBuilder] 未在 {UiRootName} 下找到 Canvas。");
            return;
        }

        EnsureCanvasScaler(canvas);
        EnsureEventSystem();

        PreservedUiState preserved = CapturePreservedState(canvas.transform);
        DestroyExistingUi(canvas.transform);

        DefaultControls.Resources resources = CreateDefaultUiResources();

        GameObject uiRoot = new GameObject(UiRootObjectName, typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        RectTransform uiRootRect = uiRoot.GetComponent<RectTransform>();
        if (preserved.HasUiRootLayout)
        {
            ApplyPanelLayout(uiRootRect, preserved.UiRootLayout);
        }
        else
        {
            SetupFullStretchRect(uiRootRect);
        }

        DemoGameStateUINavigator navigator = uiRoot.AddComponent<DemoGameStateUINavigator>();

        GameObject menuPanel = CreateMenuPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(MenuPanelName),
            navigator,
            out Toggle fpsDisplayToggle);
        GameObject jumpPanel = CreateControlStateJumpPanel(
            uiRoot.transform,
            resources,
            preserved,
            navigator,
            out Button backButton);
        Font demoUiFont = LoadDemoUiFont();
        GameObject highlightPanel = CreatePlateMapHighlightPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(HighlightPanelName),
            navigator,
            demoUiFont);
        GameObject vehicleHeatmapPanel = CreateVehicleHeatmapUpdatePanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(VehicleHeatmapPanelName),
            navigator,
            demoUiFont);
        GameObject carPanelUiPanel = CreateCarPanelUiPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(CarPanelUiPanelName),
            navigator,
            demoUiFont);
        GameObject previousLevelPanel = CreatePreviousLevelPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(PreviousLevelPanelName),
            navigator,
            preserved.InstantToggleCheckmarkSprite ?? resources.checkmark,
            demoUiFont);
        GameObject bigScreenCarouselPanel = CreateBigScreenCarouselPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(BigScreenCarouselPanelName),
            navigator,
            demoUiFont);
        GameObject httpApiTestPanel = CreateHttpApiTestPanel(
            uiRoot.transform,
            resources,
            preserved.GetPanelLayout(HttpApiTestPanelName),
            preserved,
            navigator,
            demoUiFont,
            out Text httpJsonResultText,
            out ScrollRect httpJsonResultScroll,
            out RectTransform httpJsonResultContent,
            out GameObject httpJsonResultBar,
            out InputField httpJsonCopyInput,
            out GameObject httpJsonCopyBar);

        Text fpsValueLabel = CreateFpsOverlay(
            uiRoot.transform,
            preserved.GetPanelLayout(MenuPanelName),
            demoUiFont);

        DemoMenuFpsDisplay fpsDisplay = uiRoot.AddComponent<DemoMenuFpsDisplay>();
        SerializedObject serializedFps = new SerializedObject(fpsDisplay);
        serializedFps.FindProperty("_fpsValueLabel").objectReferenceValue = fpsValueLabel;
        serializedFps.FindProperty("_showFpsToggle").objectReferenceValue = fpsDisplayToggle;
        serializedFps.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedNavigator = new SerializedObject(navigator);
        serializedNavigator.FindProperty("_menuPanel").objectReferenceValue = menuPanel;
        serializedNavigator.FindProperty("_controlStateJumpPanel").objectReferenceValue = jumpPanel;
        serializedNavigator.FindProperty("_plateMapHighlightPanel").objectReferenceValue = highlightPanel;
        serializedNavigator.FindProperty("_vehicleHeatmapUpdatePanel").objectReferenceValue = vehicleHeatmapPanel;
        serializedNavigator.FindProperty("_carPanelUiPanel").objectReferenceValue = carPanelUiPanel;
        serializedNavigator.FindProperty("_previousLevelPanel").objectReferenceValue = previousLevelPanel;
        serializedNavigator.FindProperty("_bigScreenCarouselPanel").objectReferenceValue = bigScreenCarouselPanel;
        serializedNavigator.FindProperty("_httpApiTestPanel").objectReferenceValue = httpApiTestPanel;
        serializedNavigator.ApplyModifiedPropertiesWithoutUndo();

        jumpPanel.SetActive(false);
        highlightPanel.SetActive(false);
        vehicleHeatmapPanel.SetActive(false);
        carPanelUiPanel.SetActive(false);
        previousLevelPanel.SetActive(false);
        bigScreenCarouselPanel.SetActive(false);
        httpApiTestPanel.SetActive(false);
        httpJsonResultBar.SetActive(false);
        httpJsonCopyBar.SetActive(false);

        Undo.RegisterCreatedObjectUndo(uiRoot, "Create Demo GameState UI");
        Selection.activeGameObject = uiRoot;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[ControlStateStartUIBuilder] 已在 {UiRootName}/Canvas 下创建 {UiRootObjectName}。");
    }

    private static GameObject CreateMenuPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        out Toggle fpsDisplayToggle)
    {
        GameObject panel = CreatePanel(parent, MenuPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;

        fpsDisplayToggle = CreateLabeledToggle(
            panel.transform,
            resources,
            FpsDisplayToggleRowName,
            FpsDisplayMenuLabel,
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            false);
        ApplyToggleCheckmarkSprite(fpsDisplayToggle, resources.checkmark);
        y -= RowHeight + 8f;

        CreateLabel(panel.transform, "Demo 功能菜单", 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Normal);
        y -= 28f + 12f;

        GameObject entryButtonGo = DefaultControls.CreateButton(resources);
        entryButtonGo.name = "ControlStateJumpEntryButton";
        SetupChildRect(entryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text entryButtonText = entryButtonGo.GetComponentInChildren<Text>();
        if (entryButtonText != null)
        {
            entryButtonText.text = ControlStateJumpUiTitle;
        }

        y -= MenuButtonHeight + 8f;

        GameObject highlightEntryButtonGo = DefaultControls.CreateButton(resources);
        highlightEntryButtonGo.name = "PlateMapHighlightEntryButton";
        SetupChildRect(highlightEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text highlightEntryButtonText = highlightEntryButtonGo.GetComponentInChildren<Text>();
        if (highlightEntryButtonText != null)
        {
            highlightEntryButtonText.text = PlateMapHighlightMenuLabel;
        }

        y -= MenuButtonHeight + 8f;

        GameObject heatmapEntryButtonGo = DefaultControls.CreateButton(resources);
        heatmapEntryButtonGo.name = "VehicleHeatmapUpdateEntryButton";
        SetupChildRect(heatmapEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text heatmapEntryButtonText = heatmapEntryButtonGo.GetComponentInChildren<Text>();
        if (heatmapEntryButtonText != null)
        {
            heatmapEntryButtonText.text = VehicleHeatmapMenuLabel;
        }

        y -= MenuButtonHeight + 8f;

        GameObject carPanelUiEntryButtonGo = DefaultControls.CreateButton(resources);
        carPanelUiEntryButtonGo.name = "CarPanelUIEntryButton";
        SetupChildRect(carPanelUiEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text carPanelUiEntryButtonText = carPanelUiEntryButtonGo.GetComponentInChildren<Text>();
        if (carPanelUiEntryButtonText != null)
        {
            carPanelUiEntryButtonText.text = CarPanelUiMenuLabel;
        }

        y -= MenuButtonHeight + 8f;

        GameObject previousLevelEntryButtonGo = DefaultControls.CreateButton(resources);
        previousLevelEntryButtonGo.name = "PreviousLevelEntryButton";
        SetupChildRect(previousLevelEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text previousLevelEntryButtonText = previousLevelEntryButtonGo.GetComponentInChildren<Text>();
        if (previousLevelEntryButtonText != null)
        {
            previousLevelEntryButtonText.text = PreviousLevelMenuLabel;
        }

        y -= MenuButtonHeight + 8f;

        GameObject bigScreenCarouselEntryButtonGo = DefaultControls.CreateButton(resources);
        bigScreenCarouselEntryButtonGo.name = "BigScreenCarouselEntryButton";
        SetupChildRect(bigScreenCarouselEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text bigScreenCarouselEntryButtonText = bigScreenCarouselEntryButtonGo.GetComponentInChildren<Text>();
        if (bigScreenCarouselEntryButtonText != null)
        {
            bigScreenCarouselEntryButtonText.text = BigScreenCarouselMenuLabel;
        }

        y -= MenuButtonHeight + 8f;

        GameObject httpApiTestEntryButtonGo = DefaultControls.CreateButton(resources);
        httpApiTestEntryButtonGo.name = "HttpApiTestEntryButton";
        SetupChildRect(httpApiTestEntryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text httpApiTestEntryButtonText = httpApiTestEntryButtonGo.GetComponentInChildren<Text>();
        if (httpApiTestEntryButtonText != null)
        {
            httpApiTestEntryButtonText.text = HttpApiTestMenuLabel;
        }

        DemoGameStateMenuUIDemo menuDemo = panel.AddComponent<DemoGameStateMenuUIDemo>();
        SerializedObject serializedMenu = new SerializedObject(menuDemo);
        serializedMenu.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedMenu.FindProperty("_controlStateJumpEntryButton").objectReferenceValue =
            entryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_plateMapHighlightEntryButton").objectReferenceValue =
            highlightEntryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_vehicleHeatmapUpdateEntryButton").objectReferenceValue =
            heatmapEntryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_carPanelUiEntryButton").objectReferenceValue =
            carPanelUiEntryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_previousLevelEntryButton").objectReferenceValue =
            previousLevelEntryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_bigScreenCarouselEntryButton").objectReferenceValue =
            bigScreenCarouselEntryButtonGo.GetComponent<Button>();
        serializedMenu.FindProperty("_httpApiTestEntryButton").objectReferenceValue =
            httpApiTestEntryButtonGo.GetComponent<Button>();
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, LoadDemoUiFont());
        ApplyPanelTextNormalStyle(panel);

        return panel;
    }

    /// <summary>FPS 文本独立 Overlay，与菜单同区域对齐，排序置于最前。</summary>
    private static Text CreateFpsOverlay(
        Transform uiRoot,
        ControlStateJumpPanelLayout.RectLayoutData menuLayout,
        Font demoUiFont)
    {
        GameObject overlay = new GameObject(FpsOverlayName, typeof(RectTransform));
        overlay.transform.SetParent(uiRoot, false);
        ApplyPanelLayout(overlay.GetComponent<RectTransform>(), menuLayout);

        Canvas overlayCanvas = overlay.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = FpsOverlaySortOrder;

        GameObject fpsLabelGo = CreateLabelObject(
            overlay.transform,
            "FpsValueLabel",
            "FPS: --",
            PanelWidth - 12f - FpsValueLabelWidth,
            -12f,
            FpsValueLabelWidth,
            28f,
            16,
            FontStyle.Normal);
        Text fpsValueLabel = fpsLabelGo.GetComponent<Text>();
        fpsValueLabel.alignment = TextAnchor.MiddleRight;
        fpsValueLabel.raycastTarget = false;
        fpsLabelGo.SetActive(false);

        if (demoUiFont != null)
        {
            fpsValueLabel.font = demoUiFont;
        }

        fpsValueLabel.fontStyle = FontStyle.Normal;

        overlay.transform.SetAsLastSibling();
        return fpsValueLabel;
    }

    private static GameObject CreatePreviousLevelPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Sprite toggleCheckmarkSprite,
        Font demoUiFont)
    {
        GameObject panel = CreatePanel(parent, PreviousLevelPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, PreviousLevelUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        GameObject currentStateLabelGo = CreateLabelObject(
            panel.transform,
            "CurrentStateLabel",
            "当前层级：地球级 (0)",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            14,
            FontStyle.Normal);
        Text currentStateLabel = currentStateLabelGo.GetComponent<Text>();
        y -= RowHeight + 8f;

        Toggle instantToggle = CreateLabeledToggle(
            panel.transform,
            resources,
            "InstantTransitionToggle",
            "瞬时跳转",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            ControlStatePreviousLevelUIDemo.DefaultUseInstantTransition);
        ApplyToggleCheckmarkSprite(instantToggle, toggleCheckmarkSprite);
        y -= RowHeight + 12f;

        GameObject previousButtonGo = DefaultControls.CreateButton(resources);
        previousButtonGo.name = "PreviousLevelButton";
        SetupChildRect(previousButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text previousButtonText = previousButtonGo.GetComponentInChildren<Text>();
        if (previousButtonText != null)
        {
            previousButtonText.text = "返回上一级";
        }

        ControlStatePreviousLevelUIDemo uiDemo = panel.AddComponent<ControlStatePreviousLevelUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_currentStateLabel").objectReferenceValue = currentStateLabel;
        serializedDemo.FindProperty("_instantTransitionToggle").objectReferenceValue = instantToggle;
        serializedDemo.FindProperty("_previousLevelButton").objectReferenceValue =
            previousButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, demoUiFont);

        return panel;
    }

    private static GameObject CreateBigScreenCarouselPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Font demoUiFont)
    {
        GameObject panel = CreatePanel(parent, BigScreenCarouselPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, BigScreenCarouselUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        GameObject statusLabelGo = CreateLabelObject(
            panel.transform,
            "CarouselStatusLabel",
            "状态：已关闭",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            14,
            FontStyle.Normal);
        Text statusLabel = statusLabelGo.GetComponent<Text>();
        y -= RowHeight + 8f;

        GameObject countdownLabelGo = CreateLabelObject(
            panel.transform,
            "CarouselCountdownLabel",
            "下次切换：--",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            14,
            FontStyle.Normal);
        Text countdownLabel = countdownLabelGo.GetComponent<Text>();
        y -= RowHeight + 12f;

        GameObject enableButtonGo = DefaultControls.CreateButton(resources);
        enableButtonGo.name = "EnableCarouselButton";
        SetupChildRect(enableButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text enableButtonText = enableButtonGo.GetComponentInChildren<Text>();
        if (enableButtonText != null)
        {
            enableButtonText.text = "开启自动轮播";
        }

        y -= 48f;

        GameObject disableButtonGo = DefaultControls.CreateButton(resources);
        disableButtonGo.name = "DisableCarouselButton";
        SetupChildRect(disableButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text disableButtonText = disableButtonGo.GetComponentInChildren<Text>();
        if (disableButtonText != null)
        {
            disableButtonText.text = "关闭自动轮播";
        }

        DemoBigScreenCarouselUIDemo uiDemo = panel.AddComponent<DemoBigScreenCarouselUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_statusLabel").objectReferenceValue = statusLabel;
        serializedDemo.FindProperty("_countdownLabel").objectReferenceValue = countdownLabel;
        serializedDemo.FindProperty("_enableButton").objectReferenceValue = enableButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_disableButton").objectReferenceValue = disableButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, demoUiFont);

        return panel;
    }

    private static GameObject CreateHttpApiTestPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        PreservedUiState preserved,
        DemoGameStateUINavigator navigator,
        Font demoUiFont,
        out Text jsonResultText,
        out ScrollRect jsonResultScroll,
        out RectTransform jsonResultContent,
        out GameObject jsonResultBar,
        out InputField jsonCopyInput,
        out GameObject jsonCopyBar)
    {
        jsonCopyBar = CreateHttpJsonCopyInputBar(
            parent,
            resources,
            demoUiFont,
            out jsonCopyInput);
        jsonResultBar = CreateHttpJsonResultBar(
            parent,
            resources,
            demoUiFont,
            HttpJsonCopyAreaHeight,
            out jsonResultText,
            out jsonResultScroll,
            out jsonResultContent);

        GameObject panel = CreatePanel(parent, HttpApiTestPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;
        CreateLabel(panel.transform, HttpApiTestUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);

        preserved.GetFormScrollOffsets(out Vector2 contentOffsetMin, out Vector2 contentOffsetMax);

        GameObject apiEntryMenuGo = new GameObject("ApiEntryMenuPanel", typeof(RectTransform));
        apiEntryMenuGo.transform.SetParent(panel.transform, false);
        RectTransform apiEntryMenuRect = apiEntryMenuGo.GetComponent<RectTransform>();
        HttpApiTestPanelLayout.ApplyFormScrollViewLayout(apiEntryMenuRect, contentOffsetMin, contentOffsetMax);

        float menuY = -8f;
        GameObject customApiEntryGo = DefaultControls.CreateButton(resources);
        customApiEntryGo.name = "CustomApiEntryButton";
        SetupChildRect(customApiEntryGo, apiEntryMenuGo.transform, 0f, menuY, PanelWidth - 24f, MenuButtonHeight);
        Text customApiEntryText = customApiEntryGo.GetComponentInChildren<Text>();
        if (customApiEntryText != null)
        {
            customApiEntryText.text = "自定义接口调用";
            customApiEntryText.fontSize = 14;
        }

        menuY -= MenuButtonHeight + 8f;
        GameObject vinLocationEntryGo = DefaultControls.CreateButton(resources);
        vinLocationEntryGo.name = "VinLocationEntryButton";
        SetupChildRect(vinLocationEntryGo, apiEntryMenuGo.transform, 0f, menuY, PanelWidth - 24f, MenuButtonHeight);
        Text vinLocationEntryText = vinLocationEntryGo.GetComponentInChildren<Text>();
        if (vinLocationEntryText != null)
        {
            vinLocationEntryText.text = "车辆位置接口调用";
            vinLocationEntryText.fontSize = 14;
        }

        ScrollRect scrollRect = CreateScrollView(
            panel.transform,
            0f,
            0f,
            100f,
            100f,
            out RectTransform scrollContent);
        scrollRect.gameObject.name = "FormScrollView";
        scrollRect.gameObject.SetActive(false);
        RectTransform formScrollRect = scrollRect.GetComponent<RectTransform>();
        HttpApiTestPanelLayout.ApplyFormScrollViewLayout(formScrollRect, contentOffsetMin, contentOffsetMax);
        Transform customRoot = scrollContent;

        float contentY = -8f;
        GameObject customListBackGo = CreateApiListBackButton(
            resources,
            customRoot,
            "CustomApiListBackButton",
            ref contentY);

        CreateLabel(customRoot, "GET 请求", 0f, contentY, PanelWidth - 48f, 22f, 14, FontStyle.Bold);
        contentY -= 26f;

        InputField getUrlInput = CreateLabeledInputField(
            customRoot,
            resources,
            "GetUrlInput",
            "GET 地址",
            0f,
            contentY,
            LabelWidth,
            FieldWidth,
            HttpApiTestUIDemo.DefaultGetUrl,
            InputFieldFontSize);
        contentY -= RowHeight + 4f;

        GameObject httpsPresetButtonGo = DefaultControls.CreateButton(resources);
        httpsPresetButtonGo.name = "ApplyHttpsTestPresetButton";
        SetupChildRect(httpsPresetButtonGo, customRoot, 0f, contentY, PanelWidth - 48f, 32f);
        Text httpsPresetButtonText = httpsPresetButtonGo.GetComponentInChildren<Text>();
        if (httpsPresetButtonText != null)
        {
            httpsPresetButtonText.text = "预设：HTTPS 测试 getBasicEventPage";
            httpsPresetButtonText.fontSize = 12;
        }
        contentY -= 36f;

        GameObject httpPresetButtonGo = DefaultControls.CreateButton(resources);
        httpPresetButtonGo.name = "ApplyInternalHttpPresetButton";
        SetupChildRect(httpPresetButtonGo, customRoot, 0f, contentY, PanelWidth - 48f, 32f);
        Text httpPresetButtonText = httpPresetButtonGo.GetComponentInChildren<Text>();
        if (httpPresetButtonText != null)
        {
            httpPresetButtonText.text = "预设：内网 HTTP 业务接口";
            httpPresetButtonText.fontSize = 12;
        }
        contentY -= 36f;

        GameObject getButtonGo = DefaultControls.CreateButton(resources);
        getButtonGo.name = "GetButton";
        SetupChildRect(getButtonGo, customRoot, 0f, contentY, PanelWidth - 48f, 36f);
        Text getButtonText = getButtonGo.GetComponentInChildren<Text>();
        if (getButtonText != null)
        {
            getButtonText.text = "发送 GET";
        }
        contentY -= 44f;

        CreateLabel(customRoot, "POST 请求", 0f, contentY, PanelWidth - 48f, 22f, 14, FontStyle.Bold);
        contentY -= 26f;

        InputField postHostInput = CreateLabeledInputField(
            customRoot,
            resources,
            "PostHostInput",
            "主机 IP:端口",
            0f,
            contentY,
            LabelWidth,
            FieldWidth,
            HttpApiTestUIDemo.DefaultPostHost,
            InputFieldFontSize);
        contentY -= RowHeight + 4f;

        InputField postPathInput = CreateLabeledInputField(
            customRoot,
            resources,
            "PostPathInput",
            "POST 路径",
            0f,
            contentY,
            LabelWidth,
            FieldWidth,
            HttpApiTestUIDemo.DefaultPostPath,
            InputFieldFontSize);
        contentY -= RowHeight + 4f;

        CreateLabel(customRoot, "请求头部", 0f, contentY, PanelWidth - 48f, 22f, 14, FontStyle.Bold);
        contentY -= 24f;

        CreateLabel(customRoot, string.Empty, 0f, contentY, 24f, 20f, 12, FontStyle.Normal);
        CreateLabel(customRoot, "请求头", 28f, contentY, 84f, 20f, 12, FontStyle.Normal);
        CreateLabel(customRoot, "内容", 116f, contentY, 150f, 20f, 12, FontStyle.Normal);
        CreateLabel(customRoot, "操作", PanelWidth - 88f, contentY, 40f, 20f, 12, FontStyle.Normal);
        contentY -= 24f;

        GameObject headerRowsContainerGo = new GameObject("HeaderRowsContainer", typeof(RectTransform));
        headerRowsContainerGo.transform.SetParent(customRoot, false);
        SetupChildRect(headerRowsContainerGo, customRoot, 0f, contentY, PanelWidth - 48f, 108f);
        VerticalLayoutGroup headerLayout = headerRowsContainerGo.AddComponent<VerticalLayoutGroup>();
        headerLayout.spacing = 4f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandHeight = false;
        contentY -= 112f;

        GameObject headerRowTemplate = CreateHttpHeaderRowTemplate(
            headerRowsContainerGo.transform,
            resources,
            postHostInput);
        headerRowTemplate.SetActive(false);
        contentY -= 4f;

        GameObject addHeaderButtonGo = DefaultControls.CreateButton(resources);
        addHeaderButtonGo.name = "AddHeaderButton";
        SetupChildRect(addHeaderButtonGo, customRoot, 0f, contentY, 48f, 32f);
        Text addHeaderButtonText = addHeaderButtonGo.GetComponentInChildren<Text>();
        if (addHeaderButtonText != null)
        {
            addHeaderButtonText.text = "+";
            addHeaderButtonText.fontSize = 20;
        }
        contentY -= 36f;

        CreateLabel(customRoot, "请求参数", 0f, contentY, PanelWidth - 48f, 22f, 14, FontStyle.Bold);
        contentY -= 24f;

        InputField postBodyInput = CreateLabeledMultilineInputField(
            customRoot,
            resources,
            "PostBodyInput",
            "JSON",
            0f,
            contentY,
            LabelWidth,
            FieldWidth,
            HttpApiTestUIDemo.DefaultPostBody,
            InputFieldFontSize,
            88f);
        if (postBodyInput.placeholder is Text postBodyPlaceholder)
        {
            postBodyPlaceholder.text = "留空则使用默认请求参数";
        }
        contentY -= 92f;

        GameObject postButtonGo = DefaultControls.CreateButton(resources);
        postButtonGo.name = "PostButton";
        SetupChildRect(postButtonGo, customRoot, 0f, contentY, PanelWidth - 48f, 36f);
        Text postButtonText = postButtonGo.GetComponentInChildren<Text>();
        if (postButtonText != null)
        {
            postButtonText.text = "发送 POST";
        }
        contentY -= 44f;

        GameObject stopButtonGo = DefaultControls.CreateButton(resources);
        stopButtonGo.name = "StopButton";
        SetupChildRect(stopButtonGo, customRoot, 0f, contentY, PanelWidth - 48f, 36f);
        Text stopButtonText = stopButtonGo.GetComponentInChildren<Text>();
        if (stopButtonText != null)
        {
            stopButtonText.text = "停止请求";
        }
        contentY -= 44f;

        Text fallbackResponseLabel = CreateExpandableResponseLabel(
            customRoot,
            0f,
            contentY,
            PanelWidth - 48f,
            ResponseLabelInitialHeight,
            "等待请求...",
            11);
        contentY -= ResponseLabelInitialHeight + 4f;
        float formScrollBaseHeight = Mathf.Abs(contentY) + 16f;
        scrollContent.sizeDelta = new Vector2(0f, formScrollBaseHeight);

        ScrollRect vinScrollRect = CreateScrollView(
            panel.transform,
            0f,
            0f,
            100f,
            100f,
            out RectTransform vinScrollContent);
        vinScrollRect.gameObject.name = "VinLocationScrollView";
        vinScrollRect.gameObject.SetActive(false);
        RectTransform vinScrollRectTransform = vinScrollRect.GetComponent<RectTransform>();
        HttpApiTestPanelLayout.ApplyFormScrollViewLayout(vinScrollRectTransform, contentOffsetMin, contentOffsetMax);
        Transform vinRoot = vinScrollContent;

        float vinContentY = -8f;
        GameObject vinListBackGo = CreateApiListBackButton(
            resources,
            vinRoot,
            "VinLocationListBackButton",
            ref vinContentY);

        CreateLabel(
            vinRoot,
            HttpProjectConfig.LatestVinLocationPath,
            0f,
            vinContentY,
            PanelWidth - 48f,
            22f,
            12,
            FontStyle.Bold);
        vinContentY -= 24f;

        InputField vinStartTimeInput = CreateLabeledInputField(
            vinRoot,
            resources,
            "VinStartTimeInput",
            "开始时间",
            0f,
            vinContentY,
            LabelWidth,
            FieldWidth,
            HttpProjectConfig.DefaultQueryStartTime,
            InputFieldFontSize);
        if (vinStartTimeInput.placeholder is Text vinStartPlaceholder)
        {
            vinStartPlaceholder.text = "可空";
        }
        vinContentY -= RowHeight + 4f;

        InputField vinEndTimeInput = CreateLabeledInputField(
            vinRoot,
            resources,
            "VinEndTimeInput",
            "结束时间",
            0f,
            vinContentY,
            LabelWidth,
            FieldWidth,
            BackendDateTimeTool.GetCurrentTimeString(),
            InputFieldFontSize);
        if (vinEndTimeInput.placeholder is Text vinEndPlaceholder)
        {
            vinEndPlaceholder.text = "可空，默认当前时间";
        }
        vinContentY -= RowHeight + 4f;

        InputField vinProvinceInput = CreateLabeledInputField(
            vinRoot,
            resources,
            "VinProvinceInput",
            "省份",
            0f,
            vinContentY,
            LabelWidth,
            FieldWidth,
            string.Empty,
            InputFieldFontSize);
        if (vinProvinceInput.placeholder is Text vinProvincePlaceholder)
        {
            vinProvincePlaceholder.text = "adcode，可空";
        }
        vinContentY -= RowHeight + 4f;

        InputField vinRegionInput = CreateLabeledInputField(
            vinRoot,
            resources,
            "VinRegionInput",
            "区域",
            0f,
            vinContentY,
            LabelWidth,
            FieldWidth,
            string.Empty,
            InputFieldFontSize);
        if (vinRegionInput.placeholder is Text vinRegionPlaceholder)
        {
            vinRegionPlaceholder.text = "可空";
        }
        vinContentY -= RowHeight + 4f;

        InputField vinCountryInput = CreateLabeledInputField(
            vinRoot,
            resources,
            "VinCountryInput",
            "国家",
            0f,
            vinContentY,
            LabelWidth,
            FieldWidth,
            string.Empty,
            InputFieldFontSize);
        if (vinCountryInput.placeholder is Text vinCountryPlaceholder)
        {
            vinCountryPlaceholder.text = "可空";
        }
        vinContentY -= RowHeight + 8f;

        GameObject vinRequestButtonGo = DefaultControls.CreateButton(resources);
        vinRequestButtonGo.name = "VinLocationRequestButton";
        SetupChildRect(vinRequestButtonGo, vinRoot, 0f, vinContentY, PanelWidth - 48f, 36f);
        Text vinRequestButtonText = vinRequestButtonGo.GetComponentInChildren<Text>();
        if (vinRequestButtonText != null)
        {
            vinRequestButtonText.text = "请求车辆位置";
        }
        vinContentY -= 44f;

        GameObject vinStopButtonGo = DefaultControls.CreateButton(resources);
        vinStopButtonGo.name = "VinLocationStopButton";
        SetupChildRect(vinStopButtonGo, vinRoot, 0f, vinContentY, PanelWidth - 48f, 36f);
        Text vinStopButtonText = vinStopButtonGo.GetComponentInChildren<Text>();
        if (vinStopButtonText != null)
        {
            vinStopButtonText.text = "停止请求";
        }
        vinContentY -= 44f;

        float vinScrollBaseHeight = Mathf.Abs(vinContentY) + 16f;
        vinScrollContent.sizeDelta = new Vector2(0f, vinScrollBaseHeight);

        HttpApiTestUIDemo uiDemo = panel.AddComponent<HttpApiTestUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_getUrlInput").objectReferenceValue = getUrlInput;
        serializedDemo.FindProperty("_getButton").objectReferenceValue = getButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_applyHttpsTestPresetButton").objectReferenceValue =
            httpsPresetButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_applyInternalHttpPresetButton").objectReferenceValue =
            httpPresetButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_postHostInput").objectReferenceValue = postHostInput;
        serializedDemo.FindProperty("_postPathInput").objectReferenceValue = postPathInput;
        serializedDemo.FindProperty("_postBodyInput").objectReferenceValue = postBodyInput;
        serializedDemo.FindProperty("_headerRowsContainer").objectReferenceValue =
            headerRowsContainerGo.GetComponent<RectTransform>();
        serializedDemo.FindProperty("_addHeaderButton").objectReferenceValue =
            addHeaderButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_headerRowTemplate").objectReferenceValue = headerRowTemplate;
        serializedDemo.FindProperty("_postButton").objectReferenceValue = postButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_stopButton").objectReferenceValue = stopButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_jsonResultBarRoot").objectReferenceValue = jsonResultBar;
        serializedDemo.FindProperty("_jsonResultText").objectReferenceValue = jsonResultText;
        serializedDemo.FindProperty("_jsonResultScroll").objectReferenceValue = jsonResultScroll;
        serializedDemo.FindProperty("_jsonResultContent").objectReferenceValue = jsonResultContent;
        serializedDemo.FindProperty("_jsonCopyBarRoot").objectReferenceValue = jsonCopyBar;
        serializedDemo.FindProperty("_jsonCopyInputField").objectReferenceValue = jsonCopyInput;
        serializedDemo.FindProperty("_fallbackResponseLabel").objectReferenceValue = fallbackResponseLabel;
        serializedDemo.FindProperty("_formScrollContent").objectReferenceValue = scrollContent;
        serializedDemo.FindProperty("_formScrollBaseHeight").floatValue = formScrollBaseHeight;
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.FindProperty("_apiEntryMenuPanel").objectReferenceValue = apiEntryMenuGo;
        serializedDemo.FindProperty("_customApiEntryButton").objectReferenceValue = customApiEntryGo.GetComponent<Button>();
        serializedDemo.FindProperty("_vinLocationEntryButton").objectReferenceValue = vinLocationEntryGo.GetComponent<Button>();
        serializedDemo.FindProperty("_formScrollViewRoot").objectReferenceValue = scrollRect.gameObject;
        serializedDemo.FindProperty("_vinLocationScrollViewRoot").objectReferenceValue = vinScrollRect.gameObject;
        serializedDemo.FindProperty("_customApiListBackButton").objectReferenceValue = customListBackGo.GetComponent<Button>();
        serializedDemo.FindProperty("_vinLocationListBackButton").objectReferenceValue = vinListBackGo.GetComponent<Button>();
        serializedDemo.FindProperty("_vinStartTimeInput").objectReferenceValue = vinStartTimeInput;
        serializedDemo.FindProperty("_vinEndTimeInput").objectReferenceValue = vinEndTimeInput;
        serializedDemo.FindProperty("_vinProvinceInput").objectReferenceValue = vinProvinceInput;
        serializedDemo.FindProperty("_vinRegionInput").objectReferenceValue = vinRegionInput;
        serializedDemo.FindProperty("_vinCountryInput").objectReferenceValue = vinCountryInput;
        serializedDemo.FindProperty("_vinLocationRequestButton").objectReferenceValue =
            vinRequestButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_vinLocationStopButton").objectReferenceValue =
            vinStopButtonGo.GetComponent<Button>();
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        HttpApiTestPanelLayout layoutComponent = panel.GetComponent<HttpApiTestPanelLayout>();
        if (layoutComponent == null)
        {
            layoutComponent = panel.AddComponent<HttpApiTestPanelLayout>();
        }

        layoutComponent.CaptureFormScrollView(formScrollRect);

        ApplyPanelFont(panel, demoUiFont);
        ApplyPanelFont(jsonResultBar, demoUiFont);
        ApplyPanelFont(jsonCopyBar, demoUiFont);

        return panel;
    }

    private static GameObject CreateApiListBackButton(
        DefaultControls.Resources resources,
        Transform parent,
        string buttonName,
        ref float contentY)
    {
        GameObject backGo = DefaultControls.CreateButton(resources);
        backGo.name = buttonName;
        SetupChildRect(backGo, parent, 0f, contentY, PanelWidth - 48f, BackButtonHeight);
        Text backText = backGo.GetComponentInChildren<Text>();
        if (backText != null)
        {
            backText.text = "返回接口列表";
            backText.fontSize = 13;
        }

        contentY -= BackButtonHeight + 8f;
        return backGo;
    }

    private static GameObject CreateHttpJsonCopyInputBar(
        Transform uiRoot,
        DefaultControls.Resources resources,
        Font demoUiFont,
        out InputField copyInputField)
    {
        GameObject barRoot = new GameObject("HttpJsonCopyBar", typeof(RectTransform), typeof(Image));
        barRoot.transform.SetParent(uiRoot, false);
        barRoot.transform.SetAsLastSibling();

        RectTransform barRect = barRoot.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.offsetMin = new Vector2(0f, 0f);
        barRect.offsetMax = new Vector2(0f, HttpJsonCopyAreaHeight);

        Image barImage = barRoot.GetComponent<Image>();
        barImage.color = new Color(0f, 0f, 0f, 0.8f);

        CreateLabel(barRoot.transform, "可复制结果（选中后 Ctrl+C）", 16f, -6f, 280f, 22f, 13, FontStyle.Bold);

        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = "JsonCopyInputField";
        inputGo.transform.SetParent(barRoot.transform, false);
        RectTransform inputRect = inputGo.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.offsetMin = new Vector2(12f, 10f);
        inputRect.offsetMax = new Vector2(-12f, -28f);

        copyInputField = inputGo.GetComponent<InputField>();
        copyInputField.text = "等待请求...";
        copyInputField.lineType = InputField.LineType.MultiLineNewline;
        copyInputField.readOnly = true;
        ConfigureInputFieldTextSize(copyInputField, 11);

        if (copyInputField.textComponent != null)
        {
            copyInputField.textComponent.color = new Color(0.9f, 0.95f, 0.9f);
            copyInputField.textComponent.alignment = TextAnchor.UpperLeft;
            if (demoUiFont != null)
            {
                copyInputField.textComponent.font = demoUiFont;
            }
        }

        if (copyInputField.placeholder is Text placeholder)
        {
            placeholder.text = string.Empty;
        }

        return barRoot;
    }

    private static GameObject CreateHttpJsonResultBar(
        Transform uiRoot,
        DefaultControls.Resources resources,
        Font demoUiFont,
        float bottomOffset,
        out Text jsonResultText,
        out ScrollRect jsonResultScroll,
        out RectTransform jsonResultContent)
    {
        GameObject barRoot = new GameObject("HttpJsonResultBar", typeof(RectTransform), typeof(Image));
        barRoot.transform.SetParent(uiRoot, false);
        barRoot.transform.SetAsLastSibling();

        RectTransform barRect = barRoot.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.offsetMin = new Vector2(0f, bottomOffset);
        barRect.offsetMax = new Vector2(0f, bottomOffset + HttpJsonResultAreaHeight);

        Image barImage = barRoot.GetComponent<Image>();
        barImage.color = new Color(0f, 0f, 0f, 0.72f);

        CreateLabel(barRoot.transform, "接口调试结果", 16f, -8f, 240f, 24f, 14, FontStyle.Bold);

        GameObject scrollGo = new GameObject(
            "JsonResultScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollGo.transform.SetParent(barRoot.transform, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 12f);
        scrollRectTransform.offsetMax = new Vector2(-12f, -32f);

        Image scrollImage = scrollGo.GetComponent<Image>();
        scrollImage.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        SetupFullStretchRect(viewportRect);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        Mask viewportMask = viewportGo.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        jsonResultContent = contentGo.GetComponent<RectTransform>();
        jsonResultContent.anchorMin = new Vector2(0f, 1f);
        jsonResultContent.anchorMax = new Vector2(1f, 1f);
        jsonResultContent.pivot = new Vector2(0.5f, 1f);
        jsonResultContent.anchoredPosition = Vector2.zero;
        jsonResultContent.sizeDelta = new Vector2(0f, HttpJsonResultAreaHeight);

        ContentSizeFitter contentFitter = contentGo.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new GameObject("JsonResultText", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        textGo.transform.SetParent(jsonResultContent, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        jsonResultText = textGo.GetComponent<Text>();
        jsonResultText.text = "等待请求...";
        jsonResultText.font = demoUiFont != null
            ? demoUiFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        jsonResultText.fontSize = 11;
        jsonResultText.fontStyle = FontStyle.Normal;
        jsonResultText.color = new Color(0.85f, 0.92f, 0.85f);
        jsonResultText.alignment = TextAnchor.UpperLeft;
        jsonResultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        jsonResultText.verticalOverflow = VerticalWrapMode.Overflow;
        jsonResultText.raycastTarget = false;

        ContentSizeFitter fitter = textGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        jsonResultScroll = scrollGo.GetComponent<ScrollRect>();
        jsonResultScroll.viewport = viewportRect;
        jsonResultScroll.content = jsonResultContent;
        jsonResultScroll.horizontal = false;
        jsonResultScroll.vertical = true;
        jsonResultScroll.movementType = ScrollRect.MovementType.Clamped;

        return barRoot;
    }

    private static GameObject CreateHttpHeaderRowTemplate(
        Transform parent,
        DefaultControls.Resources resources,
        InputField inputTemplate)
    {
        GameObject rowGo = new GameObject("HeaderRowTemplate", typeof(RectTransform), typeof(LayoutElement));
        rowGo.transform.SetParent(parent, false);
        LayoutElement layoutElement = rowGo.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 32f;
        layoutElement.minHeight = 32f;

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 32f);

        GameObject toggleGo = DefaultControls.CreateToggle(resources);
        toggleGo.name = "EnableToggle";
        toggleGo.transform.SetParent(rowGo.transform, false);
        RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 0.5f);
        toggleRect.anchorMax = new Vector2(0f, 0.5f);
        toggleRect.pivot = new Vector2(0f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(0f, 0f);
        toggleRect.sizeDelta = new Vector2(24f, 24f);
        Toggle toggle = toggleGo.GetComponent<Toggle>();
        toggle.isOn = true;

        InputField keyInput = CloneInputFieldForHeaderRow(inputTemplate, rowGo.transform, "KeyInput", 28f, 84f, "请求头");
        InputField valueInput = CloneInputFieldForHeaderRow(inputTemplate, rowGo.transform, "ValueInput", 116f, 150f, "内容");

        GameObject deleteButtonGo = DefaultControls.CreateButton(resources);
        deleteButtonGo.name = "DeleteButton";
        deleteButtonGo.transform.SetParent(rowGo.transform, false);
        RectTransform deleteRect = deleteButtonGo.GetComponent<RectTransform>();
        deleteRect.anchorMin = new Vector2(1f, 0.5f);
        deleteRect.anchorMax = new Vector2(1f, 0.5f);
        deleteRect.pivot = new Vector2(1f, 0.5f);
        deleteRect.anchoredPosition = Vector2.zero;
        deleteRect.sizeDelta = new Vector2(40f, 28f);
        Text deleteText = deleteButtonGo.GetComponentInChildren<Text>();
        if (deleteText != null)
        {
            deleteText.text = "删除";
            deleteText.fontSize = 12;
            deleteText.color = new Color(0.4f, 0.75f, 1f);
        }

        return rowGo;
    }

    private static Text CreateExpandableResponseLabel(
        Transform parent,
        float x,
        float y,
        float width,
        float minHeight,
        string text,
        int fontSize)
    {
        GameObject labelGo = new GameObject(
            "ResponseLabel",
            typeof(RectTransform),
            typeof(Text),
            typeof(ContentSizeFitter));
        labelGo.transform.SetParent(parent, false);

        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, minHeight);

        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Normal;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        ContentSizeFitter fitter = labelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return label;
    }

    private static InputField CloneInputFieldForHeaderRow(
        InputField template,
        Transform parent,
        string name,
        float x,
        float width,
        string placeholder)
    {
        GameObject inputGo = Object.Instantiate(template.gameObject, parent);
        inputGo.name = name;
        inputGo.SetActive(true);

        InputField inputField = inputGo.GetComponent<InputField>();
        inputField.text = string.Empty;

        RectTransform rect = inputGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 28f);

        if (inputField.placeholder is Text placeholderText)
        {
            placeholderText.text = placeholder;
        }

        ConfigureInputFieldTextSize(inputField, HeaderInputFieldFontSize);
        return inputField;
    }

    private static ScrollRect CreateScrollView(
        Transform parent,
        float x,
        float y,
        float width,
        float height,
        out RectTransform content)
    {
        GameObject scrollGo = new GameObject(
            "ScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 1f);
        scrollRectTransform.anchorMax = new Vector2(0f, 1f);
        scrollRectTransform.pivot = new Vector2(0f, 1f);
        scrollRectTransform.anchoredPosition = new Vector2(x, y);
        scrollRectTransform.sizeDelta = new Vector2(width, height);

        Image scrollImage = scrollGo.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.15f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        SetupFullStretchRect(viewportRect);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        Mask viewportMask = viewportGo.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, height);

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        return scrollRect;
    }

    private static InputField CreateLabeledMultilineInputField(
        Transform parent,
        DefaultControls.Resources resources,
        string name,
        string labelText,
        float x,
        float y,
        float labelWidth,
        float fieldWidth,
        string defaultText,
        int inputFontSize,
        float rowHeight)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetupChildRect(row, parent, x, y, labelWidth + fieldWidth + 8f, rowHeight);

        CreateLabel(row.transform, labelText, 0f, 0f, labelWidth, rowHeight, 14, FontStyle.Normal);

        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = "InputField";
        SetupChildRect(inputGo, row.transform, labelWidth + 8f, 0f, fieldWidth, rowHeight);
        InputField inputField = inputGo.GetComponent<InputField>();
        inputField.text = defaultText;
        inputField.lineType = InputField.LineType.MultiLineNewline;
        ConfigureInputFieldTextSize(inputField, inputFontSize);
        return inputField;
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
        SetupChildRect(labelGo, parent, x, y, width, height);
        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        return labelGo;
    }

    private static GameObject CreateCarPanelUiPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Font demoUiFont)
    {
        GameObject panel = CreatePanel(parent, CarPanelUiPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, CarPanelUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        InputField start3DNameInput = CreateLabeledInputField(
            panel.transform,
            resources,
            "Start3DObjectNameInput",
            "起点物体名",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            CarPanelUIDemo.DefaultStart3DObjectName,
            InputFieldFontSize);
        y -= RowHeight + 12f;

        GameObject openButtonGo = DefaultControls.CreateButton(resources);
        openButtonGo.name = "OpenCarUIButton";
        SetupChildRect(openButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text openButtonText = openButtonGo.GetComponentInChildren<Text>();
        if (openButtonText != null)
        {
            openButtonText.text = "打开车辆 UI";
        }
        y -= 40f + 8f;

        GameObject closeButtonGo = DefaultControls.CreateButton(resources);
        closeButtonGo.name = "CloseCarUIButton";
        SetupChildRect(closeButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text closeButtonText = closeButtonGo.GetComponentInChildren<Text>();
        if (closeButtonText != null)
        {
            closeButtonText.text = "关闭车辆 UI";
        }

        CarPanelUIDemo uiDemo = panel.AddComponent<CarPanelUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_start3DObjectNameInput").objectReferenceValue = start3DNameInput;
        serializedDemo.FindProperty("_openCarUiButton").objectReferenceValue = openButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_closeCarUiButton").objectReferenceValue = closeButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, demoUiFont);

        return panel;
    }

    private static GameObject CreateVehicleHeatmapUpdatePanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Font demoUiFont)
    {
        GameObject panel = CreatePanel(parent, VehicleHeatmapPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        PlateMapShandongRandomPointsDemo pointsDemo =
            Object.FindFirstObjectByType<PlateMapShandongRandomPointsDemo>();

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, VehicleHeatmapUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        InputField pointCountInput = CreateLabeledInputField(
            panel.transform,
            resources,
            "PointCountInput",
            "点位数量",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            VehicleHeatmapUpdateUIDemo.DefaultPointCountText,
            InputFieldFontSize);
        y -= RowHeight + 12f;

        GameObject updateButtonGo = DefaultControls.CreateButton(resources);
        updateButtonGo.name = "UpdateButton";
        SetupChildRect(updateButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text updateButtonText = updateButtonGo.GetComponentInChildren<Text>();
        if (updateButtonText != null)
        {
            updateButtonText.text = "更新";
        }

        VehicleHeatmapUpdateUIDemo uiDemo = panel.AddComponent<VehicleHeatmapUpdateUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_pointCountInput").objectReferenceValue = pointCountInput;
        serializedDemo.FindProperty("_updateButton").objectReferenceValue =
            updateButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_pointsDemo").objectReferenceValue = pointsDemo;
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, demoUiFont);

        return panel;
    }

    private static GameObject CreatePlateMapHighlightPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator,
        Font demoUiFont)
    {
        GameObject panel = CreatePanel(parent, HighlightPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        List<string> highlightNames = ControlStateStartUIOptionProvider.CollectPlateHighlightNames();

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, PlateMapHighlightUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        int defaultIndex = FindOptionIndex(highlightNames, PlateMapHighlightUIDemo.DefaultHighlightName);
        Dropdown provinceDropdown = CreateLabeledDropdown(
            panel.transform,
            resources,
            "ProvinceNameDropdown",
            "省市名字",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            highlightNames,
            defaultIndex);
        y -= RowHeight + 12f;

        GameObject highlightButtonGo = DefaultControls.CreateButton(resources);
        highlightButtonGo.name = "HighlightButton";
        SetupChildRect(highlightButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text highlightButtonText = highlightButtonGo.GetComponentInChildren<Text>();
        if (highlightButtonText != null)
        {
            highlightButtonText.text = "高亮";
        }
        y -= 40f + 8f;

        GameObject clearButtonGo = DefaultControls.CreateButton(resources);
        clearButtonGo.name = "ClearHighlightButton";
        SetupChildRect(clearButtonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text clearButtonText = clearButtonGo.GetComponentInChildren<Text>();
        if (clearButtonText != null)
        {
            clearButtonText.text = "取消高亮";
        }

        PlateMapHighlightUIDemo uiDemo = panel.AddComponent<PlateMapHighlightUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_provinceNameDropdown").objectReferenceValue = provinceDropdown;
        serializedDemo.FindProperty("_highlightButton").objectReferenceValue =
            highlightButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_clearHighlightButton").objectReferenceValue =
            clearButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, demoUiFont);

        return panel;
    }

    private static GameObject CreateControlStateJumpPanel(
        Transform parent,
        DefaultControls.Resources resources,
        PreservedUiState preserved,
        DemoGameStateUINavigator navigator,
        out Button backButton)
    {
        GameObject panel = CreatePanel(parent, PanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, preserved.GetPanelLayout(PanelName));

        ControlStateJumpPanelLayout layoutComponent = panel.AddComponent<ControlStateJumpPanelLayout>();
        layoutComponent.CaptureFrom(panelRect, preserved.InstantToggleCheckmarkSprite);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        List<string> provinceNames = ControlStateStartUIOptionProvider.CollectProvinceNames();
        List<string> moduleNames = ControlStateStartUIOptionProvider.CollectProvinceModuleNames();
        List<string> partNames = ControlStateStartUIOptionProvider.CollectPartNames();

        float y = -12f;
        GameObject backButtonGo = DefaultControls.CreateButton(resources);
        backButtonGo.name = "BackButton";
        SetupChildRect(backButtonGo, panel.transform, 12f, y, 80f, BackButtonHeight);
        Text backButtonText = backButtonGo.GetComponentInChildren<Text>();
        if (backButtonText != null)
        {
            backButtonText.text = "返回";
        }

        backButton = backButtonGo.GetComponent<Button>();
        y -= BackButtonHeight + 8f;

        CreateLabel(panel.transform, ControlStateJumpUiTitle, 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 36f;

        Dropdown targetStateDropdown = CreateLabeledDropdown(
            panel.transform,
            resources,
            "TargetStateDropdown",
            "目标状态",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            new List<string>
            {
                "地球级 (0)",
                "国家级 (1)",
                "省级 (2)",
                "车辆级 (3)",
                "零件级 (4)",
                "攻击路径级 (5)",
            },
            ControlStateStartUIDemo.DefaultTargetStateIndex);
        y -= RowHeight + 8f;

        Toggle instantToggle = CreateLabeledToggle(
            panel.transform,
            resources,
            InstantToggleRowName,
            "瞬时跳转",
            12f,
            y,
            PanelWidth - 24f,
            RowHeight,
            ControlStateStartUIDemo.DefaultUseInstantTransition);
        ApplyToggleCheckmarkSprite(
            instantToggle,
            preserved.InstantToggleCheckmarkSprite ?? resources.checkmark);
        y -= RowHeight + 4f;

        int provinceDefaultIndex = FindOptionIndex(provinceNames, ControlStateStartUIDemo.DefaultProvinceName);
        Dropdown provinceNameDropdown = CreateLabeledDropdown(
            panel.transform,
            resources,
            "ProvinceNameDropdown",
            "省级板块名字",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            provinceNames,
            provinceDefaultIndex);
        y -= RowHeight + 4f;

        int moduleDefaultIndex = FindOptionIndex(moduleNames, ControlStateStartUIDemo.DefaultProvinceModuleName);
        Dropdown provinceModuleDropdown = CreateLabeledDropdown(
            panel.transform,
            resources,
            "ProvinceModuleNameDropdown",
            "省级板块对象名",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            moduleNames,
            moduleDefaultIndex);
        y -= RowHeight + 4f;

        Dropdown partNameDropdown = CreateLabeledDropdown(
            panel.transform,
            resources,
            "PartNameDropdown",
            "车辆零部件名字",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            partNames,
            0);
        y -= RowHeight + 12f;

        GameObject buttonGo = DefaultControls.CreateButton(resources);
        buttonGo.name = "JumpButton";
        SetupChildRect(buttonGo, panel.transform, 12f, y, PanelWidth - 24f, 40f);
        Text buttonText = buttonGo.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = "跳转";
        }

        ControlStateStartUIDemo uiDemo = panel.AddComponent<ControlStateStartUIDemo>();
        SerializedObject serializedDemo = new SerializedObject(uiDemo);
        serializedDemo.FindProperty("_targetStateDropdown").objectReferenceValue = targetStateDropdown;
        serializedDemo.FindProperty("_instantTransitionToggle").objectReferenceValue = instantToggle;
        serializedDemo.FindProperty("_provinceNameDropdown").objectReferenceValue = provinceNameDropdown;
        serializedDemo.FindProperty("_provinceModuleNameDropdown").objectReferenceValue = provinceModuleDropdown;
        serializedDemo.FindProperty("_partNameDropdown").objectReferenceValue = partNameDropdown;
        serializedDemo.FindProperty("_jumpButton").objectReferenceValue = buttonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButton;
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

        ApplyPanelFont(panel, LoadDemoUiFont());

        return panel;
    }

    private static void DestroyExistingUi(Transform canvasTransform)
    {
        Transform existingRoot = canvasTransform.Find(UiRootObjectName);
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot.gameObject);
            return;
        }

        Transform existingPanel = canvasTransform.Find(PanelName);
        if (existingPanel != null)
        {
            Undo.DestroyObjectImmediate(existingPanel.gameObject);
        }
    }

    private static Transform FindExistingJumpPanel(Transform canvasTransform)
    {
        Transform root = canvasTransform.Find(UiRootObjectName);
        if (root != null)
        {
            Transform panel = root.Find(PanelName);
            if (panel != null)
            {
                return panel;
            }
        }

        return canvasTransform.Find(PanelName);
    }

    private static PreservedUiState CapturePreservedState(Transform canvasTransform)
    {
        PreservedUiState state = new PreservedUiState
        {
            PanelLayouts = new System.Collections.Generic.Dictionary<string, ControlStateJumpPanelLayout.RectLayoutData>(),
            InstantToggleCheckmarkSprite = null,
            HasUiRootLayout = false,
        };

        Transform uiRoot = canvasTransform.Find(UiRootObjectName);
        if (uiRoot != null)
        {
            RectTransform uiRootRect = uiRoot as RectTransform;
            if (uiRootRect != null)
            {
                state.UiRootLayout = ControlStateJumpPanelLayout.CaptureFromRectTransform(uiRootRect);
                state.HasUiRootLayout = true;
            }

            for (int i = 0; i < PreservedPanelNames.Length; i++)
            {
                TryCapturePanelLayout(uiRoot, PreservedPanelNames[i], state.PanelLayouts);
            }

            TryCaptureHttpApiFormScrollViewLayout(uiRoot, ref state);
            CaptureInstantToggleCheckmarkSprite(uiRoot, ref state.InstantToggleCheckmarkSprite);
            return state;
        }

        Transform legacyJumpPanel = FindExistingJumpPanel(canvasTransform);
        if (legacyJumpPanel != null)
        {
            TryCapturePanelLayout(legacyJumpPanel.parent, legacyJumpPanel.name, state.PanelLayouts);
            CaptureInstantToggleCheckmarkSprite(legacyJumpPanel, ref state.InstantToggleCheckmarkSprite);
        }

        return state;
    }

    private static void TryCaptureHttpApiFormScrollViewLayout(Transform uiRoot, ref PreservedUiState state)
    {
        if (uiRoot == null)
        {
            return;
        }

        Transform panel = uiRoot.Find(HttpApiTestPanelName);
        if (panel == null)
        {
            return;
        }

        HttpApiTestPanelLayout layoutComponent = panel.GetComponent<HttpApiTestPanelLayout>();
        if (layoutComponent != null
            && HttpApiTestPanelLayout.IsValidOffsets(layoutComponent.OffsetMin, layoutComponent.OffsetMax))
        {
            state.HttpApiFormScrollOffsetMin = layoutComponent.OffsetMin;
            state.HttpApiFormScrollOffsetMax = layoutComponent.OffsetMax;
            state.HasHttpApiFormScrollLayout = true;
            return;
        }

        Transform formScrollView = panel.Find("FormScrollView");
        RectTransform formScrollRect = formScrollView as RectTransform;
        if (formScrollRect != null && HttpApiTestPanelLayout.IsValidRect(formScrollRect))
        {
            state.HttpApiFormScrollOffsetMin = formScrollRect.offsetMin;
            state.HttpApiFormScrollOffsetMax = formScrollRect.offsetMax;
            state.HasHttpApiFormScrollLayout = true;
        }
    }

    private static void TryCapturePanelLayout(
        Transform parent,
        string panelName,
        System.Collections.Generic.Dictionary<string, ControlStateJumpPanelLayout.RectLayoutData> layouts)
    {
        if (parent == null || layouts == null || string.IsNullOrEmpty(panelName))
        {
            return;
        }

        Transform panel = parent.Find(panelName);
        if (panel == null)
        {
            return;
        }

        ControlStateJumpPanelLayout layoutComponent = panel.GetComponent<ControlStateJumpPanelLayout>();
        if (layoutComponent != null)
        {
            layouts[panelName] = layoutComponent.Layout;
            return;
        }

        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect != null)
        {
            layouts[panelName] = ControlStateJumpPanelLayout.CaptureFromRectTransform(rect);
        }
    }

    private static void CaptureInstantToggleCheckmarkSprite(Transform panelRoot, ref Sprite sprite)
    {
        if (panelRoot == null)
        {
            return;
        }

        Transform jumpPanel = panelRoot.name == PanelName ? panelRoot : panelRoot.Find(PanelName);
        if (jumpPanel == null)
        {
            return;
        }

        Transform checkmark = jumpPanel.Find(InstantToggleCheckmarkPath);
        if (checkmark == null)
        {
            return;
        }

        Image checkmarkImage = checkmark.GetComponent<Image>();
        if (checkmarkImage != null && checkmarkImage.sprite != null)
        {
            sprite = checkmarkImage.sprite;
        }
    }

    private static Font LoadDemoUiFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(DemoUiFontPath);
        if (font == null)
        {
            Debug.LogWarning($"[ControlStateStartUIBuilder] 未找到字体：{DemoUiFontPath}，将使用 LegacyRuntime.ttf。");
        }

        return font;
    }

    private static void ApplyPanelFont(GameObject panel, Font font)
    {
        if (panel == null || font == null)
        {
            return;
        }

        Text[] texts = panel.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].font = font;
        }
    }

    private static void ApplyPanelTextNormalStyle(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        Text[] texts = panel.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].fontStyle = FontStyle.Normal;
        }
    }

    private static DefaultControls.Resources CreateDefaultUiResources()
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

    private static void ApplyToggleCheckmarkSprite(Toggle toggle, Sprite checkmarkSprite)
    {
        if (toggle == null || checkmarkSprite == null)
        {
            return;
        }

        Transform checkmark = toggle.transform.Find("Checkmark");
        if (checkmark == null)
        {
            return;
        }

        Image image = checkmark.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = checkmarkSprite;
        }
    }

    private static void EnsureCanvasScaler(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static bool TryFindTargetCanvas(out Canvas canvas)
    {
        GameObject uiRoot = GameObject.Find(UiRootName);
        if (uiRoot != null)
        {
            canvas = uiRoot.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                return true;
            }
        }

        canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Debug.LogWarning(
                $"[ControlStateStartUIBuilder] 未找到 {UiRootName}，已回退到场景中的 Canvas：{canvas.name}");
            return true;
        }

        return false;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
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

    private static void ApplyPanelLayout(RectTransform panelRect, ControlStateJumpPanelLayout.RectLayoutData layout)
    {
        panelRect.anchorMin = layout.AnchorMin;
        panelRect.anchorMax = layout.AnchorMax;
        panelRect.pivot = layout.Pivot;
        panelRect.anchoredPosition = layout.AnchoredPosition;
        panelRect.sizeDelta = layout.SizeDelta;
        panelRect.localScale = layout.LocalScale;
    }

    private static int FindOptionIndex(IReadOnlyList<string> options, string value)
    {
        if (options == null || string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == value)
            {
                return i;
            }
        }

        return 0;
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        return panel;
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
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(parent, false);
        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);

        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
    }

    private static Toggle CreateLabeledToggle(
        Transform parent,
        DefaultControls.Resources resources,
        string name,
        string labelText,
        float x,
        float y,
        float width,
        float height,
        bool defaultValue)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetupChildRect(row, parent, x, y, width, height);

        CreateLabel(row.transform, labelText, 0f, 0f, LabelWidth, height, 14, FontStyle.Normal);

        GameObject toggleGo = DefaultControls.CreateToggle(resources);
        toggleGo.name = "Toggle";
        SetupChildRect(toggleGo, row.transform, LabelWidth, 0f, 28f, height);
        Toggle toggle = toggleGo.GetComponent<Toggle>();
        toggle.isOn = defaultValue;
        return toggle;
    }

    private static Dropdown CreateLabeledDropdown(
        Transform parent,
        DefaultControls.Resources resources,
        string name,
        string labelText,
        float x,
        float y,
        float labelWidth,
        float fieldWidth,
        IReadOnlyList<string> options,
        int defaultIndex)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetupChildRect(row, parent, x, y, labelWidth + fieldWidth + 8f, RowHeight);

        CreateLabel(row.transform, labelText, 0f, 0f, labelWidth, RowHeight, 14, FontStyle.Normal);

        GameObject dropdownGo = DefaultControls.CreateDropdown(resources);
        dropdownGo.name = "Dropdown";
        SetupChildRect(dropdownGo, row.transform, labelWidth + 8f, 0f, fieldWidth, RowHeight);
        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();
        ConfigureDropdownItemLabel(dropdown);
        dropdown.ClearOptions();
        if (options == null || options.Count == 0)
        {
            dropdown.AddOptions(new List<string> { "(无可用项)" });
        }
        else
        {
            dropdown.AddOptions(new List<string>(options));
            dropdown.value = Mathf.Clamp(defaultIndex, 0, options.Count - 1);
        }

        dropdown.RefreshShownValue();
        return dropdown;
    }

    /// <summary>
    /// 统一设置 Dropdown 模板 Item Label 字号，展开后动态克隆的列表项会沿用该模板。
    /// </summary>
    private static void ConfigureDropdownItemLabel(Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        Transform itemLabelTransform = dropdown.transform.Find(DropdownItemLabelPath);
        if (itemLabelTransform == null)
        {
            Debug.LogWarning($"[ControlStateStartUIBuilder] 未找到 Dropdown Item Label：{DropdownItemLabelPath}");
            return;
        }

        Text itemLabel = itemLabelTransform.GetComponent<Text>();
        if (itemLabel == null)
        {
            Debug.LogWarning("[ControlStateStartUIBuilder] Dropdown Item Label 缺少 Text 组件。");
            return;
        }

        itemLabel.fontSize = DropdownItemLabelFontSize;
    }

    private static InputField CreateLabeledInputField(
        Transform parent,
        DefaultControls.Resources resources,
        string name,
        string labelText,
        float x,
        float y,
        float labelWidth,
        float fieldWidth,
        string defaultText,
        int inputFontSize)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetupChildRect(row, parent, x, y, labelWidth + fieldWidth + 8f, RowHeight);

        CreateLabel(row.transform, labelText, 0f, 0f, labelWidth, RowHeight, 14, FontStyle.Normal);

        GameObject inputGo = DefaultControls.CreateInputField(resources);
        inputGo.name = "InputField";
        SetupChildRect(inputGo, row.transform, labelWidth + 8f, 0f, fieldWidth, RowHeight);
        InputField inputField = inputGo.GetComponent<InputField>();
        inputField.text = defaultText;
        ConfigureInputFieldTextSize(inputField, inputFontSize);
        return inputField;
    }

    private static void ConfigureInputFieldTextSize(InputField inputField, int fontSize)
    {
        if (inputField == null)
        {
            return;
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.fontSize = fontSize;
        }

        if (inputField.placeholder is Text placeholderText)
        {
            placeholderText.fontSize = fontSize;
        }
    }

    private static void SetupHttpApiSubPanelRect(GameObject subPanel, float height)
    {
        RectTransform rect = subPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, Mathf.Max(height, 120f));
    }

    private static void SetupChildRect(GameObject go, Transform parent, float x, float y, float width, float height)
    {
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
#endif
