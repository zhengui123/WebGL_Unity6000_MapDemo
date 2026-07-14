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

    private Renderer[] _renderers;
    private Collider[] _managedColliders;
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

        var candidates = new System.Collections.Generic.List<Renderer>();
        // 自身 + 全部子层级（含未激活子物体）
        GetComponentsInChildren(true, candidates);

        var list = new System.Collections.Generic.List<Renderer>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            Renderer renderer = candidates[i];
            if (IsPlateMapProvinceTechRenderer(renderer))
            {
                list.Add(renderer);
            }
        }

        _renderers = list.ToArray();
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

    private void EnsurePickColliders()
    {
        CacheRenderers();

        var colliderSet = new HashSet<Collider>();
        TryRegisterOrCreateCollider(gameObject, colliderSet);

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
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
        CollectExistingColliders(gameObject, colliderSet);

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
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
