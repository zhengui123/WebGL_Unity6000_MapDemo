#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        GameObject rawImgGo = GameObject.Find("GaodeMap_RawImg");
        GaodeMapRawImageVisibility rawVisibility = null;
        if (rawImgGo != null)
        {
            rawVisibility = rawImgGo.GetComponent<GaodeMapRawImageVisibility>() ??
                            rawImgGo.AddComponent<GaodeMapRawImageVisibility>();
        }

        SerializedObject transitionSo = new SerializedObject(controller);
        AssignIfNull(transitionSo, "_allPlateMapRoot", GameObject.Find("AllPlateMap"));
        AssignIfNull(transitionSo, "_gaodeMapController", manager.GetComponent<GaodeMapController>());
        AssignIfNull(transitionSo, "_provinceFocusController", manager.GetComponent<GaodeMapProvinceFocusController>());
        AssignIfNull(transitionSo, "_gaodeRawImageVisibility", rawVisibility);
        transitionSo.FindProperty("_scanlineOverlay").objectReferenceValue = overlay;
        transitionSo.ApplyModifiedPropertiesWithoutUndo();

        if (rawVisibility != null)
        {
            SerializedObject rawSo = new SerializedObject(rawVisibility);
            AssignIfNull(rawSo, "_rawImage", rawImgGo != null ? rawImgGo.GetComponent<RawImage>() : null);
            rawSo.ApplyModifiedPropertiesWithoutUndo();
        }

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
        if (rawImgGo != null)
        {
            EditorUtility.SetDirty(rawImgGo);
        }

        Debug.Log("[PlateToGaodeFocus] 配置完成（显隐由 GaodeMap_RawImg 控制）。");
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
