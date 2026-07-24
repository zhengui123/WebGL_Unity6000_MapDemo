#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 国内-板块车辆点绑定控制面板：扫描场景、批量从 Hierarchy 加入、按对象名匹配省份、一键绑定三组件。
/// 持久化：Assets/Scripts/Editor/Map/PlateMapVehiclePointsBindingStore.asset
/// </summary>
public class PlateMapVehiclePointsBindingEditorWindow : EditorWindow
{
    private enum SortColumn
    {
        None = 0,
        ObjectName,
        Province,
        Status
    }

    private const float RowHeight = 88f;

    private bool _foreignMode;
    private PlateMapVehiclePointsBindingStore _store;
    private ForeignPlateMapVehiclePointsBindingStore _foreignStore;
    private List<PlateMapBoundaryData> _provinceOptions;
    private List<PlateMapVehiclePointsBindingUtility.BindingRow> _rows = new List<PlateMapVehiclePointsBindingUtility.BindingRow>();
    private Vector2 _scrollPosition;
    private string[] _provinceLabels = Array.Empty<string>();
    private string[] _provinceFilterTexts = Array.Empty<string>();
    private SortColumn _sortColumn = SortColumn.None;
    private bool _sortAscending = true;

    [MenuItem("Tools/地图/国内-板块车辆点绑定")]
    public static void OpenDomesticWindow()
    {
        OpenWindow(foreignMode: false);
    }

    [MenuItem("Tools/地图/国外-车辆点绑定")]
    public static void OpenForeignWindow()
    {
        OpenWindow(foreignMode: true);
    }

    private static void OpenWindow(bool foreignMode)
    {
        // 国内外分两个窗口实例，避免互相覆盖
        string typeKey = foreignMode ? "Foreign" : "Domestic";
        PlateMapVehiclePointsBindingEditorWindow window =
            GetWindow<PlateMapVehiclePointsBindingEditorWindow>($"PlateBind_{typeKey}");
        window._foreignMode = foreignMode;
        window.titleContent = new GUIContent(foreignMode ? "国外-车辆点绑定" : "国内-板块车辆点绑定");
        window.minSize = new Vector2(760f, 420f);
        window.Show();
        window.ReloadStoreAndRows();
    }

    private void OnEnable()
    {
        if (titleContent != null &&
            !string.IsNullOrEmpty(titleContent.text) &&
            titleContent.text.IndexOf("国外", System.StringComparison.Ordinal) >= 0)
        {
            _foreignMode = true;
        }

        ReloadStoreAndRows();
    }

    private void ReloadStoreAndRows()
    {
        if (_foreignMode)
        {
            WorldMapRegionCodeTable.ReloadForEditor();
            _foreignStore = ForeignPlateMapVehiclePointsBindingStore.LoadOrCreate();
            _store = null;
        }
        else
        {
            _store = PlateMapVehiclePointsBindingStore.LoadOrCreate();
            _foreignStore = null;
        }

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

        if (GUILayout.Button("删除绑定", EditorStyles.toolbarButton, GUILayout.Width(80f)))
        {
            RemoveBindingOnSelected();
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
            UnityEngine.Object storeObject = _foreignMode ? (UnityEngine.Object)_foreignStore : _store;
            Selection.activeObject = storeObject;
            EditorGUIUtility.PingObject(storeObject);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        int issueCount = CountIssueRows();
        string storePath = _foreignMode
            ? ForeignPlateMapVehiclePointsBindingStore.AssetPath
            : PlateMapVehiclePointsBindingStore.AssetPath;
        string scopeLabel = _foreignMode ? "【国外·secondClassCode】" : "【国内·省级adcode】";
        EditorGUILayout.LabelField("持久化文件", storePath, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"{scopeLabel} 已扫描 {_rows.Count} 行 | 已勾选 {CountSelectedRows()} | 含 GeoConverter 的板块 {CountBoundRows()} 个 | 异常 {issueCount} 个",
            EditorStyles.boldLabel);

        if (_foreignMode)
        {
            EditorGUILayout.HelpBox(
                "国家 code 与国内省表同构：大板块=firstClassCode，国家=secondClassCode。" +
                "绑定/更新默认数据时会合并该国全部子 Renderer 的 XZ 外接盒并强制刷新 Left/Right 坐标。",
                MessageType.Info);
        }

        if (issueCount > 0)
        {
            EditorGUILayout.HelpBox(
                "存在与持久化不一致或未绑定的条目（红色/黄色标注）。请修正后点击「保存持久化」。",
                MessageType.Warning);
        }
    }

    private void DrawHeader()
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
        float x = headerRect.x;
        float width = headerRect.width;
        const float checkWidth = 28f;
        float objectWidth = width * 0.28f;
        float provinceWidth = width * 0.24f;
        float statusWidth = width * 0.24f;
        float actionWidth = width * 0.14f;

        bool allSelected = _rows.Count > 0 && CountSelectedRows() == _rows.Count;
        bool newAllSelected = EditorGUI.Toggle(new Rect(x, headerRect.y, checkWidth, 18f), allSelected);
        if (newAllSelected != allSelected)
        {
            SetAllRowsSelected(newAllSelected);
        }

        x += checkWidth;
        DrawSortableHeaderButton(new Rect(x, headerRect.y, objectWidth, 20f), "板块对象", SortColumn.ObjectName);
        x += objectWidth;
        DrawSortableHeaderButton(new Rect(x, headerRect.y, provinceWidth, 20f), _foreignMode ? "国家/板块" : "省份", SortColumn.Province);
        x += provinceWidth;
        DrawSortableHeaderButton(new Rect(x, headerRect.y, statusWidth, 20f), "状态", SortColumn.Status);
        x += statusWidth;
        EditorGUI.LabelField(new Rect(x, headerRect.y, actionWidth, 18f), "操作", EditorStyles.miniBoldLabel);
    }

