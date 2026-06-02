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

    [SerializeField] private string _displayName;
    [Tooltip("点击后相机局部 Y，越小越近")]
    [SerializeField] private float _focusCameraLocalY = 650f;
    [SerializeField] private bool _autoAddMeshColliderIfMissing = true;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private float _currentAlpha = 1f;
    private Tween _alphaTween;

    /// <summary>显示名（留空则用 GameObject 名）。</summary>
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? gameObject.name : _displayName;

    /// <summary>点击拉近时相机目标局部 Y。</summary>
    public float FocusCameraLocalY => _focusCameraLocalY;

    public float CurrentAlpha => _currentAlpha;

    private void Awake()
    {
        if (_autoAddMeshColliderIfMissing)
        {
            EnsurePickCollider();
        }

        CacheRenderers();
        ApplyAlphaImmediate(_currentAlpha);
    }

    private void OnDestroy()
    {
        KillAlphaTween();
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
            _propertyBlock.SetFloat(AlphaId, _currentAlpha);
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    public void KillAlphaTween()
    {
        if (_alphaTween != null && _alphaTween.IsActive())
        {
            _alphaTween.Kill();
        }

        _alphaTween = null;
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
            var meshCollider = gameObject.AddComponent<MeshCollider>();
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
