using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单入口：对选中对象及其子级执行顶面轮廓烘焙。
/// </summary>
public static class SdMapPlateHudBakeMenu
{
    [MenuItem("GameObject/地图/烘焙顶面轮廓距离", false, 0)]
    private static void BakeFromGameObjectMenu()
    {
        BakeSelection();
    }

    [MenuItem("GameObject/地图/烘焙顶面轮廓距离", true)]
    private static bool BakeFromGameObjectMenuValidate()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/地图/烘焙顶面轮廓距离（选中对象）", false, 200)]
    private static void BakeFromToolsMenu()
    {
        BakeSelection();
    }

    private static void BakeSelection()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            return;
        }

        SdMapProvinceMaterialBinder binder = root.GetComponentInChildren<SdMapProvinceMaterialBinder>(true);
        if (binder != null)
        {
            binder.BakeTopContourDistance();
            Selection.activeGameObject = binder.gameObject;
            EditorGUIUtility.PingObject(binder);
            return;
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "烘焙顶面轮廓距离",
                "未找到 SdMapProvinceMaterialBinder 或 MeshRenderer。\n" +
                "请选中 sd_map 下的 polySurface1，或任意省级网格父节点。",
                "确定");
            return;
        }

        int baked = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter mf = renderers[i].GetComponent<MeshFilter>();
            if (mf != null)
            {
                SdMapPlateTopEdgeBaker.Bake(mf, 0.85f);
                baked++;
            }
        }

        Debug.Log($"[地图] 已对 {root.name} 下 {baked} 个网格烘焙顶面轮廓距离（无 Binder，使用默认阈值）。");
    }
}
