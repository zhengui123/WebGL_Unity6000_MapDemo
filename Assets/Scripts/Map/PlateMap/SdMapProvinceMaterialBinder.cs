using UnityEngine;

/// <summary>
/// 挂在 polySurface1（地图板块父节点）上：统一材质，并为 SdMapPlateHud 烘焙顶面轮廓距离到顶点色 R。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SdMapProvinceMaterialBinder : MonoBehaviour
{
    [SerializeField] private Material _provinceMaterial;

    [Tooltip("留空则使用下方默认路径")]
    [SerializeField] private string _defaultMaterialPath = "Assets/Materials/M_SdMapProvinceTech.mat";

    [Header("顶面轮廓距离烘焙 (SdMapPlateHud)")]
    [Tooltip("勾选后，应用材质时会自动烘焙；也可在 Inspector 点按钮或组件右键菜单手动烘焙")]
    [SerializeField] private bool _bakeTopContourDistance = true;

    [Tooltip("顶面判定：局部法线 Y 小于等于 -该值 视为顶面，需与 Shader 一致")]
    [SerializeField] private float _topNormalThreshold = 0.85f;

    private void OnEnable()
    {
        EnsureMaterialReference();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        EnsureMaterialReference();
        ApplyMaterial();
    }

    private void EnsureMaterialReference()
    {
        if (_provinceMaterial != null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(_defaultMaterialPath))
        {
            _provinceMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(_defaultMaterialPath);
        }
#endif
    }

    /// <summary>
    /// 将材质应用到本节点下全部 MeshRenderer（不含自身无 Renderer 的情况）。
    /// </summary>
    public void ApplyMaterial()
    {
        if (_provinceMaterial == null)
        {
            return;
        }

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].sharedMaterial = _provinceMaterial;
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }

        if (_bakeTopContourDistance && UsesHudPlateShader(_provinceMaterial))
        {
            BakeTopContourDistance();
        }
    }

    /// <summary>
    /// 为子级所有 Mesh 烘焙顶面外轮廓 → 中心的距离（写入顶点色 R）。仅 SdMapPlateHud 需要。
    /// </summary>
    [ContextMenu("烘焙顶面轮廓距离")]
    public void BakeTopContourDistance()
    {
        if (!UsesHudPlateShader(_provinceMaterial))
        {
            Debug.LogWarning(
                "[SdMapProvinceMaterialBinder] 当前材质不是 Custom/SdMapPlateHud，无法烘焙顶面轮廓。请指定 M_SdMapPlateHud。");
            return;
        }

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        int baked = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter meshFilter = renderers[i] != null ? renderers[i].GetComponent<MeshFilter>() : null;
            if (meshFilter == null)
            {
                continue;
            }

            SdMapPlateTopEdgeBaker.Bake(meshFilter, _topNormalThreshold);
            baked++;
        }

        Debug.Log($"[SdMapProvinceMaterialBinder] 已在 {gameObject.name} 下烘焙 {baked} 个网格的顶面轮廓距离。");
    }

    private static bool UsesHudPlateShader(Material material)
    {
        return material != null
               && material.shader != null
               && material.shader.name == "Custom/SdMapPlateHud";
    }
}
