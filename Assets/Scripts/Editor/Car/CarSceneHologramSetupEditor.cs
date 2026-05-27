#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 Car 场景中布置双层车辆：mercedes（全息车身）+ mercedes_edge（边缘线），不运行时生成。
/// </summary>
public static class CarSceneHologramSetupEditor
{
    private const string BodyObjectName = "mercedes";
    private const string EdgeObjectName = "mercedes_edge";
    private const string HologramMaterialPath = "Assets/Materials/Car/M_MercedesHologram.mat";
    private const string EdgeMaterialPath = "Assets/Materials/Car/M_MercedesEdgeOutline.mat";

    [MenuItem("Tools/Car/场景：布置双层车辆（车身+边缘线）")]
    public static void SetupCarSceneDualLayer()
    {
        Material hologramMat = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
        Material edgeMat = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
        if (hologramMat == null || edgeMat == null)
        {
            Debug.LogError("[CarSceneHologramSetup] 缺少材质，请确认路径存在。");
            return;
        }

        GameObject bodyRoot = GameObject.Find(BodyObjectName);
        if (bodyRoot == null)
        {
            Debug.LogError("[CarSceneHologramSetup] 场景中未找到 " + BodyObjectName);
            return;
        }

        RemoveRuntimeOutlineComponents(bodyRoot);
        CleanupLegacyRuntimeObjects();

        ApplyMaterialToAllMeshRenderers(bodyRoot, hologramMat, disableShadows: false);

        GameObject edgeRoot = GameObject.Find(EdgeObjectName);
        if (edgeRoot == null)
        {
            edgeRoot = Object.Instantiate(bodyRoot);
            edgeRoot.name = EdgeObjectName;
            Undo.RegisterCreatedObjectUndo(edgeRoot, "Create mercedes_edge");
        }

        edgeRoot.transform.SetPositionAndRotation(bodyRoot.transform.position, bodyRoot.transform.rotation);
        edgeRoot.transform.localScale = bodyRoot.transform.localScale;

        RemoveRuntimeOutlineComponents(edgeRoot);
        ApplyMaterialToAllMeshRenderers(edgeRoot, edgeMat, disableShadows: true);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[CarSceneHologramSetup] 完成：mercedes=全息车身，mercedes_edge=边缘线（场景内静态副本）。");
    }

    [MenuItem("Tools/Car/场景：删除边缘线副本 mercedes_edge")]
    public static void RemoveEdgeLayer()
    {
        GameObject edgeRoot = GameObject.Find(EdgeObjectName);
        if (edgeRoot == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(edgeRoot);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void CleanupLegacyRuntimeObjects()
    {
        GameObject legacy = GameObject.Find("mercedes (1)");
        if (legacy != null)
        {
            Undo.DestroyObjectImmediate(legacy);
        }

        foreach (Transform child in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (child != null && child.name.EndsWith("_EdgeOutline"))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void RemoveRuntimeOutlineComponents(GameObject root)
    {
        // 历史运行时脚本已移除，此处仅清理遗留的 *_EdgeOutline 子物体
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name.EndsWith("_EdgeOutline"))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void ApplyMaterialToAllMeshRenderers(GameObject root, Material material, bool disableShadows)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            int count = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0
                ? renderer.sharedMaterials.Length
                : 1;

            Material[] mats = new Material[count];
            for (int i = 0; i < count; i++)
            {
                mats[i] = material;
            }

            renderer.sharedMaterials = mats;

            if (disableShadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            EditorUtility.SetDirty(renderer);
        }
    }
}
#endif
