#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将「世界地图」下各板块内的「边界线_板块内部」世界位姿对齐到「世界地图边界线」。
/// 对齐 position + rotation，不改 scale。
/// </summary>
public static class WorldMapInnerBoundaryAlignEditor
{
    private const string WorldMapRootName = "世界地图";
    private const string ReferenceBoundaryName = "世界地图边界线";
    private const string InnerBoundaryName = "边界线_板块内部";

    [MenuItem("Tools/Map/对齐板块内部边界线到世界地图边界线")]
    public static void AlignInnerBoundariesToWorldBoundary()
    {
        Transform worldMapRoot = FindNamedTransformInLoadedScenes(WorldMapRootName);
        if (worldMapRoot == null)
        {
            EditorUtility.DisplayDialog(
                "对齐失败",
                $"当前已加载场景中未找到「{WorldMapRootName}」。",
                "确定");
            return;
        }

        Transform reference = FindDirectOrDescendantChild(worldMapRoot, ReferenceBoundaryName);
        if (reference == null)
        {
            EditorUtility.DisplayDialog(
                "对齐失败",
                $"在「{WorldMapRootName}」下未找到「{ReferenceBoundaryName}」。",
                "确定");
            return;
        }

        Vector3 targetWorldPos = reference.position;
        Quaternion targetWorldRot = reference.rotation;
        List<Transform> innerBoundaries = CollectNamedDescendants(worldMapRoot, InnerBoundaryName);
        // 排除参考物自身（名称不同一般不会命中，防御性过滤）
        innerBoundaries.RemoveAll(t => t == null || t == reference);

        if (innerBoundaries.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "对齐完成",
                $"在「{WorldMapRootName}」下未找到「{InnerBoundaryName}」。",
                "确定");
            return;
        }

        int changed = 0;
        Undo.SetCurrentGroupName("对齐板块内部边界线世界位姿");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < innerBoundaries.Count; i++)
        {
            Transform inner = innerBoundaries[i];
            bool posMatch = (inner.position - targetWorldPos).sqrMagnitude <= 1e-10f;
            bool rotMatch = Quaternion.Angle(inner.rotation, targetWorldRot) <= 0.01f;
            if (posMatch && rotMatch)
            {
                continue;
            }

            Undo.RecordObject(inner, "Align Inner Boundary Pose");
            inner.SetPositionAndRotation(targetWorldPos, targetWorldRot);
            EditorUtility.SetDirty(inner);
            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkDirtyScenesContaining(worldMapRoot);

        string message =
            $"参考：{GetHierarchyPath(reference)}\n" +
            $"目标世界坐标：{targetWorldPos}\n" +
            $"目标世界旋转：{targetWorldRot.eulerAngles}\n" +
            $"找到「{InnerBoundaryName}」：{innerBoundaries.Count}\n" +
            $"已修改 position+rotation：{changed}";
        Debug.Log($"[WorldMapInnerBoundaryAlign] {message.Replace("\n", " | ")}");
        EditorUtility.DisplayDialog("对齐完成", message, "确定");
    }

    private static Transform FindNamedTransformInLoadedScenes(string objectName)
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform found = FindNamedInHierarchy(roots[r].transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static Transform FindNamedInHierarchy(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedInHierarchy(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDirectOrDescendantChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }
        }

        return FindNamedInHierarchy(root, objectName);
    }

    private static List<Transform> CollectNamedDescendants(Transform root, string objectName)
    {
        var result = new List<Transform>(32);
        if (root == null)
        {
            return result;
        }

        CollectNamedDescendantsRecursive(root, objectName, result);
        return result;
    }

    private static void CollectNamedDescendantsRecursive(
        Transform current,
        string objectName,
        List<Transform> result)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child.name == objectName)
            {
                result.Add(child);
            }

            CollectNamedDescendantsRecursive(child, objectName, result);
        }
    }

    private static void MarkDirtyScenesContaining(Transform transform)
    {
        if (transform == null)
        {
            return;
        }

        Scene scene = transform.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        string path = t.name;
        Transform parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
#endif
