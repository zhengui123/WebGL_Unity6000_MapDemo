using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 收集车辆根节点下所有带溶解属性的材质实例，并批量写入 Shader 参数。
/// 不继承 MonoBehaviour，由 CarModelChangeController 持有两个实例分别对应 RealyCar / KJ_Car。
/// </summary>
public class CarModelDissolveGroup
{
    // 使用 PropertyToID 避免每帧 SetFloat 产生字符串 GC
    public static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    public static readonly int DissolveNoiseScaleId = Shader.PropertyToID("_DissolveNoiseScale");

    private readonly List<Material> _materials = new List<Material>();

    public int MaterialCount => _materials.Count;

    /// <summary>
    /// 遍历子物体 Renderer，缓存支持 _DissolveAmount 的材质实例。
    /// 每次 CollectFrom 会清空旧列表并重新访问 renderer.materials。
    /// </summary>
    public void CollectFrom(GameObject root, bool isShareMaterial = false)
    {
        _materials.Clear();
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            // 访问 materials 会为 Renderer 创建独立实例，避免修改 sharedMaterial 影响其他物体
            Material[] materials;
            if(isShareMaterial)
            {
                materials = renderer.sharedMaterials;
            }
            else
            {
                materials = renderer.materials;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material != null && material.HasProperty(DissolveAmountId))
                {
                    _materials.Add(material);
                }
            }

            renderer.materials = materials;
        }
    }

    /// <summary>0=完全显示，1=完全溶解（与 Shader clip 逻辑一致）。</summary>
    public void SetDissolveAmount(float amount)
    {
        float value = Mathf.Clamp01(amount);
        for (int i = 0; i < _materials.Count; i++)
        {
            _materials[i].SetFloat(DissolveAmountId, value);
        }
    }

    /// <summary>仅对含 _DissolveNoiseScale 的材质生效（StandardDissolve / CarHologram 等）。</summary>
    public void SetDissolveNoiseScale(float noiseScale)
    {
        float value = Mathf.Max(0.01f, noiseScale);
        for (int i = 0; i < _materials.Count; i++)
        {
            Material material = _materials[i];
            if (material != null && material.HasProperty(DissolveNoiseScaleId))
            {
                material.SetFloat(DissolveNoiseScaleId, value);
            }
        }
    }
}
