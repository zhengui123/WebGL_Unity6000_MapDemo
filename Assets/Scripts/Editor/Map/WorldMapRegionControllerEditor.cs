#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WorldMapRegionController Inspector：启动国外板块改为「已绑定大板块」名称下拉。
/// </summary>
[CustomEditor(typeof(WorldMapRegionController))]
public class WorldMapRegionControllerEditor : Editor
{
    private SerializedProperty _startMode;
    private SerializedProperty _startForeignPlateCode;
    private SerializedProperty _domesticPlateRoot;
    private SerializedProperty _foreignPlates;
    private SerializedProperty _displayController;
    private SerializedProperty _worldModeBackgroundLine;
    private SerializedProperty _restoreNationalViewOnSwitch;

    private void OnEnable()
    {
        _startMode = serializedObject.FindProperty("_startMode");
        _startForeignPlateCode = serializedObject.FindProperty("_startForeignPlateCode");
        _domesticPlateRoot = serializedObject.FindProperty("_domesticPlateRoot");
        _foreignPlates = serializedObject.FindProperty("_foreignPlates");
        _displayController = serializedObject.FindProperty("_displayController");
        _worldModeBackgroundLine = serializedObject.FindProperty("_worldModeBackgroundLine");
        _restoreNationalViewOnSwitch = serializedObject.FindProperty("_restoreNationalViewOnSwitch");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("启动", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_startMode);
        DrawStartForeignPlatePopup();
        DrawApplyCurrentSelectionButton();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("国内", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_domesticPlateRoot);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("国外大板块", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_foreignPlates, includeChildren: true);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("显示", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_displayController);
        EditorGUILayout.PropertyField(_worldModeBackgroundLine);
        EditorGUILayout.PropertyField(_restoreNationalViewOnSwitch);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStartForeignPlatePopup()
    {
        if (_startForeignPlateCode == null || _foreignPlates == null)
        {
            return;
        }

        BuildBoundPlateOptions(out List<string> codes, out List<string> labels);
        if (codes.Count == 0)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Popup(
                new GUIContent("启动国外板块", "请先在「国外大板块」中配置绑定项"),
                0,
                new[] { "(无已绑定板块)" });
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox("请先在下方「国外大板块」中添加绑定，启动国外板块下拉才会出现选项。", MessageType.Info);
            return;
        }

        string currentCode = _startForeignPlateCode.stringValue != null
            ? _startForeignPlateCode.stringValue.Trim()
            : string.Empty;
        int selected = FindCodeIndex(codes, currentCode);
        if (selected < 0)
        {
            // 当前值不在已绑定列表：追加一项以便显示并可选回合法项
            labels.Insert(0, string.IsNullOrEmpty(currentCode)
                ? "(未匹配绑定)"
                : $"(未绑定) {currentCode}");
            codes.Insert(0, currentCode);
            selected = 0;
        }

        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup(
            new GUIContent("启动国外板块", "仅列出已绑定的大板块；显示板块名，内部仍存 plateCode"),
            selected,
            labels.ToArray());
        if (EditorGUI.EndChangeCheck() && next >= 0 && next < codes.Count)
        {
            _startForeignPlateCode.stringValue = codes[next] ?? string.Empty;
        }
    }

    private void BuildBoundPlateOptions(out List<string> codes, out List<string> labels)
    {
        codes = new List<string>();
        labels = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _foreignPlates.arraySize; i++)
        {
            SerializedProperty element = _foreignPlates.GetArrayElementAtIndex(i);
            if (element == null)
            {
                continue;
            }

            SerializedProperty codeProp = element.FindPropertyRelative("plateCode");
            SerializedProperty nameProp = element.FindPropertyRelative("plateName");
            string code = codeProp != null && codeProp.stringValue != null
                ? codeProp.stringValue.Trim()
                : string.Empty;
            if (string.IsNullOrEmpty(code) || !seen.Add(code))
            {
                continue;
            }

            string name = nameProp != null && nameProp.stringValue != null
                ? nameProp.stringValue.Trim()
                : string.Empty;
            // 显示仅大板块名；名为空时用占位，避免空白项
            labels.Add(string.IsNullOrEmpty(name) ? $"(未命名 #{i})" : name);
            codes.Add(code);
        }
    }

    private static int FindCodeIndex(List<string> codes, string code)
    {
        if (codes == null || string.IsNullOrEmpty(code))
        {
            return -1;
        }

        for (int i = 0; i < codes.Count; i++)
        {
            if (string.Equals(codes[i], code, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void DrawApplyCurrentSelectionButton()
    {
        WorldMapRegionController controller = target as WorldMapRegionController;
        if (controller == null)
        {
            return;
        }

        string buttonLabel = "切换到当前所选状态";
        string helpText = _startMode != null && _startMode.enumValueIndex == (int)WorldMapRegionMode.Foreign
            ? "将切到当前选中的国外板块。"
            : "将切到当前选中的国内状态。";

        EditorGUILayout.HelpBox(helpText, MessageType.None);
        if (!GUILayout.Button(buttonLabel))
        {
            return;
        }

        if (_startMode != null && _startMode.enumValueIndex == (int)WorldMapRegionMode.Foreign)
        {
            string plateCode = _startForeignPlateCode != null ? _startForeignPlateCode.stringValue : string.Empty;
            if (!controller.SwitchToForeignPlate(plateCode))
            {
                EditorUtility.DisplayDialog("切换失败", $"未能切换到国外板块：{plateCode}", "确定");
            }
        }
        else
        {
            controller.SwitchToDomestic();
        }

        EditorUtility.SetDirty(controller);
    }
}
#endif
