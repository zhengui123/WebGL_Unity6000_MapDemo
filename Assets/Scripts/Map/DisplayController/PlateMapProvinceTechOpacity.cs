using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 驱动 <see cref="Shader"/> Custom/PlateMapProvinceTech 的 <c>_Alpha</c>，用于整体显隐（0 隐藏，1 显示）。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapProvinceTechOpacity : MonoBehaviour
{
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("目标渲染器（留空则自动收集子级 MeshRenderer / SkinnedMeshRenderer）")]
    [SerializeField] private Renderer[] _renderers;

    [Header("整体透明度")]
    [SerializeField, Range(0f, 1f)] private float _overallAlpha = 1f;

    [Header("可选：UI 进度条")]
    [SerializeField] private Slider _uiSlider;
    [SerializeField] private bool _syncSliderOnEnable = true;

    private MaterialPropertyBlock _propertyBlock;

    public float OverallAlpha
    {
        get => _overallAlpha;
        set => SetOverallAlpha(value);
    }

    private void OnEnable()
    {
        if (_uiSlider != null)
        {
            if (_syncSliderOnEnable)
            {
                _uiSlider.SetValueWithoutNotify(_overallAlpha);
            }

            _uiSlider.onValueChanged.AddListener(SetOverallAlpha);
        }

        ApplyAlpha();
    }

    private void OnDisable()
    {
        if (_uiSlider != null)
        {
            _uiSlider.onValueChanged.RemoveListener(SetOverallAlpha);
        }
    }

    private void OnValidate()
    {
        _overallAlpha = Mathf.Clamp01(_overallAlpha);
        if (Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        ApplyAlpha();
    }

    /// <summary>供 UI Slider OnValueChanged 或动画曲线调用。</summary>
    public void SetOverallAlpha(float alpha)
    {
        _overallAlpha = Mathf.Clamp01(alpha);
        if (_uiSlider != null && !Mathf.Approximately(_uiSlider.value, _overallAlpha))
        {
            _uiSlider.SetValueWithoutNotify(_overallAlpha);
        }

        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        Renderer[] targets = ResolveRenderers();
        if (targets == null || targets.Length == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();

        for (int i = 0; i < targets.Length; i++)
        {
            Renderer r = targets[i];
            if (r == null)
            {
                continue;
            }

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(AlphaId, _overallAlpha);
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    private Renderer[] ResolveRenderers()
    {
        if (_renderers != null && _renderers.Length > 0)
        {
            return _renderers;
        }

        var list = new System.Collections.Generic.List<Renderer>();
        GetComponentsInChildren(true, list);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Material mat = list[i].sharedMaterial;
            if (mat == null || mat.shader == null ||
                mat.shader.name != "Custom/PlateMapProvinceTech")
            {
                list.RemoveAt(i);
            }
        }

        return list.ToArray();
    }
}
