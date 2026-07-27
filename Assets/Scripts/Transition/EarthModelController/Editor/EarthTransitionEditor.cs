#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EarthTransition Inspector：国外板块 AllPlateMap 位置快捷保存 / 应用。
/// 目标板块列表与 <see cref="WorldMapRegionController"/> 绑定同步；Play 时保存到当前激活板块。
/// </summary>
[CustomEditor(typeof(EarthTransition))]
public class EarthTransitionEditor : Editor
{
    private const string DefaultConfigPath =
        "Assets/Scripts/Transition/EarthModelController/EarthPlateMapPositionConfig.asset";

    private SerializedProperty _foreignPlateMapPositionConfig;
    private int _selectedPlateIndex;
    private string[] _plateLabels = Array.Empty<string>();
    private string[] _plateCodes = Array.Empty<string>();

    private void OnEnable()
    {
        _foreignPlateMapPositionConfig = serializedObject.FindProperty("_foreignPlateMapPositionConfig");
        RefreshPlateOptions();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("国外板块 AllPlateMap 位置工具", EditorStyles.boldLabel);

        EarthTransition transition = (EarthTransition)target;
        EnsureConfigAssigned(transition);

        if (!transition.UseForeignPlateMapPositionConfig)
        {
            EditorGUILayout.HelpBox(
                "「Use Foreign Plate Map Position Config」已关闭：运行时国外一律相机前方自动；下方保存仍可写入 Config。",
                MessageType.Warning);
        }

        if (transition.ForeignPlateMapPositionConfig == null)
        {
            EditorGUILayout.HelpBox(
                "未指定国外位置配置。可点击下方创建默认资源，或拖入 EarthPlateMapPositionConfig。",
                MessageType.Warning);
            if (GUILayout.Button("创建并指定默认配置资源"))
            {
                CreateAndAssignConfig(transition);
            }

            return;
        }

        RefreshPlateOptions();
        SyncSelectedIndexToActivePlate();

        if (_plateCodes.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "场景中未找到 WorldMapRegionController 的国外板块绑定，无法选择 plateCode。",
                MessageType.Info);
            return;
        }

