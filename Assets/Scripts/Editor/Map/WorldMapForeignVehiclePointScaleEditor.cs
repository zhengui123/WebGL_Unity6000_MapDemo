#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将世界地图下国外国家节点上的 PlateMapVehiclePointController
/// 点位缩放统一设置为指定值，并关闭调试映射 Cube。
/// </summary>
public static class WorldMapForeignVehiclePointScaleEditor
{
    private const string WorldMapRootName = "世界地图";
    private static readonly Vector3 TargetPointLocalScale = new Vector3(10f, 10f, 10f);
    private const bool TargetSpawnMappedPointCubesOnce = false;

    [MenuItem("Tools/Map/设置国外国家点位缩放为10")]
    public static void ApplyForeignVehiclePointScale()
    {
        Transform worldMapRoot = FindNamedTransformInLoadedScenes(WorldMapRootName);
        if (worldMapRoot == null)
        {
            EditorUtility.DisplayDialog(
                "设置失败",
                $"当前已加载场景中未找到「{WorldMapRootName}」。",
                "确定");
            return;
        }

        PlateMapVehiclePointController[] controllers =
            worldMapRoot.GetComponentsInChildren<PlateMapVehiclePointController>(true);

        if (controllers == null || controllers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "设置完成",
                $"在「{WorldMapRootName}」下未找到 {nameof(PlateMapVehiclePointController)}。",
                "确定");
            return;
        }

        int changed = 0;
        Undo.SetCurrentGroupName("设置国外国家点位参数");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlateMapVehiclePointController controller = controllers[i];
            if (controller == null || controller.GetComponent<PlateMapDisplayModule>() == null)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(controller);
            SerializedProperty pointLocalScale = serializedObject.FindProperty("_pointLocalScale");
            SerializedProperty spawnMappedPointCubesOnce =
                serializedObject.FindProperty("_spawnMappedPointCubesOnce");
            if (pointLocalScale == null || spawnMappedPointCubesOnce == null)
            {
                continue;
            }

            bool scaleMatched = pointLocalScale.vector3Value == TargetPointLocalScale;
            bool cubeToggleMatched = spawnMappedPointCubesOnce.boolValue == TargetSpawnMappedPointCubesOnce;
            if (scaleMatched && cubeToggleMatched)
            {
                continue;
            }

            Undo.RecordObject(controller, "Set Foreign Vehicle Point Params");
            pointLocalScale.vector3Value = TargetPointLocalScale;
            spawnMappedPointCubesOnce.boolValue = TargetSpawnMappedPointCubesOnce;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkDirtyScenesContaining(worldMapRoot);

        string message =
            $"世界地图下找到控制器：{controllers.Length}\n" +
            $"目标缩放：{TargetPointLocalScale}\n" +
            $"调试映射 Cube：{TargetSpawnMappedPointCubesOnce}\n" +
            $"已修改国家节点组件数：{changed}";
        Debug.Log($"[WorldMapForeignVehiclePointScale] {message.Replace("\n", " | ")}");
        EditorUtility.DisplayDialog("设置完成", message, "确定");
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
}
#endif
