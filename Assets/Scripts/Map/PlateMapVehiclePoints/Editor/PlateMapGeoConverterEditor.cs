#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlateMapGeoConverter Inspector：可搜索省份下拉，自动写入 provinceCode。
/// </summary>
[CustomEditor(typeof(PlateMapGeoConverter))]
public class PlateMapGeoConverterEditor : Editor
{
    private const int MaxVisibleProvinceCount = 12;
    private const float ProvinceListHeight = 22f;

    private const string ProvinceSearchControlName = "PlateMapProvinceSearch";

    private string _provinceSearch = string.Empty;
    private Vector2 _provinceScrollPos;
    private List<PlateMapBoundaryData> _cachedProvinces;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProvinceQuickPicker();
        EditorGUILayout.Space(6f);
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("重建地理映射", GUILayout.Height(26f)))
        {
            foreach (Object item in targets)
            {
                if (item is PlateMapGeoConverter converter)
                {
                    converter.Rebuild();
                    EditorUtility.SetDirty(converter);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProvinceQuickPicker()
    {
        SerializedProperty provinceCodeProp = serializedObject.FindProperty("_provinceCode");
        if (provinceCodeProp == null)
        {
            return;
        }

        EditorGUILayout.LabelField("省份快捷选择", EditorStyles.boldLabel);

        string currentCode = provinceCodeProp.stringValue;
        string currentName = ResolveProvinceName(currentCode);
        EditorGUILayout.LabelField("当前", $"{currentName} ({currentCode})");

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        GUI.SetNextControlName(ProvinceSearchControlName);
        _provinceSearch = EditorGUILayout.TextField("搜索省份", _provinceSearch);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }
        if (GUILayout.Button("清空", GUILayout.Width(48f)))
        {
            _provinceSearch = string.Empty;
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        bool isSearchFocused = GUI.GetNameOfFocusedControl() == ProvinceSearchControlName;
        if (!isSearchFocused)
        {
            return;
        }

        DrawProvinceButtonList(provinceCodeProp);
    }

    private void DrawProvinceButtonList(SerializedProperty provinceCodeProp)
    {
        IEnumerable<PlateMapBoundaryData> filtered = FilterProvinces(_provinceSearch);
        var list = filtered.Take(MaxVisibleProvinceCount).ToList();

        EditorGUILayout.HelpBox(
            $"匹配 {FilterProvinces(_provinceSearch).Count()} 项，显示前 {list.Count} 项。点击条目自动写入 Province Code 并重建映射。",
            MessageType.None);

        float listHeight = Mathf.Min(MaxVisibleProvinceCount, list.Count) * ProvinceListHeight;
        if (listHeight <= 0f)
        {
            EditorGUILayout.LabelField("无匹配省份");
            return;
        }

        Rect scrollRect = GUILayoutUtility.GetRect(0f, listHeight, GUILayout.ExpandWidth(true));
        Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, list.Count * ProvinceListHeight);
        _provinceScrollPos = GUI.BeginScrollView(scrollRect, _provinceScrollPos, viewRect);

        for (int i = 0; i < list.Count; i++)
        {
            PlateMapBoundaryData item = list[i];
            Rect rowRect = new Rect(0f, i * ProvinceListHeight, viewRect.width, ProvinceListHeight - 2f);
            if (GUI.Button(rowRect, $"{item.provinceName}  ({item.provinceCode})"))
            {
                ApplyProvinceSelection(provinceCodeProp, item);
            }
        }

        GUI.EndScrollView();
    }

    private void ApplyProvinceSelection(SerializedProperty provinceCodeProp, PlateMapBoundaryData item)
    {
        provinceCodeProp.stringValue = item.provinceCode;

        SerializedProperty westLon = serializedObject.FindProperty("_westAnchor.longitude");
        SerializedProperty westLat = serializedObject.FindProperty("_westAnchor.latitude");
        SerializedProperty eastLon = serializedObject.FindProperty("_eastAnchor.longitude");
        SerializedProperty eastLat = serializedObject.FindProperty("_eastAnchor.latitude");
        if (westLon != null)
        {
            westLon.doubleValue = item.westLongitude;
        }

        if (westLat != null)
        {
            westLat.doubleValue = item.southLatitude;
        }

        if (eastLon != null)
        {
            eastLon.doubleValue = item.eastLongitude;
        }

        if (eastLat != null)
        {
            eastLat.doubleValue = item.northLatitude;
        }

        serializedObject.ApplyModifiedProperties();

        foreach (Object targetObject in targets)
        {
            if (targetObject is not PlateMapGeoConverter converter)
            {
                continue;
            }

            converter.Rebuild();
            EditorUtility.SetDirty(converter);
        }

        _provinceSearch = string.Empty;
        GUI.FocusControl(null);
        Repaint();
    }

    private static string ResolveProvinceName(string provinceCode)
    {
        if (PlateMapBoundaryDatabase.TryGet(provinceCode, out PlateMapBoundaryData data))
        {
            return data.provinceName;
        }

        return string.IsNullOrWhiteSpace(provinceCode) ? "(未设置)" : provinceCode;
    }

    private void EnsureProvinceCache()
    {
        if (_cachedProvinces != null && _cachedProvinces.Count > 0)
        {
            return;
        }

        _cachedProvinces = PlateMapBoundaryDatabase.All
            .OrderBy(item => item.provinceCode == PlateMapBoundaryDatabase.NationalProvinceCode ? 0 : 1)
            .ThenBy(item => item.provinceName)
            .ToList();
    }

    private IEnumerable<PlateMapBoundaryData> FilterProvinces(string keyword)
    {
        EnsureProvinceCache();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return _cachedProvinces;
        }

        string normalized = keyword.Trim();
        return _cachedProvinces.Where(item =>
            item.provinceName.Contains(normalized) ||
            item.provinceCode.Contains(normalized));
    }
}
#endif
