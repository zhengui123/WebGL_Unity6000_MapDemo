#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 国外车辆点绑定持久化（与国内 Store 分离）。
/// </summary>
public class ForeignPlateMapVehiclePointsBindingStore : ScriptableObject
{
    public const string AssetPath = "Assets/Scripts/Editor/Map/ForeignPlateMapVehiclePointsBindingStore.asset";

    [SerializeField] private List<PlateMapVehiclePointsBindingStore.Entry> _entries =
        new List<PlateMapVehiclePointsBindingStore.Entry>();

    public IReadOnlyList<PlateMapVehiclePointsBindingStore.Entry> Entries => _entries;

    public void SetEntries(List<PlateMapVehiclePointsBindingStore.Entry> entries)
    {
        _entries = entries ?? new List<PlateMapVehiclePointsBindingStore.Entry>();
    }

    public static ForeignPlateMapVehiclePointsBindingStore LoadOrCreate()
    {
        ForeignPlateMapVehiclePointsBindingStore store =
            UnityEditor.AssetDatabase.LoadAssetAtPath<ForeignPlateMapVehiclePointsBindingStore>(AssetPath);
        if (store != null)
        {
            return store;
        }

        store = CreateInstance<ForeignPlateMapVehiclePointsBindingStore>();
        UnityEditor.AssetDatabase.CreateAsset(store, AssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        return store;
    }
}
#endif
