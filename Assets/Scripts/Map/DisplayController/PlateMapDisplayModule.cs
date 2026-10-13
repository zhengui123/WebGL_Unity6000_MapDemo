using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 板块地图上可点击的显示模块（挂在地市/区域 mesh 节点或其父节点上）。
/// 材质透明度/发光与拾取碰撞体均作用于<strong>自身及全部子物体</strong>（符合 PlateMapProvinceTech 的 Renderer 及其 Mesh）。
/// 需有 Collider 供 <see cref="PlateMapDisplayController"/> 射线拾取（可通过子物体 Collider + GetComponentInParent 命中本模块）。
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

    [Header("边界线")]
    [Tooltip("边界线 Renderer 运行时固定 _EmissionIntensity，与板块高亮基准分离")]
    [SerializeField] private float _lineEmissionIntensity = 3f;

    /// <summary>全部 Tech Renderer（板块填充 + 边界线），用于 Alpha / 发光。</summary>
    private Renderer[] _renderers;
    /// <summary>参与包围盒/拾取的 Renderer（排除仅显示子物体，如台湾 default）。</summary>
    private Renderer[] _rangeBoundsRenderers;
    /// <summary>板块填充（不含边界线），用于高亮发光基准。</summary>
    private Renderer[] _fillRenderers;
    /// <summary>边界线（*_边界线 / 材质名含 Line）。</summary>
    private Renderer[] _lineRenderers;
    private Collider[] _managedColliders;
    private PlateMapDisplayRangeExclude _rangeExclude;
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

    private void Awake()
    {
        CacheRenderers();

        if (_autoAddMeshColliderIfMissing)
        {
            EnsurePickColliders();
        }
        else
        {
            CacheManagedColliders();
        }

        CacheBaseEmissionIntensity();
        ApplyAlphaImmediate(_currentAlpha);
        ApplyEmissionIntensityImmediate(_currentEmissionIntensity);
    }

    private void OnEnable()
    {
        // AllPlateMap 显隐后 PropertyBlock 可能丢失，按逻辑 alpha 重刷材质
        ApplyAlphaImmediate(_currentAlpha);
        ApplyEmissionIntensityImmediate(_currentEmissionIntensity);
    }

    public void ChangeColliderState()
    {
        if (_managedColliders == null || _managedColliders.Length == 0)
        {
            return;
        }

        bool enabled = _currentAlpha >= 1f;
        for (int i = 0; i < _managedColliders.Length; i++)
        {
            Collider collider = _managedColliders[i];
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }
    }

    private void OnDestroy()
    {
        KillAlphaTween();
        KillEmissionTween();
    }

    /// <summary>模块在世界空间下的包围盒（用于屏幕居中；已排除仅显示子物体）。</summary>
    public Bounds GetWorldBounds()
    {
        CacheRenderers();
        Renderer[] source = _rangeBoundsRenderers != null && _rangeBoundsRenderers.Length > 0
            ? _rangeBoundsRenderers
            : _renderers;
        if (source == null || source.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one * 0.1f);
        }

        Bounds bounds = source[0].bounds;
        for (int i = 1; i < source.Length; i++)
        {
            if (source[i] != null)
            {
                bounds.Encapsulate(source[i].bounds);
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
        ApplyToRenderers(_renderers, AlphaId, _currentAlpha);
        ChangeColliderState();
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
        // 高亮/还原只改板块填充，边界线保持独立亮度
        ApplyToRenderers(_fillRenderers, EmissionIntensityId, _currentEmissionIntensity);
        ApplyLineEmissionIntensity();
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
        // 必须从板块材质读基准，禁止用边界线（层级常排在前面）污染高亮基准
        Renderer fill = FindFirstRenderer(_fillRenderers);
        if (fill == null)
        {
            _baseEmissionIntensity = 2.2f;
            _currentEmissionIntensity = _baseEmissionIntensity;
            return;
        }

        Material mat = fill.sharedMaterial;
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

    private void ApplyLineEmissionIntensity()
    {
        ApplyToRenderers(_lineRenderers, EmissionIntensityId, Mathf.Max(0f, _lineEmissionIntensity));
    }

    private void ApplyToRenderers(Renderer[] renderers, int propertyId, float value)
    {
        CacheRenderers();
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(propertyId, value);
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    private void CacheRenderers()
    {
        if (_renderers != null && _renderers.Length > 0)
        {
            return;
        }

        ResolveRangeExclude();

        var candidates = new List<Renderer>();
        // 自身 + 全部子层级（含未激活子物体）
        GetComponentsInChildren(true, candidates);

        var all = new List<Renderer>(candidates.Count);
        var rangeBounds = new List<Renderer>(candidates.Count);
        var fill = new List<Renderer>(candidates.Count);
        var line = new List<Renderer>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            Renderer renderer = candidates[i];
            if (!IsPlateMapProvinceTechRenderer(renderer))
            {
                continue;
            }

            all.Add(renderer);
            if (!IsExcludedFromRange(renderer))
            {
                rangeBounds.Add(renderer);
            }

            if (IsBoundaryLineRenderer(renderer))
            {
                line.Add(renderer);
            }
            else
            {
                fill.Add(renderer);
            }
        }

        _renderers = all.ToArray();
        _rangeBoundsRenderers = rangeBounds.ToArray();
        _fillRenderers = fill.ToArray();
        _lineRenderers = line.ToArray();
    }

    private void ResolveRangeExclude()
    {
        if (_rangeExclude == null)
        {
            _rangeExclude = GetComponent<PlateMapDisplayRangeExclude>();
        }

        _rangeExclude?.EnsureReady();
    }

    private bool IsExcludedFromRange(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        ResolveRangeExclude();
        return _rangeExclude != null && _rangeExclude.IsExcluded(renderer.transform);
    }

    private static Renderer FindFirstRenderer(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                return renderers[i];
            }
        }

        return null;
    }

    private static bool IsPlateMapProvinceTechRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Material mat = renderer.sharedMaterial;
        return mat != null &&
               mat.shader != null &&
               mat.shader.name == "Custom/PlateMapProvinceTech";
    }

    /// <summary>边界线：物体名含「边界线」，或材质名含 Line（如 M_SdMapProvinceTech Line）。</summary>
    private static bool IsBoundaryLineRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string goName = renderer.gameObject.name;
        if (!string.IsNullOrEmpty(goName) && goName.IndexOf("边界线", System.StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        Material mat = renderer.sharedMaterial;
        if (mat == null || string.IsNullOrEmpty(mat.name))
        {
            return false;
        }

        // Unity 实例材质名可能带 " (Instance)"
        string matName = mat.name;
        return matName.IndexOf("Line", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void EnsurePickColliders()
    {
        CacheRenderers();

        var colliderSet = new HashSet<Collider>();
        if (!IsExcludedFromRange(GetComponent<Renderer>()))
        {
            TryRegisterOrCreateCollider(gameObject, colliderSet);
        }

        Renderer[] source = _rangeBoundsRenderers != null && _rangeBoundsRenderers.Length > 0
            ? _rangeBoundsRenderers
            : _renderers;
        if (source != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                Renderer renderer = source[i];
                if (renderer != null)
                {
                    TryRegisterOrCreateCollider(renderer.gameObject, colliderSet);
                }
            }
        }

        _managedColliders = ToArray(colliderSet);
        ChangeColliderState();
    }

    private void CacheManagedColliders()
    {
        CacheRenderers();

        var colliderSet = new HashSet<Collider>();
        if (!IsExcludedFromRange(GetComponent<Renderer>()))
        {
            CollectExistingColliders(gameObject, colliderSet);
        }

        Renderer[] source = _rangeBoundsRenderers != null && _rangeBoundsRenderers.Length > 0
            ? _rangeBoundsRenderers
            : _renderers;
        if (source != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                Renderer renderer = source[i];
                if (renderer != null)
                {
                    CollectExistingColliders(renderer.gameObject, colliderSet);
                }
            }
        }

        _managedColliders = ToArray(colliderSet);
    }

    private static void CollectExistingColliders(GameObject target, HashSet<Collider> colliders)
    {
        if (target == null)
        {
            return;
        }

        Collider[] existing = target.GetComponents<Collider>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
            {
                colliders.Add(existing[i]);
            }
        }
    }

    private static void TryRegisterOrCreateCollider(GameObject target, HashSet<Collider> colliders)
    {
        if (target == null)
        {
            return;
        }

        Collider existing = target.GetComponent<Collider>();
        if (existing != null)
        {
            colliders.Add(existing);
            return;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            MeshCollider meshCollider = target.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            colliders.Add(meshCollider);
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        BoxCollider boxCollider = target.AddComponent<BoxCollider>();
        Bounds worldBounds = renderer.bounds;
        boxCollider.center = target.transform.InverseTransformPoint(worldBounds.center);
        boxCollider.size = worldBounds.size;
        colliders.Add(boxCollider);
    }

    private static Collider[] ToArray(HashSet<Collider> colliders)
    {
        if (colliders == null || colliders.Count == 0)
        {
            return System.Array.Empty<Collider>();
        }

        var array = new Collider[colliders.Count];
        colliders.CopyTo(array);
        return array;
    }
}
