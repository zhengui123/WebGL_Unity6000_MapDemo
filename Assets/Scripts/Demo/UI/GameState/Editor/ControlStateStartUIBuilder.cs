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
    private const string InstantToggleRowName = "InstantTransitionToggle";
    private const string InstantTogglePath = InstantToggleRowName + "/Toggle";
    private const string InstantToggleCheckmarkPath = InstantTogglePath + "/Checkmark";
    private const float PanelWidth = 360f;
    private const float RowHeight = 36f;
    private const float LabelWidth = 140f;
    private const float FieldWidth = 200f;
    private const float BackButtonHeight = 32f;
    private const float MenuButtonHeight = 40f;
    private const string ControlStateJumpUiTitle = "界面跳转——跨层级跳转";

    private struct PreservedPanelState
    {
        public ControlStateJumpPanelLayout.RectLayoutData Layout;
        public Sprite InstantToggleCheckmarkSprite;
    }

    [MenuItem("Tools/Demo/创建操控状态跳转 UI")]
    public static void CreateControlStateJumpUI()
    {
        if (!TryFindTargetCanvas(out Canvas canvas))
        {
            Debug.LogError($"[ControlStateStartUIBuilder] 未在 {UiRootName} 下找到 Canvas。");
            return;
        }

        EnsureEventSystem();

        PreservedPanelState preserved = CapturePreservedState(canvas.transform);
        DestroyExistingUi(canvas.transform);

        DefaultControls.Resources resources = CreateDefaultUiResources();
        ControlStateJumpPanelLayout.RectLayoutData sharedLayout = preserved.Layout;

        GameObject uiRoot = new GameObject(UiRootObjectName, typeof(RectTransform));
        uiRoot.transform.SetParent(canvas.transform, false);
        SetupFullStretchRect(uiRoot.GetComponent<RectTransform>());
        DemoGameStateUINavigator navigator = uiRoot.AddComponent<DemoGameStateUINavigator>();

        GameObject menuPanel = CreateMenuPanel(uiRoot.transform, resources, sharedLayout, navigator);
        GameObject jumpPanel = CreateControlStateJumpPanel(
            uiRoot.transform,
            resources,
            preserved,
            navigator,
            out Button backButton);

        SerializedObject serializedNavigator = new SerializedObject(navigator);
        serializedNavigator.FindProperty("_menuPanel").objectReferenceValue = menuPanel;
        serializedNavigator.FindProperty("_controlStateJumpPanel").objectReferenceValue = jumpPanel;
        serializedNavigator.ApplyModifiedPropertiesWithoutUndo();

        jumpPanel.SetActive(false);

        Undo.RegisterCreatedObjectUndo(uiRoot, "Create Demo GameState UI");
        Selection.activeGameObject = uiRoot;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[ControlStateStartUIBuilder] 已在 {UiRootName}/Canvas 下创建 {UiRootObjectName}。");
    }

    private static GameObject CreateMenuPanel(
        Transform parent,
        DefaultControls.Resources resources,
        ControlStateJumpPanelLayout.RectLayoutData layout,
        DemoGameStateUINavigator navigator)
    {
        GameObject panel = CreatePanel(parent, MenuPanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, layout);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        float y = -12f;
        CreateLabel(panel.transform, "Demo 功能菜单", 12f, y, PanelWidth - 24f, 28f, 18, FontStyle.Bold);
        y -= 28f + 12f;

        GameObject entryButtonGo = DefaultControls.CreateButton(resources);
        entryButtonGo.name = "ControlStateJumpEntryButton";
        SetupChildRect(entryButtonGo, panel.transform, 12f, y, PanelWidth - 24f, MenuButtonHeight);
        Text entryButtonText = entryButtonGo.GetComponentInChildren<Text>();
        if (entryButtonText != null)
        {
            entryButtonText.text = ControlStateJumpUiTitle;
        }

        DemoGameStateMenuUIDemo menuDemo = panel.AddComponent<DemoGameStateMenuUIDemo>();
        SerializedObject serializedMenu = new SerializedObject(menuDemo);
        serializedMenu.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedMenu.FindProperty("_controlStateJumpEntryButton").objectReferenceValue =
            entryButtonGo.GetComponent<Button>();
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    private static GameObject CreateControlStateJumpPanel(
        Transform parent,
        DefaultControls.Resources resources,
        PreservedPanelState preserved,
        DemoGameStateUINavigator navigator,
        out Button backButton)
    {
        GameObject panel = CreatePanel(parent, PanelName);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ApplyPanelLayout(panelRect, preserved.Layout);

        ControlStateJumpPanelLayout layoutComponent = panel.AddComponent<ControlStateJumpPanelLayout>();
        layoutComponent.CaptureFrom(panelRect, preserved.InstantToggleCheckmarkSprite);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

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

        InputField provinceNameInput = CreateLabeledInputField(
            panel.transform,
            resources,
            "ProvinceNameInput",
            "省级板块名字",
            12f,
            y,
            LabelWidth,
            FieldWidth,
            ControlStateStartUIDemo.DefaultProvinceName);
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
        serializedDemo.FindProperty("_provinceNameInput").objectReferenceValue = provinceNameInput;
        serializedDemo.FindProperty("_provinceModuleNameDropdown").objectReferenceValue = provinceModuleDropdown;
        serializedDemo.FindProperty("_partNameDropdown").objectReferenceValue = partNameDropdown;
        serializedDemo.FindProperty("_jumpButton").objectReferenceValue = buttonGo.GetComponent<Button>();
        serializedDemo.FindProperty("_backButton").objectReferenceValue = backButton;
        serializedDemo.FindProperty("_navigator").objectReferenceValue = navigator;
        serializedDemo.FindProperty("_controlStateStartDemo").objectReferenceValue =
            Object.FindFirstObjectByType<ControlStateStartDemo>();
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();

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

    private static PreservedPanelState CapturePreservedState(Transform canvasTransform)
    {
        PreservedPanelState state = new PreservedPanelState
        {
            Layout = ControlStateJumpPanelLayout.CreateDefault(),
            InstantToggleCheckmarkSprite = null,
        };

        Transform existing = FindExistingJumpPanel(canvasTransform);
        if (existing == null)
        {
            return state;
        }

        ControlStateJumpPanelLayout layoutComponent = existing.GetComponent<ControlStateJumpPanelLayout>();
        if (layoutComponent != null)
        {
            state.Layout = layoutComponent.Layout;
            state.InstantToggleCheckmarkSprite = layoutComponent.InstantToggleCheckmarkSprite;
        }
        else
        {
            RectTransform rect = existing.GetComponent<RectTransform>();
            if (rect != null)
            {
                state.Layout = new ControlStateJumpPanelLayout.RectLayoutData
                {
                    AnchorMin = rect.anchorMin,
                    AnchorMax = rect.anchorMax,
                    Pivot = rect.pivot,
                    AnchoredPosition = rect.anchoredPosition,
                    SizeDelta = rect.sizeDelta,
                    LocalScale = rect.localScale,
                };
            }
        }

        Transform checkmark = existing.Find(InstantToggleCheckmarkPath);
        if (checkmark != null)
        {
            Image checkmarkImage = checkmark.GetComponent<Image>();
            if (checkmarkImage != null && checkmarkImage.sprite != null)
            {
                state.InstantToggleCheckmarkSprite = checkmarkImage.sprite;
            }
        }

        return state;
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

    private static InputField CreateLabeledInputField(
        Transform parent,
        DefaultControls.Resources resources,
        string name,
        string labelText,
        float x,
        float y,
        float labelWidth,
        float fieldWidth,
        string defaultText)
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
        return inputField;
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
