#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 板块车辆点绑定控制面板：扫描场景、批量从 Hierarchy 加入、按对象名匹配省份、一键绑定三组件。
/// 持久化：Assets/Scripts/Editor/Map/PlateMapVehiclePointsBindingStore.asset
/// </summary>
public class PlateMapVehiclePointsBindingEditorWindow : EditorWindow
{
    private const float RowHeight = 78f;

    private PlateMapVehiclePointsBindingStore _store;
    private List<PlateMapBoundaryData> _provinceOptions;
    private List<PlateMapVehiclePointsBindingUtility.BindingRow> _rows = new List<PlateMapVehiclePointsBindingUtility.BindingRow>();
    private Vector2 _scrollPosition;
    private string[] _provinceLabels = System.Array.Empty<string>();
    private int[] _provincePopupIndices;

    [MenuItem("Tools/地图/板块车辆点绑定面板")]
    public static void OpenWindow()
    {
        PlateMapVehiclePointsBindingEditorWindow window = GetWindow<PlateMapVehiclePointsBindingEditorWindow>();
        window.titleContent = new GUIContent("板块车辆点绑定");
        window.minSize = new Vector2(720f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        _store = PlateMapVehiclePointsBindingStore.LoadOrCreate();
        RefreshProvinceOptions();
        RefreshRows();
    }

    private void OnFocus()
    {
        RefreshRows();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4f);
        DrawSummary();
        EditorGUILayout.Space(6f);
        DrawHeader();
        DrawRows();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新场景", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            RefreshRows();
        }

        if (GUILayout.Button("保存持久化", EditorStyles.toolbarButton, GUILayout.Width(88f)))
        {
            SavePersistence();
        }

        if (GUILayout.Button("添加绑定行", EditorStyles.toolbarButton, GUILayout.Width(88f)))
        {
            AddManualRow();
        }

        if (GUILayout.Button("从多选加入", EditorStyles.toolbarButton, GUILayout.Width(88f)))
        {
            AddRowsFromHierarchySelection();
        }

        if (GUILayout.Button("批量绑定", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            BatchBindPendingRows();
        }

        if (GUILayout.Button("更新默认数据", EditorStyles.toolbarButton, GUILayout.Width(96f)))
        {
            UpdateDefaultVisualDataOnSelected();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("全选", EditorStyles.toolbarButton, GUILayout.Width(48f)))
        {
            SetAllRowsSelected(true);
        }

        if (GUILayout.Button("全不选", EditorStyles.toolbarButton, GUILayout.Width(56f)))
        {
            SetAllRowsSelected(false);
        }

        if (GUILayout.Button("选中 Store", EditorStyles.toolbarButton, GUILayout.Width(88f)))
        {
            Selection.activeObject = _store;
            EditorGUIUtility.PingObject(_store);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        int issueCount = CountIssueRows();
        EditorGUILayout.LabelField("持久化文件", PlateMapVehiclePointsBindingStore.AssetPath, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"已扫描 {_rows.Count} 行 | 已勾选 {CountSelectedRows()} | 含 GeoConverter 的板块 {CountBoundRows()} 个 | 异常 {issueCount} 个",
            EditorStyles.boldLabel);

        if (issueCount > 0)
        {
            EditorGUILayout.HelpBox(
                "存在与持久化不一致或未绑定的条目（红色/黄色标注）。请修正后点击「保存持久化」。",
                MessageType.Warning);
        }
    }

    private void DrawHeader()
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 20f);
        float x = headerRect.x;
        float width = headerRect.width;
        const float checkWidth = 28f;
        float objectWidth = width * 0.30f;
        float provinceWidth = width * 0.22f;
        float statusWidth = width * 0.26f;
        float actionWidth = width * 0.14f;

        bool allSelected = _rows.Count > 0 && CountSelectedRows() == _rows.Count;
        bool newAllSelected = EditorGUI.Toggle(new Rect(x, headerRect.y, checkWidth, 18f), allSelected);
        if (newAllSelected != allSelected)
        {
            SetAllRowsSelected(newAllSelected);
        }

