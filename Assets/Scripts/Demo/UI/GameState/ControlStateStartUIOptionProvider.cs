using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 收集操控状态跳转 UI 下拉选项：省份名、板块模块名、车辆零件名。
/// </summary>
public static class ControlStateStartUIOptionProvider
{
    private struct PartOption
    {
        public string PartName;
        public string PartId;
    }

    private static readonly List<PartOption> CachedPartOptions = new List<PartOption>();
    /// <summary>从 <see cref="ChinaProvinceMapDatabase"/> 获取全部省级名称。</summary>
    public static List<string> CollectProvinceNames()
    {
        IReadOnlyList<ChinaProvinceMapFocusData> all = ChinaProvinceMapDatabase.All;
        List<string> names = new List<string>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            string name = all[i].ProvinceName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        names.Sort();
        return names;
    }

    /// <summary>从场景 <see cref="PlateMapDisplayModule"/> 收集板块显示名（省市名，用于高亮）。</summary>
    public static List<string> CollectPlateHighlightNames()
    {
        HashSet<string> unique = new HashSet<string>();
        PlateMapDisplayModule[] modules = Object.FindObjectsByType<PlateMapDisplayModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < modules.Length; i++)
        {
            PlateMapDisplayModule module = modules[i];
            if (module == null)
            {
                continue;
            }

            string name = module.DisplayName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                unique.Add(name);
            }
        }

        List<string> names = new List<string>(unique);
        names.Sort();
        return names;
    }

    /// <summary>从场景 <see cref="PlateMapDisplayModule"/> 收集板块模块名（GameObject 名）。</summary>
    public static List<string> CollectProvinceModuleNames()
    {
        HashSet<string> unique = new HashSet<string>();
        PlateMapDisplayModule[] modules = Object.FindObjectsByType<PlateMapDisplayModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < modules.Length; i++)
        {
            PlateMapDisplayModule module = modules[i];
            if (module == null)
            {
                continue;
            }

            string key = module.ModuleKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                unique.Add(key);
            }
        }

        List<string> names = new List<string>(unique);
        names.Sort();
        return names;
    }

    /// <summary>从 <see cref="VehicleToPartTransitionController"/> 零件列表收集零件名。</summary>
    public static List<string> CollectPartNames()
    {
        RebuildPartOptionsCache();
        List<string> names = new List<string>();
        for (int i = 0; i < CachedPartOptions.Count; i++)
        {
            names.Add(CachedPartOptions[i].PartName);
        }
        return names;
    }

    /// <summary>读取零件下拉当前项对应的 partId；无有效项时返回 null。</summary>
    public static string GetSelectedPartId(Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
        {
            return null;
        }

        if (dropdown.value < 0 || dropdown.value >= CachedPartOptions.Count)
        {
            return null;
        }

        return CachedPartOptions[dropdown.value].PartId;
    }

    /// <summary>将名称列表写入 Dropdown，并尽量选中 defaultValue。</summary>
    public static void ApplyOptions(Dropdown dropdown, IReadOnlyList<string> options, string defaultValue)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();
        if (options == null || options.Count == 0)
        {
            dropdown.AddOptions(new List<string> { "(无可用项)" });
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return;
        }

        dropdown.AddOptions(new List<string>(options));
        int index = 0;
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == defaultValue)
                {
                    index = i;
                    break;
                }
            }
        }

        dropdown.value = index;
        dropdown.RefreshShownValue();
    }

    /// <summary>读取 Dropdown 当前选中项文本；无有效项时返回 null。</summary>
    public static string GetSelectedText(Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
        {
            return null;
        }

        string text = dropdown.options[dropdown.value].text;
        if (text == "(无可用项)")
        {
            return null;
        }

        return text;
    }

    private static void RebuildPartOptionsCache()
    {
        CachedPartOptions.Clear();
        VehicleToPartTransitionController controller =
            Object.FindFirstObjectByType<VehicleToPartTransitionController>();
        if (controller == null)
        {
            return;
        }

        IReadOnlyList<VehicleToPartTransitionController.PartBindingData> bindings = controller.ConfiguredPartRoots;
        if (bindings == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            VehicleToPartTransitionController.PartBindingData binding = bindings[i];
            if (binding.partRoot == null)
            {
                continue;
            }

            string partId = string.IsNullOrWhiteSpace(binding.partId)
                ? binding.partRoot.name
                : binding.partId.Trim();
            CachedPartOptions.Add(new PartOption
            {
                PartName = binding.partRoot.name,
                PartId = partId
            });
        }
    }
}
