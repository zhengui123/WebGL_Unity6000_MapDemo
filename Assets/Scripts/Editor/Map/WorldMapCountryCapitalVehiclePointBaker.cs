#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将「世界地图」下各国 PlateMapVehiclePointController 的测试点设为该国首都经纬度（1 点），
/// 并勾选 _useDefaultVehiclePointsTestData。跳过大板块根与国内省级节点。
/// 首都坐标来自同目录 CountryCapitalCoordinates.json（Natural Earth Admin-0 capital）。
/// </summary>
public static class WorldMapCountryCapitalVehiclePointBaker
{
    private const string WorldMapRootName = "世界地图";
    private const string CapitalJsonAssetPath =
        "Assets/Scripts/Editor/Map/CountryCapitalCoordinates.json";
    private const string MenuPath = "Tools/Map/Bake WorldMap Country Capital Vehicle Points";

    [Serializable]
    private class CapitalEntry
    {
        public string ccn3;
        public string name;
        public string country;
        public double lon;
        public double lat;
        public string a2;
        public string a3;
    }

    [Serializable]
    private class CapitalEntryList
    {
        public CapitalEntry[] items;
    }

    [MenuItem(MenuPath)]
    public static void BakeCapitalsOntoWorldMapCountries()
    {
        Debug.Log("[WorldMapCountryCapitalVehiclePointBaker] 开始写入…");
        Transform worldMapRoot = FindNamedTransformInLoadedScenes(WorldMapRootName);
        if (worldMapRoot == null)
        {
            Debug.LogError($"[WorldMapCountryCapitalVehiclePointBaker] 未找到「{WorldMapRootName}」。");
            return;
        }

        if (!TryLoadCapitalMap(out Dictionary<string, CapitalEntry> capitalByCode, out string loadError))
        {
            Debug.LogError($"[WorldMapCountryCapitalVehiclePointBaker] {loadError}");
            return;
        }

        PlateMapVehiclePointController[] controllers =
            worldMapRoot.GetComponentsInChildren<PlateMapVehiclePointController>(true);
        if (controllers == null || controllers.Length == 0)
        {
            Debug.LogWarning(
                $"[WorldMapCountryCapitalVehiclePointBaker] 「{WorldMapRootName}」下无 {nameof(PlateMapVehiclePointController)}。");
            return;
        }

        int changed = 0;
        int skippedNonCountry = 0;
        int skippedNoCapital = 0;
        int skippedNoModule = 0;
        var missingCodes = new List<string>(16);

        Undo.SetCurrentGroupName("世界地图各国首都测试点");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < controllers.Length; i++)
        {
            PlateMapVehiclePointController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            if (controller.GetComponent<PlateMapDisplayModule>() == null)
            {
                skippedNoModule++;
                continue;
            }

            PlateMapGeoConverter geo = controller.GetComponent<PlateMapGeoConverter>();
            if (geo == null)
            {
                skippedNonCountry++;
                continue;
            }

            if (!TryNormalizeCountrySoc(geo.ProvinceCode, out string soc))
            {
                skippedNonCountry++;
                continue;
            }

            if (!capitalByCode.TryGetValue(soc, out CapitalEntry capital))
            {
                skippedNoCapital++;
                missingCodes.Add($"{soc} ({controller.gameObject.name})");
                continue;
            }

            if (!ApplyCapitalPoint(controller, soc, capital))
            {
                continue;
            }

            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        MarkDirtyScenesContaining(worldMapRoot);

        if (changed > 0)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        string missingPreview = missingCodes.Count == 0
            ? "无"
            : string.Join("\n", missingCodes.GetRange(0, Math.Min(12, missingCodes.Count)));
        if (missingCodes.Count > 12)
        {
            missingPreview += $"\n… 另有 {missingCodes.Count - 12} 条";
        }

        string message =
            $"控制器总数：{controllers.Length}\n" +
            $"已写入首都点：{changed}\n" +
            $"跳过（无 DisplayModule）：{skippedNoModule}\n" +
            $"跳过（非国家 SOC）：{skippedNonCountry}\n" +
            $"跳过（无首都坐标）：{skippedNoCapital}\n" +
            $"缺坐标样例：\n{missingPreview}";

        Debug.Log($"[WorldMapCountryCapitalVehiclePointBaker] {message.Replace("\n", " | ")}");
    }

    private static bool ApplyCapitalPoint(
        PlateMapVehiclePointController controller,
        string soc,
        CapitalEntry capital)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        SerializedProperty useTestData = serializedObject.FindProperty("_useDefaultVehiclePointsTestData");
        SerializedProperty vehiclePoints = serializedObject.FindProperty("_vehiclePoints");
        if (useTestData == null || vehiclePoints == null)
        {
            return false;
        }

        Undo.RecordObject(controller, "Bake Capital Vehicle Point");
        useTestData.boolValue = true;
        vehiclePoints.arraySize = 1;
        SerializedProperty element = vehiclePoints.GetArrayElementAtIndex(0);
        element.FindPropertyRelative("vehicleId").stringValue = $"CAP-{soc}";
        element.FindPropertyRelative("longitude").doubleValue = capital.lon;
        element.FindPropertyRelative("latitude").doubleValue = capital.lat;
        element.FindPropertyRelative("alertValue").floatValue = 0.5f;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        return true;
    }

    /// <summary>
    /// 仅接受国外国家 secondClassCode（1～3 位数字，归一为 3 位）；
    /// 拒绝大板块 firstClassCode、国内 6 位 adcode、"0" 等。
    /// </summary>
    private static bool TryNormalizeCountrySoc(string raw, out string soc)
    {
        soc = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string trimmed = raw.Trim();
        if (trimmed == "0")
        {
            return false;
        }

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (!char.IsDigit(trimmed[i]))
            {
                return false;
            }
        }

        // 国内省级 adcode：6 位（如 370000）
        if (trimmed.Length >= 6)
        {
            return false;
        }

        if (trimmed.Length > 3)
        {
            return false;
        }

        soc = trimmed.PadLeft(3, '0');
        return true;
    }

    private static bool TryLoadCapitalMap(out Dictionary<string, CapitalEntry> map, out string error)
    {
        map = new Dictionary<string, CapitalEntry>(256);
        error = null;

        string absolutePath = Path.GetFullPath(CapitalJsonAssetPath);
        if (!File.Exists(absolutePath))
        {
            error = $"未找到首都坐标文件：{CapitalJsonAssetPath}";
            return false;
        }

        string json = File.ReadAllText(absolutePath);
        // Unity JsonUtility 不支持顶层数组，包一层
        string wrapped = "{\"items\":" + json + "}";
        CapitalEntryList list = JsonUtility.FromJson<CapitalEntryList>(wrapped);
        if (list?.items == null || list.items.Length == 0)
        {
            error = $"首都坐标文件为空或解析失败：{CapitalJsonAssetPath}";
            return false;
        }

        for (int i = 0; i < list.items.Length; i++)
        {
            CapitalEntry entry = list.items[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ccn3))
            {
                continue;
            }

            string key = entry.ccn3.Trim().PadLeft(3, '0');
            map[key] = entry;
        }

        if (map.Count == 0)
        {
            error = "首都坐标字典为空。";
            return false;
        }

        return true;
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