        bool playMode = EditorApplication.isPlaying;
        if (playMode)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Popup("目标国外板块（当前激活）", _selectedPlateIndex, _plateLabels);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox(
                "Play 模式：保存写入当前激活国外板块（与 WorldMapRegionController 同步）。",
                MessageType.Info);
        }
        else
        {
            _selectedPlateIndex = Mathf.Clamp(_selectedPlateIndex, 0, _plateCodes.Length - 1);
            _selectedPlateIndex = EditorGUILayout.Popup("目标国外板块", _selectedPlateIndex, _plateLabels);
        }

        string plateCode = _plateCodes[Mathf.Clamp(_selectedPlateIndex, 0, _plateCodes.Length - 1)];
        string plateName = ExtractPlateName(_plateLabels[Mathf.Clamp(_selectedPlateIndex, 0, _plateLabels.Length - 1)]);

        EditorGUILayout.BeginHorizontal();
        string saveLabel = playMode ? "保存当前 AllPlateMap → 激活板块" : "保存当前 AllPlateMap → 该板块";
        if (GUILayout.Button(saveLabel))
        {
            if (playMode && !TryResolveActiveForeignPlate(out plateCode, out plateName))
            {
                EditorUtility.DisplayDialog(
                    "保存失败",
                    "当前不是国外激活板块。请先在 WorldMapRegionController 切到某个国外大板块。",
                    "确定");
            }
            else
            {
                SaveCurrentLocalPosition(transition, plateCode, plateName);
            }
        }

        if (GUILayout.Button("应用该板块配置到场景"))
        {
            if (playMode && !TryResolveActiveForeignPlate(out plateCode, out plateName))
            {
                EditorUtility.DisplayDialog(
                    "应用失败",
                    "当前不是国外激活板块。",
                    "确定");
            }
            else
            {
                ApplyConfigToScene(transition, plateCode);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("同步绑定列表 → Config（补齐缺失条目）"))
        {
            SyncBindingsToConfig(transition);
        }

        if (transition.ForeignPlateMapPositionConfig.TryGetLocalPosition(plateCode, out Vector3 saved))
        {
            EditorGUILayout.HelpBox($"当前目标 {plateName} ({plateCode}) 已配置 local：{saved}", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"当前目标 {plateName} ({plateCode}) 尚未保存配置；运行时将使用相机前方自动位置。",
                MessageType.Info);
        }
    }

    private void SyncSelectedIndexToActivePlate()
    {
        if (!EditorApplication.isPlaying || _plateCodes.Length == 0)
        {
            return;
        }

        if (!TryResolveActiveForeignPlate(out string activeCode, out _))
        {
            return;
        }

        for (int i = 0; i < _plateCodes.Length; i++)
        {
            if (string.Equals(_plateCodes[i], activeCode, StringComparison.OrdinalIgnoreCase))
            {
                _selectedPlateIndex = i;
                return;
            }
        }
    }

    private static bool TryResolveActiveForeignPlate(out string plateCode, out string plateName)
    {
        plateCode = string.Empty;
        plateName = string.Empty;

        WorldMapRegionController region = WorldMapRegionController.Instance;
        if (region == null || WorldMapRegionContext.Mode != WorldMapRegionMode.Foreign)
        {
            return false;
        }

        plateCode = region.ActiveForeignPlateCode;
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            plateCode = WorldMapRegionContext.PlateCode;
        }

        if (string.IsNullOrWhiteSpace(plateCode) ||
            string.Equals(plateCode, WorldMapRegionCodeTable.DomesticNationalCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        plateName = WorldMapRegionContext.PlateName;
        if (region.TryGetForeignBinding(plateCode, out WorldMapRegionController.ForeignPlateBinding binding) &&
            binding != null &&
            !string.IsNullOrWhiteSpace(binding.plateName))
        {
            plateName = binding.plateName.Trim();
        }

        return true;
    }

    private void EnsureConfigAssigned(EarthTransition transition)
    {
        if (transition.ForeignPlateMapPositionConfig != null || _foreignPlateMapPositionConfig == null)
        {
            return;
        }

        EarthPlateMapPositionConfig existing =
            AssetDatabase.LoadAssetAtPath<EarthPlateMapPositionConfig>(DefaultConfigPath);
        if (existing == null)
        {
            return;
        }

        serializedObject.Update();
        _foreignPlateMapPositionConfig.objectReferenceValue = existing;
        serializedObject.ApplyModifiedProperties();
    }

    private void CreateAndAssignConfig(EarthTransition transition)
    {
        EarthPlateMapPositionConfig existing =
            AssetDatabase.LoadAssetAtPath<EarthPlateMapPositionConfig>(DefaultConfigPath);
        if (existing == null)
        {
            existing = CreateInstance<EarthPlateMapPositionConfig>();
            AssetDatabase.CreateAsset(existing, DefaultConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Undo.RecordObject(transition, "Assign EarthPlateMapPositionConfig");
        transition.ForeignPlateMapPositionConfig = existing;
        EditorUtility.SetDirty(transition);
        serializedObject.Update();
    }

    private void RefreshPlateOptions()
    {
        var codes = new List<string>();
        var labels = new List<string>();

        WorldMapRegionController region =
            FindFirstObjectByType<WorldMapRegionController>(FindObjectsInactive.Include);
        if (region != null)
        {
            IReadOnlyList<WorldMapRegionController.ForeignPlateBinding> plates = region.ForeignPlates;
            for (int i = 0; i < plates.Count; i++)
            {
                WorldMapRegionController.ForeignPlateBinding binding = plates[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.plateCode))
                {
                    continue;
                }

                string code = binding.plateCode.Trim();
                if (codes.Exists(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string name = binding.plateName;
                codes.Add(code);
                labels.Add(string.IsNullOrWhiteSpace(name) ? code : $"{name.Trim()} ({code})");
            }
        }

        _plateCodes = codes.ToArray();
        _plateLabels = labels.ToArray();
    }

    private static string ExtractPlateName(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        int idx = label.LastIndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? label.Substring(0, idx) : label;
    }

    private static void SaveCurrentLocalPosition(EarthTransition transition, string plateCode, string plateName)
    {
        GameObject plateMap = transition.PlateMapObj;
        if (plateMap == null)
        {
            EditorUtility.DisplayDialog("保存失败", "EarthTransition.plateMapObj 未赋值。", "确定");
            return;
        }

        EarthPlateMapPositionConfig config = transition.ForeignPlateMapPositionConfig;
        if (config == null)
        {
            EditorUtility.DisplayDialog("保存失败", "未指定 ForeignPlateMapPositionConfig。", "确定");
            return;
        }

        Vector3 local = plateMap.transform.localPosition;
        Undo.RecordObject(config, "Save Foreign Plate Map Position");
        config.SetOrAddLocalPosition(plateCode, plateName, local);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EarthTransitionEditor] 已保存 | {plateName} ({plateCode}) → local={local}");
    }

    private static void ApplyConfigToScene(EarthTransition transition, string plateCode)
    {
        EarthPlateMapPositionConfig config = transition.ForeignPlateMapPositionConfig;
        GameObject plateMap = transition.PlateMapObj;
        if (config == null || plateMap == null)
        {
            EditorUtility.DisplayDialog("应用失败", "缺少 Config 或 plateMapObj。", "确定");
            return;
        }

        if (!config.TryGetLocalPosition(plateCode, out Vector3 local))
        {
            EditorUtility.DisplayDialog("应用失败", $"配置中尚无板块：{plateCode}", "确定");
            return;
        }

        Undo.RecordObject(plateMap.transform, "Apply Foreign Plate Map Position");
        plateMap.transform.localPosition = local;
        EditorUtility.SetDirty(plateMap);
        Debug.Log($"[EarthTransitionEditor] 已应用 | code={plateCode} | local={local}");
    }

    private static void SyncBindingsToConfig(EarthTransition transition)
    {
        EarthPlateMapPositionConfig config = transition.ForeignPlateMapPositionConfig;
        WorldMapRegionController region =
            FindFirstObjectByType<WorldMapRegionController>(FindObjectsInactive.Include);
        if (config == null || region == null)
        {
            EditorUtility.DisplayDialog("同步失败", "缺少 Config 或 WorldMapRegionController。", "确定");
            return;
        }

        Undo.RecordObject(config, "Sync Foreign Plate Bindings To Config");
        int added = config.SyncEntriesFromForeignBindings(region.ForeignPlates);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "同步完成",
            $"已对照 WorldMapRegionController 绑定列表。\n新补齐条目：{added}\n已有条目坐标未覆盖。",
            "确定");
    }
}
#endif
