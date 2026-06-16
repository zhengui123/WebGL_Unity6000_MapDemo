using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图上可点击的显示模块（挂在地市/区域 mesh 节点上）。
/// 需有 Collider 供 <see cref="PlateMapDisplayController"/> 射线拾取。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapDisplayModule : MonoBehaviour
{
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");

    [SerializeField] private string _displayName;
    [Tooltip("点击后相机局部 Y，越小越近")]
    [SerializeField] private float _focusCameraLocalY = 650f;
    [SerializeField] private bool _autoAddMeshColliderIfMissing = true;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private float _currentAlpha = 1f;
    private float _currentEmissionIntensity;
    private float _baseEmissionIntensity = 2.2f;
    private Tween _alphaTween;
    private Tween _emissionTween;

    /// <summary>显示名（留空则用 GameObject 名）。</summary>
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? gameObject.name : _displayName;

    /// <summary>模块匹配 key，默认与场景物体名一致。</summary>
    public string ModuleKey => gameObject.name;

    /// <summary>点击拉近时相机目标局部 Y。</summary>
    public float FocusCameraLocalY => _focusCameraLocalY;

    public float CurrentAlpha => _currentAlpha;

    public float BaseEmissionIntensity => _baseEmissionIntensity;

    public float CurrentEmissionIntensity => _currentEmissionIntensity;

    private MeshCollider meshCollider;
    private void Awake()
    {
        if (_autoAddMeshColliderIfMissing)
        {
            EnsurePickCollider();
        }

        CacheRenderers();
        CacheBaseEmissionIntensity();
        ApplyAlphaImmediate(_currentAlpha);
        ApplyEmissionIntensityImmediate(_currentEmissionIntensity);
    }


    public void ChangeColliderState()
    {
        if(_propertyBlock.GetFloat("_Alpha") >= 1)
        {
            meshCollider.enabled = true;
        }
        else if(meshCollider.enabled)
        {
            meshCollider.enabled = false;
        }
    }

    private void OnDestroy()
    {
        KillAlphaTween();
        KillEmissionTween();
    }

    /// <summary>模块在世界空间下的包围盒（用于屏幕居中）。</summary>
    public Bounds GetWorldBounds()
    {
        CacheRenderers();
        if (_renderers == null || _renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one * 0.1f);
        }

        Bounds bounds = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                bounds.Encapsulate(_renderers[i].bounds);
            }
        }

        return bounds;
    }

    public void TweenAlpha(float targetAlpha, float duration, Ease ease = Ease.InOutQuad)
    {
        KillAlphaTween();
        targetAlpha = Mathf.Clamp01(targetAlpha);

        if (duration <= 0f)
        {
            ApplyAlphaImmediate(targetAlpha);
            return;
        }

        _alphaTween = DOTween.To(() => _currentAlpha, ApplyAlphaImmediate, targetAlpha, duration)
            .SetEase(ease)
            .SetTarget(this);
    }

    public void ApplyAlphaImmediate(float alpha)
    {
        _currentAlpha = Mathf.Clamp01(alpha);
        ApplyRendererProperty(AlphaId, _currentAlpha);
    }

    public void KillAlphaTween()
    {
        if (_alphaTween != null && _alphaTween.IsActive())
        {
            _alphaTween.Kill();
        }

        _alphaTween = null;
    }

    public void TweenEmissionIntensity(float targetIntensity, float duration, Ease ease = Ease.InOutQuad)
    {
        KillEmissionTween();
        targetIntensity = Mathf.Max(0f, targetIntensity);

        if (duration <= 0f)
        {
            ApplyEmissionIntensityImmediate(targetIntensity);
            return;
        }

        _emissionTween = DOTween.To(() => _currentEmissionIntensity, ApplyEmissionIntensityImmediate, targetIntensity, duration)
            .SetEase(ease)
            .SetTarget(this);
    }

    public void ApplyEmissionIntensityImmediate(float intensity)
    {
        _currentEmissionIntensity = Mathf.Max(0f, intensity);
        ApplyRendererProperty(EmissionIntensityId, _currentEmissionIntensity);
    }

    public void RestoreEmissionIntensity(float duration, Ease ease = Ease.InOutQuad)
    {
        TweenEmissionIntensity(_baseEmissionIntensity, duration, ease);
    }

    public void KillEmissionTween()
    {
        if (_emissionTween != null && _emissionTween.IsActive())
        {
            _emissionTween.Kill();
        }

        _emissionTween = null;
    }

    private void CacheBaseEmissionIntensity()
    {
        CacheRenderers();
        if (_renderers == null || _renderers.Length == 0)
        {
            _baseEmissionIntensity = 2.2f;
            _currentEmissionIntensity = _baseEmissionIntensity;
            return;
        }

        Material mat = _renderers[0].sharedMaterial;
        if (mat != null && mat.HasProperty(EmissionIntensityId))
        {
            _baseEmissionIntensity = mat.GetFloat(EmissionIntensityId);
        }
        else
        {
            _baseEmissionIntensity = 2.2f;
        }

        _currentEmissionIntensity = _baseEmissionIntensity;
    }

    private void ApplyRendererProperty(int propertyId, float value)
    {
        CacheRenderers();
        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null)
            {
                continue;
            }

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(propertyId, value);
            r.SetPropertyBlock(_propertyBlock);
        }

        if (propertyId == AlphaId)
        {
            ChangeColliderState();
        }
    }

    private void CacheRenderers()
    {
        if (_renderers != null && _renderers.Length > 0)
        {
            return;
        }

        var list = new System.Collections.Generic.List<Renderer>();
        GetComponentsInChildren(false, list);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Material mat = list[i].sharedMaterial;
            if (mat == null || mat.shader == null || mat.shader.name != "Custom/PlateMapProvinceTech")
            {
                list.RemoveAt(i);
            }
        }

        _renderers = list.ToArray();
    }

    private void EnsurePickCollider()
    {
        if (GetComponent<Collider>() != null)
        {
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            return;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = renderer.bounds.center - transform.position;
            box.size = renderer.bounds.size;
        }
    }
}