    private void DrawSortableHeaderButton(Rect rect, string title, SortColumn column)
    {
        string arrow = string.Empty;
        if (_sortColumn == column)
        {
            arrow = _sortAscending ? " ▲" : " ▼";
        }

        if (GUI.Button(rect, title + arrow, EditorStyles.miniButton))
        {
            if (_sortColumn == column)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }

            ApplyCurrentSort();
        }
    }

    private void DrawRows()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EnsureProvinceFilterCacheSize();

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
            GUILayout.Width(position.width * 0.28f - 12f));
        if (EditorGUI.EndChangeCheck())
        {
            row.Target = newTarget;
            PlateMapVehiclePointsBindingUtility.EvaluateComponentState(row);
            if (row.Target != null)
            {
                PlateMapVehiclePointsBindingUtility.ApplyProvinceFromObjectName(row, _provinceOptions);
                SyncProvinceFilterText(index);
            }
        }

        DrawProvinceSearchField(index, row, position.width * 0.24f - 12f);

        DrawStatusColumn(row, position.width * 0.24f - 12f);

        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.14f - 12f));
        if (GUILayout.Button("绑定", GUILayout.Height(24f)))
        {
            if (row.Target == null)
            {
                EditorUtility.DisplayDialog("绑定失败", "请先指定板块对象。", "确定");
            }
            else if (PlateMapVehiclePointsBindingUtility.ApplyBinding(row, forceRecalculateLeftRight: _foreignMode))
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
            EnsureProvinceFilterCacheSize();
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

    /// <summary>省份：上方搜索框过滤，下方弹出当前过滤结果。</summary>
    private void DrawProvinceSearchField(
        int index,
        PlateMapVehiclePointsBindingUtility.BindingRow row,
        float width)
    {
        EnsureProvinceFilterCacheSize();
        EditorGUILayout.BeginVertical(GUILayout.Width(width));

        EditorGUI.BeginChangeCheck();
        _provinceFilterTexts[index] = EditorGUILayout.TextField(
            _provinceFilterTexts[index] ?? string.Empty,
            EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        List<PlateMapBoundaryData> filtered = PlateMapVehiclePointsBindingUtility.FilterProvinceOptions(
            _provinceOptions,
            _provinceFilterTexts[index]);

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("无匹配省份", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] labels = new string[filtered.Count];
        int selected = 0;
        for (int i = 0; i < filtered.Count; i++)
        {
            labels[i] = PlateMapVehiclePointsBindingUtility.FormatProvinceLabel(filtered[i]);
            if (filtered[i].provinceCode == row.ProvinceCode)
            {
                selected = i;
            }
        }

        EditorGUI.BeginChangeCheck();
        int newSelected = EditorGUILayout.Popup(selected, labels);
        if (EditorGUI.EndChangeCheck() && newSelected >= 0 && newSelected < filtered.Count)
        {
            row.ProvinceCode = filtered[newSelected].provinceCode;
            _provinceFilterTexts[index] = string.Empty;
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawStatusColumn(PlateMapVehiclePointsBindingUtility.BindingRow row, float width)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true
        };
        style.normal.textColor = GetIssueTextColor(row.Issue);

        string title = GetIssueTitle(row.Issue);
        EditorGUILayout.LabelField(title, style);
        if (!string.IsNullOrEmpty(row.IssueDetail))
        {
            EditorGUILayout.LabelField(row.IssueDetail, EditorStyles.miniLabel);
        }

        if (row.Target != null)
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
        _provinceOptions = _foreignMode
            ? PlateMapVehiclePointsBindingUtility.BuildForeignCountryOptions()
            : PlateMapVehiclePointsBindingUtility.BuildProvinceOptions();
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
        Dictionary<string, string> filterByKey = new Dictionary<string, string>();

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

            if (existing.Target != null &&
                i < _provinceFilterTexts.Length &&
                !string.IsNullOrEmpty(_provinceFilterTexts[i]))
            {
                filterByKey[PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(existing.Target)] =
                    _provinceFilterTexts[i];
            }
        }

        _rows = _foreignMode
            ? PlateMapVehiclePointsBindingUtility.BuildRows(
                _foreignStore != null ? _foreignStore.Entries : null,
                foreignMode: true)
            : PlateMapVehiclePointsBindingUtility.BuildRows(_store);
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

        EnsureProvinceFilterCacheSize();
        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row?.Target == null)
            {
                _provinceFilterTexts[i] = string.Empty;
                continue;
            }

            string key = PlateMapVehiclePointsBindingUtility.BuildRowKeyPublic(row.Target);
            _provinceFilterTexts[i] = filterByKey.TryGetValue(key, out string kept) ? kept : string.Empty;
        }

        ApplyCurrentSort();
        Repaint();
    }

    private void EnsureProvinceFilterCacheSize()
    {
        if (_provinceFilterTexts != null && _provinceFilterTexts.Length == _rows.Count)
        {
            return;
        }

        string[] resized = new string[_rows.Count];
        int copy = _provinceFilterTexts != null
            ? Math.Min(_provinceFilterTexts.Length, resized.Length)
            : 0;
        for (int i = 0; i < copy; i++)
        {
            resized[i] = _provinceFilterTexts[i];
        }

        for (int i = copy; i < resized.Length; i++)
        {
            resized[i] = string.Empty;
        }

        _provinceFilterTexts = resized;
    }

    private void SyncProvinceFilterText(int index)
    {
        EnsureProvinceFilterCacheSize();
        if (index >= 0 && index < _provinceFilterTexts.Length)
        {
            _provinceFilterTexts[index] = string.Empty;
        }
    }

    private void ApplyCurrentSort()
    {
        if (_sortColumn == SortColumn.None || _rows == null || _rows.Count <= 1)
        {
            return;
        }

        EnsureProvinceFilterCacheSize();
        List<(PlateMapVehiclePointsBindingUtility.BindingRow row, string filter)> paired =
            new List<(PlateMapVehiclePointsBindingUtility.BindingRow, string)>(_rows.Count);
        for (int i = 0; i < _rows.Count; i++)
        {
            paired.Add((_rows[i], _provinceFilterTexts[i] ?? string.Empty));
        }

        paired.Sort((a, b) =>
        {
            int cmp = CompareRows(a.row, b.row);
            return _sortAscending ? cmp : -cmp;
        });

        for (int i = 0; i < paired.Count; i++)
        {
            _rows[i] = paired[i].row;
            _provinceFilterTexts[i] = paired[i].filter;
        }
    }

    private static readonly CompareInfo ZhCnCompareInfo =
        CultureInfo.GetCultureInfo("zh-CN").CompareInfo;

    private int CompareRows(
        PlateMapVehiclePointsBindingUtility.BindingRow a,
        PlateMapVehiclePointsBindingUtility.BindingRow b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        switch (_sortColumn)
        {
            case SortColumn.ObjectName:
                return CompareObjectName(a, b);
            case SortColumn.Province:
                return CompareProvinceCode(a, b);
            case SortColumn.Status:
                // 状态为主；同状态内按对象拼音 → 省份 code
                int issueCmp = ((int)a.Issue).CompareTo((int)b.Issue);
                if (issueCmp != 0)
                {
                    return issueCmp;
                }

                int nameCmp = CompareObjectName(a, b);
                if (nameCmp != 0)
                {
                    return nameCmp;
                }

                return CompareProvinceCode(a, b);
            default:
                return 0;
        }
    }

    /// <summary>板块对象名：zh-CN 语言序（拼音 / 字母）。</summary>
    private static int CompareObjectName(
        PlateMapVehiclePointsBindingUtility.BindingRow a,
        PlateMapVehiclePointsBindingUtility.BindingRow b)
    {
        string nameA = a.Target != null ? a.Target.name : string.Empty;
        string nameB = b.Target != null ? b.Target.name : string.Empty;
        return ZhCnCompareInfo.Compare(nameA, nameB, CompareOptions.IgnoreCase);
    }

    /// <summary>省份：按 provinceCode 排序。</summary>
    private static int CompareProvinceCode(
        PlateMapVehiclePointsBindingUtility.BindingRow a,
        PlateMapVehiclePointsBindingUtility.BindingRow b)
    {
        return string.Compare(a.ProvinceCode, b.ProvinceCode, StringComparison.Ordinal);
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
        EnsureProvinceFilterCacheSize();
    }

    private void AddRowsFromHierarchySelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("从多选加入", "请先在 Hierarchy 中选中一个或多个板块对象。", "确定");
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
            _rows.Add(row);
            added++;
        }

        EnsureProvinceFilterCacheSize();
        ShowNotification(new GUIContent($"从多选加入 {added} 行"));
        Repaint();
    }

    private void BatchBindPendingRows()
    {
        int success = 0;
        int failed = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row == null || row.Target == null)
            {
                continue;
            }

            if (row.Issue == PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved)
            {
                failed++;
                continue;
            }

            if (PlateMapVehiclePointsBindingUtility.HasFullBinding(row.Target))
            {
                continue;
            }

            if (PlateMapVehiclePointsBindingUtility.ApplyBinding(row, forceRecalculateLeftRight: _foreignMode))
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

            // 未绑定则先绑定（绑定内已写随机点），再刷默认视觉并再写一遍随机三点
            if (!PlateMapVehiclePointsBindingUtility.HasFullBinding(row.Target))
            {
                if (row.Issue == PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved)
                {
                    skipped++;
                    continue;
                }

                if (!PlateMapVehiclePointsBindingUtility.ApplyBinding(row, forceRecalculateLeftRight: _foreignMode))
                {
                    skipped++;
                    continue;
                }
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

        RefreshRows();
        ShowNotification(new GUIContent($"更新默认数据：成功 {success}，跳过 {skipped}"));
    }

    private void RemoveBindingOnSelected()
    {
        if (CountSelectedRows() == 0)
        {
            EditorUtility.DisplayDialog("删除绑定", "请先勾选要删除绑定的行（可用全选）。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "删除绑定",
                $"将删除勾选的 {CountSelectedRows()} 个对象上的三组件（GeoConverter / Controller / InstancedRenderer），并删除其子物体 Left / Right。\n此操作可 Undo，是否继续？",
                "删除",
                "取消"))
        {
            return;
        }

        int success = 0;
        int skipped = 0;
        List<PlateMapVehiclePointsBindingUtility.BindingRow> remaining =
            new List<PlateMapVehiclePointsBindingUtility.BindingRow>();

        for (int i = 0; i < _rows.Count; i++)
        {
            PlateMapVehiclePointsBindingUtility.BindingRow row = _rows[i];
            if (row == null)
            {
                continue;
            }

            if (!row.IsSelected)
            {
                remaining.Add(row);
                continue;
            }

            if (row.Target == null)
            {
                skipped++;
                continue;
            }

            if (PlateMapVehiclePointsBindingUtility.RemoveBinding(row.Target))
            {
                success++;
            }
            else
            {
                skipped++;
                remaining.Add(row);
            }
        }

        _rows = remaining;
        EnsureProvinceFilterCacheSize();
        ShowNotification(new GUIContent($"删除绑定：成功 {success}，跳过 {skipped}"));
        Repaint();
    }

    private void SavePersistence()
    {
        if (_foreignMode)
        {
            if (_foreignStore == null)
            {
                _foreignStore = ForeignPlateMapVehiclePointsBindingStore.LoadOrCreate();
            }

            PlateMapVehiclePointsBindingUtility.SyncStoreFromRows(_foreignStore, _rows);
        }
        else
        {
            if (_store == null)
            {
                _store = PlateMapVehiclePointsBindingStore.LoadOrCreate();
            }

            PlateMapVehiclePointsBindingUtility.SyncStoreFromRows(_store, _rows);
        }

        ShowNotification(new GUIContent("已保存持久化"));
        RefreshRows();
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
                return "场景缺失";
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceMismatch:
                return "省份不一致";
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
                return "缺组件";
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
                return "待绑定";
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return "省份未解析";
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
                return new Color(0.85f, 0.2f, 0.2f);
            case PlateMapVehiclePointsBindingUtility.RowIssue.NotPersisted:
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return new Color(0.75f, 0.55f, 0.1f);
            default:
                return EditorStyles.label.normal.textColor;
        }
    }

    private static Color GetRowBackground(PlateMapVehiclePointsBindingUtility.RowIssue issue)
    {
        switch (issue)
        {
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingInScene:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceMismatch:
                return new Color(0.45f, 0.15f, 0.15f, 0.25f);
            case PlateMapVehiclePointsBindingUtility.RowIssue.NotPersisted:
            case PlateMapVehiclePointsBindingUtility.RowIssue.MissingComponents:
            case PlateMapVehiclePointsBindingUtility.RowIssue.PendingBind:
            case PlateMapVehiclePointsBindingUtility.RowIssue.ProvinceUnresolved:
                return new Color(0.4f, 0.35f, 0.1f, 0.2f);
            default:
                return new Color(0f, 0f, 0f, 0.05f);
        }
    }
}
#endif
