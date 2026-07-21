using UnityEngine;

/// <summary>
/// 零部件名称 3D 文本：挂在零件上，文字位于模型上方并始终朝向相机。
/// </summary>
[DisallowMultipleComponent]
public class PartNameLabelBillboard : MonoBehaviour
{
    [Tooltip("相对零件本地坐标的偏移；默认在模型上方（会叠加包围盒高度）")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("是否按 Renderer 包围盒顶面自动抬高；关闭则仅使用 Offset")]
    [SerializeField] private bool _useBoundsTop = true;

    [Tooltip("文字内容；留空则使用物体名")]
    [SerializeField] private string _labelText;

    [SerializeField] private float _characterSize = 0.08f;
    [SerializeField] private int _fontSize = 48;
    [SerializeField] private FontStyle _fontStyle = FontStyle.Bold;
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private TextAnchor _anchor = TextAnchor.LowerCenter;
    [SerializeField] private TextAlignment _alignment = TextAlignment.Center;

    [Tooltip("留空则使用 Camera.main")]
    [SerializeField] private Camera _targetCamera;

    private Transform _labelRoot;
    private TextMesh _textMesh;
    /// <summary>模型顶面本地坐标（不含文字）；缓存避免重复计算把文字自身算进包围盒。</summary>
    private Vector3? _cachedModelLocalTop;

    private void Awake()
    {
        EnsureLabel();
        RefreshLabelContent();
        RefreshLabelPosition();
    }

    private void OnEnable()
    {
        EnsureLabel();
        RefreshLabelContent();
        RefreshLabelPosition();
    }

    private void LateUpdate()
    {
        if (_labelRoot == null)
        {
            return;
        }

        // 仅旋转文字，不改位置；与相机同向再绕 Y 转 180°，保证镜头中正向可读。
        FaceCamera();
    }

    /// <summary>用物体名刷新文本，并重建位置。</summary>
    public void SetupFromOwnerName()
    {
        _labelText = gameObject.name;
        InvalidateModelBoundsCache();
        EnsureLabel();
        RefreshLabelContent();
        RefreshLabelPosition();
    }

    /// <summary>由 Generator 写入偏移配置。</summary>
    public void ApplyOffsetSettings(Vector3 offset, bool useBoundsTop)
    {
        _offset = offset;
        _useBoundsTop = useBoundsTop;
        InvalidateModelBoundsCache();
        EnsureLabel();
        RefreshLabelPosition();
    }

    /// <summary>由 Generator 写入字号与粗细（不改位置，避免高度被反复抬高）。</summary>
    public void ApplyStyleSettings(float characterSize, int fontSize, FontStyle fontStyle, Color color)
    {
        _characterSize = Mathf.Max(0.001f, characterSize);
        _fontSize = Mathf.Max(1, fontSize);
        _fontStyle = fontStyle;
        _color = color;
        EnsureLabel();
        ApplyTextMeshStyle();
    }

    public void InvalidateModelBoundsCache()
    {
        _cachedModelLocalTop = null;
    }

    /// <summary>仅刷新显示文字，不改位置。</summary>
    public void RefreshLabelTextOnly()
    {
        _labelText = gameObject.name;
        EnsureLabel();
        RefreshLabelContent();
    }

    /// <summary>只开关文字子物体，不影响零件本体显隐。</summary>
    public void SetLabelVisible(bool visible)
    {
        EnsureLabel();
        if (_labelRoot == null)
        {
            return;
        }

        _labelRoot.gameObject.SetActive(visible);
    }

    /// <summary>销毁文字子物体（供 Generator 删除/重置）。</summary>
    public void DestroyLabelObject()
    {
        if (_labelRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(_labelRoot.gameObject);
            }
            else
#endif
            {
                Object.Destroy(_labelRoot.gameObject);
            }

            _labelRoot = null;
            _textMesh = null;
            return;
        }

        Transform existing = transform.Find("PartNameLabel");
        if (existing == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
        else
#endif
        {
            Object.Destroy(existing.gameObject);
        }
    }

    private void EnsureLabel()
    {
        if (_labelRoot != null && _textMesh != null)
        {
            return;
        }

        Transform existing = transform.Find("PartNameLabel");
        if (existing != null)
        {
            _labelRoot = existing;
            _textMesh = existing.GetComponent<TextMesh>();
            if (_textMesh == null)
            {
                _textMesh = existing.gameObject.AddComponent<TextMesh>();
            }

            return;
        }

        GameObject labelGo = new GameObject("PartNameLabel");
        labelGo.transform.SetParent(transform, false);
        _labelRoot = labelGo.transform;
        _textMesh = labelGo.AddComponent<TextMesh>();
        ApplyTextMeshStyle();
    }

    private void ApplyTextMeshStyle()
    {
        if (_textMesh == null)
        {
            return;
        }

        _textMesh.anchor = _anchor;
        _textMesh.alignment = _alignment;
        _textMesh.characterSize = _characterSize;
        _textMesh.fontSize = _fontSize;
        _textMesh.fontStyle = _fontStyle;
        _textMesh.color = _color;
    }

    private void RefreshLabelContent()
    {
        EnsureLabel();
        ApplyTextMeshStyle();
        if (_textMesh == null)
        {
            return;
        }

        _textMesh.text = string.IsNullOrWhiteSpace(_labelText) ? gameObject.name : _labelText.Trim();
    }

    private void RefreshLabelPosition()
    {
        if (_labelRoot == null)
        {
            return;
        }

        Vector3 localPos = _offset;
        if (_useBoundsTop)
        {
            localPos += GetLocalBoundsTopOffset();
        }

        _labelRoot.localPosition = localPos;
    }

    /// <summary>
    /// 取模型（不含 PartNameLabel）包围盒顶面在零件本地空间的位置。
    /// 结果会缓存；切勿把文字 Renderer 算进去，否则每次刷新高度会叠高。
    /// </summary>
    private Vector3 GetLocalBoundsTopOffset()
    {
        if (_cachedModelLocalTop.HasValue)
        {
            return _cachedModelLocalTop.Value;
        }

        bool hasBounds = false;
        Bounds worldBounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !IsModelRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            _cachedModelLocalTop = Vector3.zero;
            return Vector3.zero;
        }

        Vector3 worldTop = new Vector3(worldBounds.center.x, worldBounds.max.y, worldBounds.center.z);
        _cachedModelLocalTop = transform.InverseTransformPoint(worldTop);
        return _cachedModelLocalTop.Value;
    }

    private bool IsModelRenderer(Renderer renderer)
    {
        if (_labelRoot != null && renderer.transform.IsChildOf(_labelRoot))
        {
            return false;
        }

        if (renderer.transform.name == "PartNameLabel")
        {
            return false;
        }

        // TextMesh 自带 MeshRenderer，必须排除
        if (renderer.GetComponent<TextMesh>() != null)
        {
            return false;
        }

        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }

    private void FaceCamera()
    {
        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam == null || _labelRoot == null)
        {
            return;
        }

        // 只改文字旋转与左右镜像，不改位置与字号相关缩放。
        _labelRoot.rotation = cam.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        _labelRoot.localScale = new Vector3(-1f, 1f, 1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureLabel();
        RefreshLabelContent();
        RefreshLabelPosition();
    }
#endif
}
