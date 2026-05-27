using UnityEngine;

/// <summary>
/// 将全息材质应用到车身层（mercedes），跳过边缘线层 mercedes_edge。
/// </summary>
[DisallowMultipleComponent]
public class MercedesHologramApplier : MonoBehaviour
{
    private const string DefaultMaterialPath = "Assets/Materials/Car/M_MercedesHologram.mat";
    private const string EdgeLayerName = "mercedes_edge";

    [SerializeField] private Material hologramMaterial;

    private void Awake()
    {
        ApplyHologramMaterial();
    }

    [ContextMenu("应用全息材质")]
    public void ApplyHologramMaterial()
    {
        Material mat = ResolveMaterial();
        if (mat == null)
        {
            Debug.LogWarning("[MercedesHologramApplier] 未找到全息材质。", this);
            return;
        }

        if (gameObject.name == EdgeLayerName)
        {
            return;
        }

        int count = 0;
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null || IsUnderEdgeLayer(renderer.transform))
            {
                continue;
            }

            if (renderer.sharedMaterial != null &&
                renderer.sharedMaterial.shader != null &&
                renderer.sharedMaterial.shader.name == "Custom/CarHologramEdgeOutline")
            {
                continue;
            }

            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }

            renderer.sharedMaterials = mats;
            count++;
        }

        Debug.Log($"[MercedesHologramApplier] 已对 {count} 个 MeshRenderer 应用 {mat.name}。", this);
    }

    private static bool IsUnderEdgeLayer(Transform t)
    {
        while (t != null)
        {
            if (t.name == EdgeLayerName)
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    private Material ResolveMaterial()
    {
        if (hologramMaterial != null)
        {
            return hologramMaterial;
        }

#if UNITY_EDITOR
        hologramMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        if (hologramMaterial != null)
        {
            return hologramMaterial;
        }
#endif

        return null;
    }
}
