using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 国外各大板块对应的 AllPlateMap 初始局部坐标配置。
/// 国内仍使用 <see cref="EarthTransition"/> 上的手动/自动字段，不走本表。
/// </summary>
[CreateAssetMenu(
    fileName = "EarthPlateMapPositionConfig",
    menuName = "Map/Earth Plate Map Position Config",
    order = 50)]
public class EarthPlateMapPositionConfig : ScriptableObject
{
    [Serializable]
    public class PlateLocalPositionEntry
    {
        [Tooltip("国外大板块 firstClassCode，如 EAST_ASIA")]
        public string plateCode = "EAST_ASIA";

        [Tooltip("显示名（仅编辑器用）")]
        public string plateName = "东亚";

        [Tooltip("AllPlateMap 相对父节点的局部坐标")]
        public Vector3 localPosition;
    }

    [SerializeField] private List<PlateLocalPositionEntry> _entries = new List<PlateLocalPositionEntry>();

    /// <summary>配置条目（编辑器读写）。</summary>
    public List<PlateLocalPositionEntry> Entries => _entries;

    /// <summary>按 plateCode 查找局部坐标（忽略大小写）。</summary>
    public bool TryGetLocalPosition(string plateCode, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (string.IsNullOrWhiteSpace(plateCode) || _entries == null)
        {
            return false;
        }

        string key = plateCode.Trim();
        for (int i = 0; i < _entries.Count; i++)
        {
            PlateLocalPositionEntry entry = _entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.plateCode))
            {
                continue;
            }

            if (string.Equals(entry.plateCode.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                localPosition = entry.localPosition;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 写入或更新指定板块的局部坐标；已存在则覆盖，否则追加。
    /// </summary>
    public void SetOrAddLocalPosition(string plateCode, string plateName, Vector3 localPosition)
    {
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            return;
        }

        string key = plateCode.Trim();
        if (_entries == null)
        {
            _entries = new List<PlateLocalPositionEntry>();
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            PlateLocalPositionEntry entry = _entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.plateCode))
            {
                continue;
            }

            if (string.Equals(entry.plateCode.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                entry.localPosition = localPosition;
                if (!string.IsNullOrWhiteSpace(plateName))
                {
                    entry.plateName = plateName.Trim();
                }

                return;
            }
        }

        _entries.Add(new PlateLocalPositionEntry
        {
            plateCode = key,
            plateName = string.IsNullOrWhiteSpace(plateName) ? key : plateName.Trim(),
            localPosition = localPosition
        });
    }

    /// <summary>
    /// 确保条目存在：已有则只更新显示名（不改 local）；没有则追加默认 Vector3.zero。
    /// </summary>
    public bool EnsureEntryExists(string plateCode, string plateName)
    {
        if (string.IsNullOrWhiteSpace(plateCode))
        {
            return false;
        }

        string key = plateCode.Trim();
        if (_entries == null)
        {
            _entries = new List<PlateLocalPositionEntry>();
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            PlateLocalPositionEntry entry = _entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.plateCode))
            {
                continue;
            }

            if (string.Equals(entry.plateCode.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(plateName))
                {
                    entry.plateName = plateName.Trim();
                }

                return false;
            }
        }

        _entries.Add(new PlateLocalPositionEntry
        {
            plateCode = key,
            plateName = string.IsNullOrWhiteSpace(plateName) ? key : plateName.Trim(),
            localPosition = Vector3.zero
        });
        return true;
    }

    /// <summary>
    /// 按 WorldMapRegionController 绑定列表补齐 Config 条目（不覆盖已有坐标）。
    /// </summary>
    /// <returns>新追加的条目数。</returns>
    public int SyncEntriesFromForeignBindings(
        IReadOnlyList<WorldMapRegionController.ForeignPlateBinding> bindings)
    {
        if (bindings == null || bindings.Count == 0)
        {
            return 0;
        }

        int added = 0;
        for (int i = 0; i < bindings.Count; i++)
        {
            WorldMapRegionController.ForeignPlateBinding binding = bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.plateCode))
            {
                continue;
            }

            if (EnsureEntryExists(binding.plateCode, binding.plateName))
            {
                added++;
            }
        }

        return added;
    }
}
