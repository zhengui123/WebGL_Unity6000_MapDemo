#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 板块三组件绑定、场景路径解析与持久化条目同步。
/// </summary>
public static class PlateMapVehiclePointsBindingUtility
{
    private const string InstancedMaterialResourcePath = "CarPoint/M_CarPointGlowInstanced";
    private const string InstancedMaterialAssetPath = "Assets/Resources/CarPoint/M_CarPointGlowInstanced.mat";
    private const string InstancedMaterialFallbackPath = "Assets/CarPoint/Materials/M_CarPointGlowInstanced.mat";

    public enum RowIssue
    {
        None = 0,
        NotPersisted,
        MissingInScene,
        ProvinceMismatch,
        MissingComponents,
        PendingBind
    }

    public sealed class BindingRow
    {
        public GameObject Target;
        public string ProvinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;
        public RowIssue Issue = RowIssue.None;
        public string IssueDetail;
        public bool IsManualAdd;
        public bool FromPersistenceOnly;
        public PlateMapVehiclePointsBindingStore.Entry PersistedEntry;
    }

    public static string GetHierarchyPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        Transform current = gameObject.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }

    public static string GetSceneAssetPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        Scene scene = gameObject.scene;
        return scene.IsValid() ? scene.path : string.Empty;
    }

    public static GameObject FindInLoadedScenes(string sceneAssetPath, string hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return null;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(sceneAssetPath) &&
                !string.IsNullOrEmpty(scene.path) &&
                scene.path != sceneAssetPath)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root.name == hierarchyPath)
                {
                    return root;
                }

                Transform child = root.transform.Find(hierarchyPath);
                if (child != null)
                {
                    return child.gameObject;
                }

                if (hierarchyPath.StartsWith(root.name + "/"))
                {
                    string relative = hierarchyPath.Substring(root.name.Length + 1);
                    child = root.transform.Find(relative);
                    if (child != null)
                    {
                        return child.gameObject;
                    }
                }
            }
        }

        return null;
    }

    public static List<PlateMapBoundaryData> BuildProvinceOptions()
    {
        return PlateMapBoundaryDatabase.All
            .OrderBy(item => item.provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode ? 0 : 1)
            .ThenBy(item => item.provinceName)
            .ToList();
    }

    public static string FormatProvinceLabel(PlateMapBoundaryData data)
    {
        if (data == null)
        {
            return "(无效)";
        }

        return $"{data.provinceCode}  {data.provinceName}";
    }

    public static int FindProvinceIndex(IReadOnlyList<PlateMapBoundaryData> options, string provinceCode)
    {
        if (options == null || string.IsNullOrWhiteSpace(provinceCode))
        {
            return 0;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].provinceCode == provinceCode)
            {
                return i;
            }
        }

        return 0;
    }

    public static List<BindingRow> BuildRows(PlateMapVehiclePointsBindingStore store)
    {
        List<BindingRow> rows = new List<BindingRow>(16);
        Dictionary<string, BindingRow> keyed = new Dictionary<string, BindingRow>();

        PlateMapGeoConverter[] converters = Object.FindObjectsByType<PlateMapGeoConverter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < converters.Length; i++)
        {
            PlateMapGeoConverter converter = converters[i];
            if (converter == null)
            {
                continue;
            }

            GameObject target = converter.gameObject;
            string key = BuildRowKey(GetSceneAssetPath(target), GetHierarchyPath(target));
            BindingRow row = new BindingRow
            {
                Target = target,
                ProvinceCode = converter.ProvinceCode,
                Issue = RowIssue.None
            };
            EvaluateComponentState(row);
            keyed[key] = row;
            rows.Add(row);
        }

        IReadOnlyList<PlateMapVehiclePointsBindingStore.Entry> persisted = store != null ? store.Entries : null;
        if (persisted != null)
        {
            for (int i = 0; i < persisted.Count; i++)
            {
                PlateMapVehiclePointsBindingStore.Entry entry = persisted[i];
                if (entry == null)
                {
                    continue;
                }

                string key = BuildRowKey(entry.sceneAssetPath, entry.hierarchyPath);
                if (keyed.TryGetValue(key, out BindingRow existing))
                {
                    existing.PersistedEntry = entry;
                    if (!string.Equals(existing.ProvinceCode, entry.provinceCode, System.StringComparison.Ordinal))
                    {
                        existing.Issue = RowIssue.ProvinceMismatch;
                        existing.IssueDetail =
                            $"持久化 code={entry.provinceCode}，场景实际 code={existing.ProvinceCode}";
                    }

                    continue;
                }

                GameObject resolved = FindInLoadedScenes(entry.sceneAssetPath, entry.hierarchyPath);
                BindingRow orphanRow = new BindingRow
                {
                    Target = resolved,
                    ProvinceCode = entry.provinceCode,
                    PersistedEntry = entry,
                    FromPersistenceOnly = true
                };

                if (resolved == null)
                {
                    orphanRow.Issue = RowIssue.MissingInScene;
                    orphanRow.IssueDetail =
                        $"持久化对象未找到：{entry.sceneAssetPath} / {entry.hierarchyPath}";
                }
                else
                {
                    EvaluateComponentState(orphanRow);
                    PlateMapGeoConverter geo = resolved.GetComponent<PlateMapGeoConverter>();
                    if (geo != null && geo.ProvinceCode != entry.provinceCode)
                    {
                        orphanRow.Issue = RowIssue.ProvinceMismatch;
                        orphanRow.IssueDetail =
                            $"持久化 code={entry.provinceCode}，场景实际 code={geo.ProvinceCode}";
                        orphanRow.ProvinceCode = geo.ProvinceCode;
                    }
                }

                keyed[key] = orphanRow;
                rows.Add(orphanRow);
            }
        }

        for (int i = 0; i < rows.Count; i++)
        {
            BindingRow row = rows[i];
            if (row.PersistedEntry == null && row.Target != null && row.Issue == RowIssue.None)
            {
                row.Issue = RowIssue.NotPersisted;
                row.IssueDetail = "场景中存在，但尚未写入持久化配置。";
            }
        }

        return rows;
    }

    public static bool ApplyBinding(BindingRow row)
    {
        if (row == null || row.Target == null)
        {
            return false;
        }

        GameObject target = row.Target;
        Undo.RegisterFullObjectHierarchyUndo(target, "绑定板块车辆点组件");

        PlateMapGeoConverter geoConverter = target.GetComponent<PlateMapGeoConverter>();
        if (geoConverter == null)
        {
            geoConverter = Undo.AddComponent<PlateMapGeoConverter>(target);
        }

        PlateMapVehiclePointController controller = target.GetComponent<PlateMapVehiclePointController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<PlateMapVehiclePointController>(target);
        }

        PlateMapVehiclePointInstancedRenderer renderer = target.GetComponent<PlateMapVehiclePointInstancedRenderer>();
        if (renderer == null)
        {
            renderer = Undo.AddComponent<PlateMapVehiclePointInstancedRenderer>(target);
        }

        Transform mapRoot = target.transform;
        SetTransformReference(geoConverter, "_mapRoot", mapRoot);
        SetTransformReference(controller, "_mapRoot", mapRoot);
        SetComponentReference(controller, "_instancedRenderer", renderer);
        SetTransformReference(renderer, "_mapRoot", mapRoot);
        AssignDefaultInstancedMaterial(renderer);

        SetProvinceCode(geoConverter, row.ProvinceCode);
        geoConverter.Rebuild();

        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(geoConverter);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(renderer);
        EditorSceneManager.MarkSceneDirty(target.scene);

        row.IsManualAdd = false;
        row.FromPersistenceOnly = false;
        EvaluateComponentState(row);
        return true;
    }

    public static void SyncStoreFromRows(PlateMapVehiclePointsBindingStore store, IReadOnlyList<BindingRow> rows)
    {
        if (store == null || rows == null)
        {
            return;
        }

        List<PlateMapVehiclePointsBindingStore.Entry> entries = new List<PlateMapVehiclePointsBindingStore.Entry>();
        HashSet<string> keys = new HashSet<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            BindingRow row = rows[i];
            if (row == null || row.Target == null)
            {
                continue;
            }

            if (!HasFullBinding(row.Target))
            {
                continue;
            }

            string scenePath = GetSceneAssetPath(row.Target);
            string hierarchyPath = GetHierarchyPath(row.Target);
            string key = BuildRowKey(scenePath, hierarchyPath);
            if (!keys.Add(key))
            {
                continue;
            }

            PlateMapGeoConverter geo = row.Target.GetComponent<PlateMapGeoConverter>();
            entries.Add(new PlateMapVehiclePointsBindingStore.Entry
            {
                sceneAssetPath = scenePath,
                hierarchyPath = hierarchyPath,
                objectName = row.Target.name,
                provinceCode = geo != null ? geo.ProvinceCode : row.ProvinceCode
            });
        }

        store.SetEntries(entries);
        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
    }

    public static bool HasFullBinding(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return target.GetComponent<PlateMapGeoConverter>() != null &&
               target.GetComponent<PlateMapVehiclePointController>() != null &&
               target.GetComponent<PlateMapVehiclePointInstancedRenderer>() != null;
    }

    public static void EvaluateComponentState(BindingRow row)
    {
        if (row == null)
        {
            return;
        }

        if (row.Target == null)
        {
            if (row.Issue != RowIssue.MissingInScene)
            {
                row.Issue = RowIssue.PendingBind;
                row.IssueDetail = "请先指定板块对象。";
            }

            return;
        }

        if (!HasFullBinding(row.Target))
        {
            row.Issue = RowIssue.MissingComponents;
            row.IssueDetail = "缺少 PlateMapGeoConverter / Controller / InstancedRenderer 之一，请点击「绑定」。";
            return;
        }

        PlateMapGeoConverter geo = row.Target.GetComponent<PlateMapGeoConverter>();
        if (geo != null)
        {
            row.ProvinceCode = geo.ProvinceCode;
        }

        if (row.Issue == RowIssue.MissingComponents || row.Issue == RowIssue.PendingBind)
        {
            row.Issue = RowIssue.None;
            row.IssueDetail = null;
        }
    }

    private static string BuildRowKey(string sceneAssetPath, string hierarchyPath)
    {
        return (sceneAssetPath ?? string.Empty) + "|" + (hierarchyPath ?? string.Empty);
    }

    private static void SetProvinceCode(PlateMapGeoConverter converter, string provinceCode)
    {
        SerializedObject serializedObject = new SerializedObject(converter);
        SerializedProperty property = serializedObject.FindProperty("_provinceCode");
        if (property != null)
        {
            property.stringValue = provinceCode ?? string.Empty;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetTransformReference(Object component, string propertyName, Transform value)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetComponentReference(Object component, string propertyName, Component value)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AssignDefaultInstancedMaterial(PlateMapVehiclePointInstancedRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(renderer);
        SerializedProperty materialProperty = serializedObject.FindProperty("_material");
        if (materialProperty == null || materialProperty.objectReferenceValue != null)
        {
            return;
        }

        Material material = Resources.Load<Material>(InstancedMaterialResourcePath);
        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialAssetPath);
        }

        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialFallbackPath);
        }

        if (material != null)
        {
            materialProperty.objectReferenceValue = material;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
