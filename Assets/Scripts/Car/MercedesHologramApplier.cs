using UnityEngine;

/// <summary>
/// 运行时将所有子网格 Renderer 替换为全息材质（Car 场景备用，FBX Remap 未生效时兜底）。
/// </summary>
[DisallowMultipleComponent]
public class MercedesHologramApplier : MonoBehaviour
{
    private const string DefaultMaterialPath = "Assets/Materials/Car/M_MercedesHologram.mat";

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

        int count = 0;
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
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
