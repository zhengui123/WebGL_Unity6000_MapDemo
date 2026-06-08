using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 收集车辆根节点下所有带 _DissolveAmount 的材质，并统一设置溶解值。
/// </summary>
public class CarModelDissolveGroup
{
    public static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    public static readonly int DissolveNoiseScaleId = Shader.PropertyToID("_DissolveNoiseScale");

    private readonly List<Material> _materials = new List<Material>();

    public int MaterialCount => _materials.Count;

    /// <summary>遍历子物体 Renderer，缓存支持溶解的材质实例。</summary>
    public void CollectFrom(GameObject root)
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
            Material[] materials = renderer.materials;
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

    public void SetDissolveAmount(float amount)
    {
        float value = Mathf.Clamp01(amount);
        for (int i = 0; i < _materials.Count; i++)
        {
            _materials[i].SetFloat(DissolveAmountId, value);
        }
    }

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
