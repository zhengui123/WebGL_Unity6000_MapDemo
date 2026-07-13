#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块车辆点绑定持久化数据（存于 Assets/Scripts/Editor/Map/）。
/// </summary>
public class PlateMapVehiclePointsBindingStore : ScriptableObject
{
    public const string AssetPath = "Assets/Scripts/Editor/Map/PlateMapVehiclePointsBindingStore.asset";

    [Serializable]
    public class Entry
    {
        public string sceneAssetPath;
        public string hierarchyPath;
        public string objectName;
        public string provinceCode;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => _entries;

    public void SetEntries(List<Entry> entries)
    {
        _entries = entries ?? new List<Entry>();
    }

    public static PlateMapVehiclePointsBindingStore LoadOrCreate()
    {
        PlateMapVehiclePointsBindingStore store =
            UnityEditor.AssetDatabase.LoadAssetAtPath<PlateMapVehiclePointsBindingStore>(AssetPath);
        if (store != null)
        {
            return store;
        }

        store = CreateInstance<PlateMapVehiclePointsBindingStore>();
        UnityEditor.AssetDatabase.CreateAsset(store, AssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        return store;
    }
}
#endif
