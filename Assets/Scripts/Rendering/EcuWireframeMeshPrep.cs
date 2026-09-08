using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为使用 VSOC/ECU/ConvexEdge* 且三角边线模式的渲染器准备重心网格（WebGL 无 GS 时必需）。
/// 挂在 ECU 根节点或零件模型上，Awake 时处理自身及子级 MeshFilter。
/// </summary>
[DisallowMultipleComponent]
public class EcuWireframeMeshPrep : MonoBehaviour
{
    [SerializeField] private bool _includeInactive = true;
    [SerializeField] private bool _onlyWhenTriangleMode = true;
    [Tooltip("Shader 名包含此字符串时才处理（默认 ConvexEdge）。")]
    [SerializeField] private string _shaderNameContains = "ConvexEdge";

    private bool _prepared;

    private void Awake()
    {
        Prepare();
    }

    private void OnEnable()
    {
        // 若被其它 Awake 提前 SetActive(false)，本组件 Awake 会延后；首次显示时再补一次
        if (!_prepared)
        {
            Prepare();
        }
    }

    [ContextMenu("立即准备重心网格")]
    public void Prepare()
    {
        MeshFilter[] filters = _includeInactive
            ? GetComponentsInChildren<MeshFilter>(true)
            : GetComponentsInChildren<MeshFilter>(false);

        HashSet<int> prepared = new HashSet<int>();
        int changed = 0;

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null)
            {
                continue;
            }

            if (!ShouldPrepare(filter))
            {
                continue;
            }

            if (EcuBarycentricMeshUtility.TryPrepareMeshFilter(filter, prepared))
            {
                changed++;
            }
        }

        _prepared = true;
        if (changed > 0)
        {
            LogManager.LogFeature($"[EcuWireframeMeshPrep] 已为 {changed} 个 MeshFilter 写入三角重心坐标：{name}");
        }
    }

    private bool ShouldPrepare(MeshFilter filter)
    {
        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterials == null)
        {
            return false;
        }

        Material[] mats = renderer.sharedMaterials;
        for (int m = 0; m < mats.Length; m++)
        {
            Material mat = mats[m];
            if (mat == null || mat.shader == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(_shaderNameContains)
                && mat.shader.name.IndexOf(_shaderNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (_onlyWhenTriangleMode)
            {
                bool isTriangle = mat.IsKeywordEnabled("_EDGEMODE_TRIANGLE");
                if (!isTriangle && mat.HasProperty("_EdgeMode"))
                {
                    isTriangle = mat.GetFloat("_EdgeMode") >= 0.5f;
                }

                if (!isTriangle)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }
}
