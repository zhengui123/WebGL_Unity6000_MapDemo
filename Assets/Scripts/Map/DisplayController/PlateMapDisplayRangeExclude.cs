using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 板块显示模块的范围判定排除配置（如台湾子物体 default 仅作显示）。
/// 挂在与 <see cref="PlateMapDisplayModule"/> 同一节点上；未配置时自动查找名为「default」的子物体。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlateMapDisplayModule))]
[DefaultExecutionOrder(-50)]
public class PlateMapDisplayRangeExclude : MonoBehaviour
{
    private const string DefaultChildName = "default";

    [Header("排除根节点（仅显示，不参与包围盒/拾取）")]
    [SerializeField] private List<Transform> _excludeRoots = new List<Transform>();

    [Tooltip("列表为空时，自动查找同名子物体并加入排除")]
    [SerializeField] private bool _autoFindDefaultChild = true;

    [Tooltip("启动时禁用排除子树下的 Collider，避免射线拾取命中")]
    [SerializeField] private bool _disableExcludedColliders = true;

    private void Awake()
    {
        EnsureReady();
    }

    /// <summary>供 PlateMapDisplayModule 在缓存 Renderer 前调用，保证排除列表已就绪。</summary>
    public void EnsureReady()
    {
        if (_autoFindDefaultChild)
        {
            EnsureDefaultChildExcluded();
        }

        if (_disableExcludedColliders)
        {
            DisableExcludedColliders();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureDefaultChildExcluded();
    }

    private void OnValidate()
    {
        if (_autoFindDefaultChild && (_excludeRoots == null || _excludeRoots.Count == 0))
        {
            EnsureDefaultChildExcluded();
        }
    }
#endif

    /// <summary>该 Transform 是否落在任一排除根之下（含根自身）。</summary>
    public bool IsExcluded(Transform target)
    {
        if (target == null || _excludeRoots == null || _excludeRoots.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _excludeRoots.Count; i++)
        {
            Transform root = _excludeRoots[i];
            if (root == null)
            {
                continue;
            }

            if (target == root || target.IsChildOf(root))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>手动加入排除根（已存在则忽略）。</summary>
    public void AddExcludeRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        if (_excludeRoots == null)
        {
            _excludeRoots = new List<Transform>();
        }

        if (!_excludeRoots.Contains(root))
        {
            _excludeRoots.Add(root);
        }
    }

    private void EnsureDefaultChildExcluded()
    {
        if (_excludeRoots == null)
        {
            _excludeRoots = new List<Transform>();
        }

        for (int i = 0; i < _excludeRoots.Count; i++)
        {
            Transform existing = _excludeRoots[i];
            if (existing != null &&
                string.Equals(existing.name, DefaultChildName, System.StringComparison.Ordinal))
            {
                return;
            }
        }

        Transform found = FindChildRecursive(transform, DefaultChildName);
        if (found != null)
        {
            _excludeRoots.Add(found);
        }
    }

    private void DisableExcludedColliders()
    {
        if (_excludeRoots == null)
        {
            return;
        }

        for (int i = 0; i < _excludeRoots.Count; i++)
        {
            Transform root = _excludeRoots[i];
            if (root == null)
            {
                continue;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null)
                {
                    colliders[c].enabled = false;
                }
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null &&
                string.Equals(child.name, childName, System.StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
