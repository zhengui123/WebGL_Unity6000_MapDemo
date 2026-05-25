using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SdMapProvinceMaterialBinder))]
public class SdMapProvinceMaterialBinderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var binder = (SdMapProvinceMaterialBinder)target;
        Material mat = GetProvinceMaterial(binder);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("顶面轮廓距离", EditorStyles.boldLabel);

        bool isHud = mat != null && mat.shader != null && mat.shader.name == "Custom/SdMapPlateHud";
        if (!isHud)
        {
            EditorGUILayout.HelpBox(
                "当前材质不是 Custom/SdMapPlateHud，无需烘焙。\n" +
                "请把 Province Material 设为 M_SdMapPlateHud，或 Default Material Path 指向该材质。",
                MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(!isHud);
        if (GUILayout.Button("烘焙顶面轮廓距离", GUILayout.Height(28)))
        {
            Undo.RecordObjects(binder.GetComponentsInChildren<Transform>(true), "Bake Top Contour");
            binder.BakeTopContourDistance();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("应用材质并烘焙", GUILayout.Height(24)))
        {
            binder.ApplyMaterial();
            SceneView.RepaintAll();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.HelpBox(
            "挂载位置：地图下 polySurface1（不是 sd_map 根节点）。\n" +
            "烘焙数据在顶点色 R：0=外轮廓，1=中心。",
            MessageType.None);
    }

    private static Material GetProvinceMaterial(SdMapProvinceMaterialBinder binder)
    {
        SerializedObject so = new SerializedObject(binder);
        SerializedProperty prop = so.FindProperty("_provinceMaterial");
        if (prop != null && prop.objectReferenceValue is Material m)
        {
            return m;
        }

        prop = so.FindProperty("_defaultMaterialPath");
        if (prop != null && !string.IsNullOrEmpty(prop.stringValue))
        {
            return AssetDatabase.LoadAssetAtPath<Material>(prop.stringValue);
        }

        return null;
    }
}
