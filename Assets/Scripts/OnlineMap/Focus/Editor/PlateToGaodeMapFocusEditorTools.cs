#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlateToGaodeMapFocusEditorTools
{
    private const string ScanlineMatPath = "Assets/Scripts/OnlineMap/Focus/PlateToGaodeScanlineOverlay.mat";

    [MenuItem("Tools/OnlineMap/配置 Plate→Gaode 过渡（OnlineMapManger）")]
    public static void SetupTransitionOnOnlineMapManager()
    {
        GameObject manager = GameObject.Find("OnlineMapManger");
        if (manager == null)
        {
            Debug.LogError("[PlateToGaodeFocus] 未找到 OnlineMapManger。");
            return;
        }

        EnsureScanlineMaterial();

        PlateToGaodeMapTransitionController controller =
            manager.GetComponent<PlateToGaodeMapTransitionController>() ??
            manager.AddComponent<PlateToGaodeMapTransitionController>();

        PlateToGaodeMapScanlineOverlay overlay =
            manager.GetComponent<PlateToGaodeMapScanlineOverlay>() ??
            manager.AddComponent<PlateToGaodeMapScanlineOverlay>();

        GaodeMapTransitionVisibility visibility =
            manager.GetComponent<GaodeMapTransitionVisibility>() ??
            manager.AddComponent<GaodeMapTransitionVisibility>();

        SerializedObject transitionSo = new SerializedObject(controller);
        AssignIfNull(transitionSo, "_allPlateMapRoot", GameObject.Find("AllPlateMap"));
        AssignIfNull(transitionSo, "_gaodeMapController", manager.GetComponent<GaodeMapController>());
        AssignIfNull(transitionSo, "_provinceFocusController", manager.GetComponent<GaodeMapProvinceFocusController>());
        AssignIfNull(transitionSo, "_gaodeVisibility", visibility);
        transitionSo.FindProperty("_scanlineOverlay").objectReferenceValue = overlay;
        transitionSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject visibilitySo = new SerializedObject(visibility);
        AssignIfNull(visibilitySo, "_gaodeMapController", manager.GetComponent<GaodeMapController>());
        visibilitySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject overlaySo = new SerializedObject(overlay);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ScanlineMatPath);
        if (mat != null)
        {
            overlaySo.FindProperty("_overlayMaterial").objectReferenceValue = mat;
        }

        overlaySo.ApplyModifiedPropertiesWithoutUndo();

        GameObject demo = GameObject.Find("PlateToGaodeMapTransitionDemo");
        if (demo != null)
        {
            SerializedObject demoSo = new SerializedObject(demo.GetComponent<PlateToGaodeMapTransitionDemo>());
            demoSo.FindProperty("_transitionController").objectReferenceValue = controller;
            demoSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(manager);
        Debug.Log("[PlateToGaodeFocus] 配置完成。");
    }

    private static void EnsureScanlineMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(ScanlineMatPath) != null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/PlateToGaodeScanlineOverlay");
        if (shader == null)
        {
            return;
        }

        AssetDatabase.CreateAsset(new Material(shader) { name = "PlateToGaodeScanlineOverlay" }, ScanlineMatPath);
        AssetDatabase.SaveAssets();
    }

    private static void AssignIfNull(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null && prop.objectReferenceValue == null && value != null)
        {
            prop.objectReferenceValue = value;
        }
    }
}
#endif