        x += checkWidth;
        EditorGUI.LabelField(new Rect(x, headerRect.y, objectWidth, 18f), "板块对象", EditorStyles.miniBoldLabel);
        x += objectWidth;
        EditorGUI.LabelField(new Rect(x, headerRect.y, provinceWidth, 18f), "省份", EditorStyles.miniBoldLabel);
        x += provinceWidth;
        EditorGUI.LabelField(new Rect(x, headerRect.y, statusWidth, 18f), "状态", EditorStyles.miniBoldLabel);
        x += statusWidth;
        EditorGUI.LabelField(new Rect(x, headerRect.y, actionWidth, 18f), "操作", EditorStyles.miniBoldLabel);
    }

    private void DrawRows()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EnsurePopupCacheSize();

        for (int i = 0; i < _rows.Count; i++)
        {
            DrawRow(i, _rows[i]);
        }

        if (_rows.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "当前无绑定行。可「添加绑定行」，或在 Hierarchy 多选省份物体后点「从多选加入」。",
                MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRow(int index, PlateMapVehiclePointsBindingUtility.BindingRow row)
    {
        if (row == null)
        {
            return;
        }

        Color background = GetRowBackground(row.Issue);
        Rect rowRect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(new Rect(rowRect.x - 2f, rowRect.y, position.width - 24f, RowHeight), background);
        EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

        row.IsSelected = EditorGUILayout.Toggle(row.IsSelected, GUILayout.Width(22f));

        EditorGUI.BeginChangeCheck();
        GameObject newTarget = (GameObject)EditorGUILayout.ObjectField(
            row.Target,
            typeof(GameObject),
            true,
            GUILayout.Width(position.width * 0.30f - 12f));
        if (EditorGUI.EndChangeCheck())
        {
            row.Target = newTarget;
            PlateMapVehiclePointsBindingUtility.EvaluateComponentState(row);
            if (row.Target != null)
            {
                PlateMapVehiclePointsBindingUtility.ApplyProvinceFromObjectName(row, _provinceOptions);
                _provincePopupIndices[index] = PlateMapVehiclePointsBindingUtility.FindProvinceIndex(
                    _provinceOptions,
                    row.ProvinceCode);
            }
        }

        _provincePopupIndices[index] = EditorGUILayout.Popup(
            _provincePopupIndices[index],
            _provinceLabels,
            GUILayout.Width(position.width * 0.22f - 12f));
        if (_provinceOptions != null &&
            _provincePopupIndices[index] >= 0 &&
            _provincePopupIndices[index] < _provinceOptions.Count)
        {
            row.ProvinceCode = _provinceOptions[_provincePopupIndices[index]].provinceCode;
        }

        DrawStatusColumn(row, position.width * 0.26f - 12f);

        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.14f - 12f));
        if (GUILayout.Button("绑定", GUILayout.Height(24f)))
        {
            if (row.Target == null)
            {
                EditorUtility.DisplayDialog("绑定失败", "请先指定板块对象。", "确定");
            }
            else if (PlateMapVehiclePointsBindingUtility.ApplyBinding(row))
            {
                RefreshRows();
            }
        }

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = row.Target != null;
        if (GUILayout.Button("定位", GUILayout.Height(22f)))
        {
            Selection.activeGameObject = row.Target;
            EditorGUIUtility.PingObject(row.Target);
        }

        GUI.enabled = row.IsManualAdd;
        if (GUILayout.Button("删", GUILayout.Height(22f)))
        {
            _rows.RemoveAt(index);
            Repaint();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private static void DrawStatusColumn(PlateMapVehiclePointsBindingUtility.BindingRow row, float width)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        string title = GetIssueTitle(row.Issue);
        Color previous = GUI.contentColor;
        GUI.contentColor = GetIssueTextColor(row.Issue);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        GUI.contentColor = previous;

        if (!string.IsNullOrWhiteSpace(row.IssueDetail))
        {
            EditorGUILayout.LabelField(row.IssueDetail, EditorStyles.wordWrappedMiniLabel);
        }
        else if (row.Target != null)
        {
            string scenePath = PlateMapVehiclePointsBindingUtility.GetSceneAssetPath(row.Target);
            string hierarchy = PlateMapVehiclePointsBindingUtility.GetHierarchyPath(row.Target);
            EditorGUILayout.LabelField($"{scenePath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(hierarchy, EditorStyles.miniLabel);
        }
        else if (row.PersistedEntry != null)
        {
            EditorGUILayout.LabelField(row.PersistedEntry.sceneAssetPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(row.PersistedEntry.hierarchyPath, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void RefreshProvinceOptions()
    {
        _provinceOptions = PlateMapVehiclePointsBindingUtility.BuildProvinceOptions();
        _provinceLabels = new string[_provinceOptions.Count];
        for (int i = 0; i < _provinceOptions.Count; i++)
        {
            _provinceLabels[i] = PlateMapVehiclePointsBindingUtility.FormatProvinceLabel(_provinceOptions[i]);
        }
    }

    private void RefreshRows()
    {
        HashSet<string> previouslySelected = new HashSet<string>();
        List<PlateMapVehiclePointsBindingUtility.BindingRow> manualRows =
            new List<PlateMapVehiclePointsBindingUtility.BindingRow>();

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow existing = _rows[i];
            if (existing == null)
            {
                continue;
            }

            if (existing.IsSelected && existing.Target != null)
            {
                previouslySelected.Add(PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(existing.Target));
            }

            if (existing.IsManualAdd)
            {
                manualRows.Add(existing);
            }
        }

        _rows = PlateMapVehiclePointsBindingUtility.BuildRows(_store);
        _rows.AddRange(manualRows);

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row?.Target == null)
            {
                continue;
            }

            row.IsSelected = previouslySelected.Contains(
                PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(row.Target));
        }

        EnsurePopupCacheSize();
        Repaint();
    }

    private void EnsurePopupCacheSize()
    {
        if (_provincePopupIndices == null || _provincePopupIndices.Length != _rows.Count)
        {
            _provincePopupIndices = new int[_rows.Count];
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            _provincePopupIndices[i] = PlateMapVehiclePointsBindingUtility.FindProvinceIndex(
                _provinceOptions,
                _rows[i].ProvinceCode);
        }
    }

    private void AddManualRow()
    {
        _rows.Add(new PlateMapVehiclePointsBindingUtility.BindingRow
        {
            IsManualAdd = true,
            Issue = PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind,
            IssueDetail = "选择对象与省份后点击「绑定」。",
            ProvinceCode = _provinceOptions != null && _provinceOptions.Count > 0
                ? _provinceOptions[0].provinceCode
                : PlateMapBoundaryDatabase.NationalProvinceCode
        });
        EnsurePopupCacheSize();
    }

    private void AddRowsFromHierarchySelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("从多选加入", "请先在 Hierarchy 中多选板块对象（如 山东、重庆）。", "确定");
            return;
        }

        HashSet<string> existingKeys = new HashSet<string>();
        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow existing = _rows[i];
            if (existing?.Target == null)
            {
                continue;
            }

            existingKeys.Add(PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(existing.Target));
        }

        int added = 0;
        int skipped = 0;
        int unresolved = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject target = selected[i];
            if (target == null)
            {
                continue;
            }

            string key = PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(target);
            if (!existingKeys.Add(key))
            {
                skipped++;
                continue;
            }

            var row = new PlateMapVehiclePointsBindingUtility.BindingRow
            {
                Target = target,
                IsManualAdd = true,
                IsSelected = true,
                Issue = PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind,
                IssueDetail = "已加入列表，确认省份后点击「绑定」或「批量绑定」。"
            };

            PlateMapVehiclePointsBindingUtility.EvaluateComponentState(row);
            PlateMapVehiclePointsBindingUtility.ApplyProvinceFromObjectName(row, _provinceOptions);
            if (row.Issue == PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved)
            {
                unresolved++;
            }

            _rows.Add(row);
            added++;
        }

        EnsurePopupCacheSize();
        Repaint();
        ShowNotification(new GUIContent($"加入 {added} 个（跳过重复 {skipped}，未匹配省名 {unresolved}）"));
    }

    private void BatchBindPendingRows()
    {
        if (CountSelectedRows() == 0)
        {
            EditorUtility.DisplayDialog("批量绑定", "请先勾选要绑定的行（可用全选）。", "确定");
            return;
        }

        int success = 0;
        int failed = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row == null || !row.IsSelected || row.Target == null)
            {
                continue;
            }

            if (row.Issue == PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved)
            {
                failed++;
                continue;
            }

            if (PlateMapVehiclePointsBindingUtility.ApplyBinding(row))
            {
                success++;
            }
            else
            {
                failed++;
            }
        }

        RefreshRows();
        ShowNotification(new GUIContent($"批量绑定完成：成功 {success}，失败/跳过 {failed}"));
    }

    private void UpdateDefaultVisualDataOnSelected()
    {
        if (CountSelectedRows() == 0)
        {
            EditorUtility.DisplayDialog("更新默认数据", "请先勾选要更新的行（可用全选）。", "确定");
            return;
        }

        int success = 0;
        int skipped = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row == null || !row.IsSelected)
            {
                continue;
            }

            if (row.Target == null)
            {
                skipped++;
                continue;
            }

            if (PlateMapVehiclePointsBindingUtility.ApplyDefaultVisualData(row.Target, forceOverwriteColors: true))
            {
                success++;
            }
            else
            {
                skipped++;
            }
        }

        ShowNotification(new GUIContent($"更新默认数据：成功 {success}，跳过 {skipped}"));
    }

    private void SetAllRowsSelected(bool selected)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
            {
                _rows[i].IsSelected = selected;
            }
        }

        Repaint();
    }

    private int CountSelectedRows()
    {
        int count = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null && _rows[i].IsSelected)
            {
                count++;
            }
        }

        return count;
    }

    private void SavePersistence()
    {
        if (_store == null)
        {
            _store = PlateMapVehiclePointsBindingStore.LoadOrCreate();
        }

        PlateMapVehiclePointsBindingUtility.SyncStoreFromRows(_store, _rows);
        AssetDatabase.SaveAssets();
        RefreshRows();
        ShowNotification(new GUIContent("持久化已保存"));
    }

    private int CountBoundRows()
    {
        int count = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i]?.Target != null &&
                _rows[i].Target.GetComponent<PlateMapGeoConverter>() != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountIssueRows()
    {
        int count = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null && _rows[i].Issue != PlateMapVehiclePointsBindingUtility.RowIssue.None)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetIssueTitle(PlateMapVehiclePointsBindingUtility.RowIssue issue)
    {
        switch (issue)
        {
            case PlateMapVehiclePointsBindingUtility.RowIssue.NotPersisted:
                return "未持久化";
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingInScene:
                return "持久化对象缺失";
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceMismatch:
                return "省份不一致";
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
                return "组件未齐";
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
                return "待绑定";
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return "省名未匹配";
            default:
                return "正常";
        }
    }

    private static Color GetIssueTextColor(PlateMapVehiclePointsBindingUtility.RowIssue issue)
    {
        switch (issue)
        {
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingInScene:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceMismatch:
                return new Color(0.95f, 0.25f, 0.2f);
            case PlateMapVehiclePointsBindingUtility.RowIssue.NotPersisted:
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return new Color(0.95f, 0.72f, 0.1f);
            default:
                return new Color(0.2f, 0.85f, 0.35f);
        }
    }

    private static Color GetRowBackground(PlateMapVehiclePointsBindingUtility.RowIssue issue)
    {
        switch (issue)
        {
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingInScene:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceMismatch:
                return new Color(0.45f, 0.12f, 0.12f, 0.22f);
            case PlateMapVehiclePointsBindingUtility.RowIssue.NotPersisted:
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return new Color(0.42f, 0.32f, 0.05f, 0.18f);
            default:
                return new Color(0.1f, 0.28f, 0.14f, 0.12f);
        }
    }
}
#endif
