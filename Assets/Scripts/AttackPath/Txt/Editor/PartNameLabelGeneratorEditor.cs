#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="PartNameLabelGenerator"/> Inspector：生成/重置、删除、应用样式按钮。
/// </summary>
[CustomEditor(typeof(PartNameLabelGenerator))]
public class PartNameLabelGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);

        PartNameLabelGenerator generator = (PartNameLabelGenerator)target;

        if (GUILayout.Button("生成 / 重置零部件名称文本", GUILayout.Height(28f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "生成零部件名称文本");
            generator.GenerateLabels();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("应用文字大小与粗细", GUILayout.Height(28f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "应用文字样式");
            generator.ApplyStyleToExistingLabels();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
        if (GUILayout.Button("删除零部件名称文本", GUILayout.Height(28f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "删除零部件名称文本");
            generator.ClearLabels();
            EditorUtility.SetDirty(generator);
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "生成会先清除旧标签再重建。位置=模型包围盒顶面+Offset（不含文字自身）。「应用文字大小与粗细」只改样式不改高度。若旧标签高度已错乱，请先删除再生成。",
            MessageType.Info);
    }
}
#endif
