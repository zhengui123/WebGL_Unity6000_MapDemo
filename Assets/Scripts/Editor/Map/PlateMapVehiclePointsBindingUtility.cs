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
    private const string InstancedMaterialPrimaryPath = "Assets/Materials/CarPoint/Materials/M_CarPointGlowInstanced.mat";
    private const string InstancedMaterialAssetPath = "Assets/Resources/CarPoint/M_CarPointGlowInstanced.mat";
    private const string InstancedMaterialFallbackPath = "Assets/CarPoint/Materials/M_CarPointGlowInstanced.mat";

    /// <summary>Controller 颜色标定默认值（#FFFBA0 / #F5FF00）。</summary>
    public static readonly Color DefaultColorAtDataMin = new Color(1f, 251f / 255f, 160f / 255f, 1f);
    public static readonly Color DefaultColorAtDataMax = new Color(245f / 255f, 1f, 0f, 1f);

    public enum RowIssue
    {
        None = 0,
        NotPersisted,
        MissingInScene,
        ProvinceMismatch,
        MissingComponents,
        PendingBind,
        ProvinceUnresolved
    }

    public sealed class BindingRow
    {
        public GameObject Target;
        public string ProvinceCode = PlateMapBoundaryDatabase.NationalProvinceCode;
        public RowIssue Issue = RowIssue.None;
        public string IssueDetail;
        public bool IsManualAdd;
        public bool FromPersistenceOnly;
        public bool IsSelected;
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

    /// <summary>
    /// 按板块对象名关联省份：全等 → 规范化全等 → 唯一前缀匹配。
    /// 多命中或零命中返回 false。
    /// </summary>
    public static bool TryResolveProvinceByObjectName(
        string objectName,
        IReadOnlyList<PlateMapBoundaryData> options,
        out PlateMapBoundaryData matched)
    {
        matched = null;
        if (string.IsNullOrWhiteSpace(objectName) || options == null || options.Count == 0)
        {
            return false;
        }

        string trimmedName = objectName.Trim();
        string normalizedName = NormalizeAdminRegionName(trimmedName);

        for (int i = 0; i < options.Count; i++)
        {
            PlateMapBoundaryData item = options[i];
            if (item == null || string.IsNullOrWhiteSpace(item.provinceName))
            {
                continue;
            }

            if (string.Equals(item.provinceName, trimmedName, System.StringComparison.Ordinal))
            {
                matched = item;
                return true;
            }
        }

        for (int i = 0; i < options.Count; i++)
        {
            PlateMapBoundaryData item = options[i];
            if (item == null || string.IsNullOrWhiteSpace(item.provinceName))
            {
                continue;
            }

            if (string.Equals(NormalizeAdminRegionName(item.provinceName), normalizedName, System.StringComparison.Ordinal))
            {
                matched = item;
                return true;
            }
        }

        List<PlateMapBoundaryData> prefixMatches = new List<PlateMapBoundaryData>();
        for (int i = 0; i < options.Count; i++)
        {
            PlateMapBoundaryData item = options[i];
            if (item == null || string.IsNullOrWhiteSpace(item.provinceName))
            {
                continue;
            }

            string provinceName = item.provinceName.Trim();
            string normalizedProvince = NormalizeAdminRegionName(provinceName);
            bool isPrefix =
                provinceName.StartsWith(trimmedName, System.StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(normalizedName) &&
                 normalizedProvince.StartsWith(normalizedName, System.StringComparison.Ordinal));
            if (isPrefix)
            {
                prefixMatches.Add(item);
            }
        }

        if (prefixMatches.Count == 1)
        {
            matched = prefixMatches[0];
            return true;
        }

        return false;
    }

    /// <summary>根据对象名填充省份；失败时标记 ProvinceUnresolved。</summary>
    public static void ApplyProvinceFromObjectName(BindingRow row, IReadOnlyList<PlateMapBoundaryData> options)
    {
        if (row == null || row.Target == null)
        {
            return;
        }

        PlateMapGeoConverter existingGeo = row.Target.GetComponent<PlateMapGeoConverter>();
        if (existingGeo != null && !string.IsNullOrWhiteSpace(existingGeo.ProvinceCode))
        {
            row.ProvinceCode = existingGeo.ProvinceCode;
            return;
        }

        if (TryResolveProvinceByObjectName(row.Target.name, options, out PlateMapBoundaryData matched))
        {
            row.ProvinceCode = matched.provinceCode;
            if (row.Issue == RowIssue.ProvinceUnresolved || row.Issue == RowIssue.PendingBind)
            {
                row.Issue = HasFullBinding(row.Target) ? RowIssue.None : RowIssue.PendingBind;
                row.IssueDetail = HasFullBinding(row.Target)
                    ? null
                    : $"已匹配省份：{matched.provinceName}（{matched.provinceCode}），点击「绑定」写入组件。";
            }
            else if (row.Issue == RowIssue.MissingComponents)
            {
                row.IssueDetail =
                    $"已匹配省份：{matched.provinceName}（{matched.provinceCode}）。缺少组件，请点击「绑定」。";
            }

            return;
        }

        row.Issue = RowIssue.ProvinceUnresolved;
        row.IssueDetail = $"无法根据对象名「{row.Target.name}」唯一匹配省份，请手动选择。";
    }

    /// <summary>去掉省/市/自治区等行政后缀，便于「重庆」匹配「重庆市」。</summary>
    public static string NormalizeAdminRegionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string result = name.Trim();
        string[] suffixes =
        {
            "特别行政区",
            "壮族自治区",
            "回族自治区",
            "维吾尔自治区",
            "自治区",
            "省",
            "市"
        };

        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (result.EndsWith(suffix, System.StringComparison.Ordinal) && result.Length > suffix.Length)
            {
                result = result.Substring(0, result.Length - suffix.Length);
                break;
            }
        }

        return result;
    }

    public static string BuildRowKeyPublic(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        return BuildRowKey(GetSceneAssetPath(target), GetHierarchyPath(target));
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

    /// <summary>
    /// 强制覆盖颜色标定默认值；材质为空时挂载 Instanced 材质；
    /// 并将 PlateMapGeoConverter._autoBindWestEastByLocalX 设为 false。
    /// </summary>
    public static bool ApplyDefaultVisualData(GameObject target, bool forceOverwriteColors = true)
    {
        if (target == null)
        {
            return false;
        }

        PlateMapVehiclePointController controller = target.GetComponent<PlateMapVehiclePointController>();
        PlateMapVehiclePointInstancedRenderer renderer = target.GetComponent<PlateMapVehiclePointInstancedRenderer>();
        PlateMapGeoConverter geoConverter = target.GetComponent<PlateMapGeoConverter>();
        if (controller == null && renderer == null && geoConverter == null)
        {
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(target, "更新车辆点默认数据");

        if (controller != null && forceOverwriteColors)
        {
            SerializedObject controllerSo = new SerializedObject(controller);
            SerializedProperty minProp = controllerSo.FindProperty("_colorAtDataMin");
            SerializedProperty maxProp = controllerSo.FindProperty("_colorAtDataMax");
            if (minProp != null)
            {
                minProp.colorValue = DefaultColorAtDataMin;
            }

            if (maxProp != null)
            {
                maxProp.colorValue = DefaultColorAtDataMax;
            }

            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        if (renderer != null)
        {
            AssignDefaultInstancedMaterial(renderer);
            EditorUtility.SetDirty(renderer);
        }

        if (geoConverter != null)
        {
            SerializedObject geoSo = new SerializedObject(geoConverter);
            SerializedProperty autoBindProp = geoSo.FindProperty("_autoBindWestEastByLocalX");
            if (autoBindProp != null)
            {
                autoBindProp.boolValue = false;
                geoSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(geoConverter);
            }
        }

        EditorSceneManager.MarkSceneDirty(target.scene);
        return true;
    }

    public static Material LoadDefaultInstancedMaterial()
    {
        Material material = Resources.Load<Material>(InstancedMaterialResourcePath);
        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialPrimaryPath);
        }

        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialAssetPath);
        }

        if (material == null)
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialFallbackPath);
        }

        return material;
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

        Material material = LoadDefaultInstancedMaterial();
        if (material != null)
        {
            materialProperty.objectReferenceValue = material;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
