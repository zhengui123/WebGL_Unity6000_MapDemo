using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 攻击路径：使用 <see cref="LineRenderer"/> 绘制折线，通过材质贴图 Offset 实现滚动效果。
/// 支持通过 <see cref="Add"/> 同时显示多条「起点零件 → 终点零件」连线。
/// </summary>
public class AttackPathController : MonoBehaviour
{
    [FormerlySerializedAs("lineTransform")]
    [SerializeField] private Transform _lineTransform;
    [Tooltip("路径本地坐标参照父节点；留空则使用 _lineTransform 的 parent")]
    [SerializeField] private Transform _pathLocalRoot;
    [Tooltip("零件名对照来源；留空则运行时自动查找")]
    [SerializeField] private GridLine _gridLine;
    [Tooltip("材质 Offset X 滚动速度（单位/秒）；与线长无关，所有线速度一致")]
    [SerializeField] private float _speed = 8f;
    [Tooltip("勾选后路径首尾相连（闭合路径）")]
    [SerializeField] private bool _closePath;
    [Tooltip("勾选后贴图 Offset 动画循环播放")]
    [SerializeField] private bool _loopAnimation;

    [Header("线条样式")]
    [Tooltip("LineRenderer 线宽（widthMultiplier）；Play Mode 下修改会实时生效")]
    [SerializeField] private float _lineWidth = 0.26f;
    [Tooltip("首尾是否圆头；关闭=平头（numCapVertices=0），开启=圆头")]
    [SerializeField] private bool _useRoundedCaps;
    [Tooltip("圆头细分顶点数量（仅勾选圆头时生效）")]
    [SerializeField] private int _roundedCapVertices = 4;

    [Header("材质滚动动画")]
    [Tooltip("留空则从 LineRenderer 材质获取并实例化")]
    [SerializeField] private Material _lineMaterial;
    [Tooltip("单次滚动 MainTex Offset X 的绝对值；方向由「起点→终点」固定为负向 Offset")]
    [SerializeField] private float _endTextureOffsetX = 1f;
    [Tooltip("Offset 速率 = 速度 × 该系数；与线长无关")]
    [SerializeField] private float _endOffsetSpeedCoefficient = 1f;

    private readonly List<LineInstance> _addedLines = new List<LineInstance>();
    private LineInstance _legacyLine;

    /// <summary>单条攻击路径实例（模板或克隆体）。</summary>
    private sealed class LineInstance
    {
        public Transform LineTransform;
        public LineRenderer LineRenderer;
        public Material RuntimeMaterial;
        public Tween MaterialTween;
        public bool IsTemplate;
        public List<Vector3> LocalPath;
        public float PathLength;
        public bool HasActivePath;
    }

    private void Awake()
    {
        if (IsClonedLineObject())
        {
            return;
        }

        if (_lineTransform != null)
        {
            _legacyLine = CreateLineInstance(_lineTransform.gameObject, isTemplate: true);
            SetLineVisible(_legacyLine, false);
        }
    }

    private void OnDisable()
    {
        if (IsClonedLineObject())
        {
            return;
        }

        StopPath();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (IsClonedLineObject())
        {
            return;
        }

        _lineWidth = Mathf.Max(0.001f, _lineWidth);
        _roundedCapVertices = Mathf.Max(1, _roundedCapVertices);
        ApplyLineStyleToAllInstances();

        if (!Application.isPlaying)
        {
            return;
        }

        RefreshMaterialScrollAnimations();
    }
#endif

    /// <summary>当前滚动速度（Inspector / 代码均可改，改后会刷新正在播放的线条）。</summary>
    public float ScrollSpeed
    {
        get => _speed;
        set => ApplyMaterialScrollSettings(speed: value);
    }

    /// <summary>单次滚动 MainTex Offset X 增量。</summary>
    public float ScrollTextureOffsetX
    {
        get => _endTextureOffsetX;
        set => ApplyMaterialScrollSettings(textureOffsetX: value);
    }

    /// <summary>Offset 增速系数。</summary>
    public float ScrollOffsetSpeedCoefficient
    {
        get => _endOffsetSpeedCoefficient;
        set => ApplyMaterialScrollSettings(offsetSpeedCoefficient: value);
    }

    /// <summary>是否循环滚动。</summary>
    public bool ScrollLoopAnimation
    {
        get => _loopAnimation;
        set => ApplyMaterialScrollSettings(loopAnimation: value);
    }

    /// <summary>线条宽度。</summary>
    public float LineWidth
    {
        get => _lineWidth;
        set => ApplyLineWidth(value);
    }

    /// <summary>更新线宽并应用到所有活跃线条。</summary>
    public void ApplyLineWidth(float width)
    {
        _lineWidth = Mathf.Max(0.001f, width);
        ApplyLineStyleToAllInstances();
    }

    /// <summary>是否使用圆头；false 为平头。</summary>
    public bool UseRoundedCaps
    {
        get => _useRoundedCaps;
        set
        {
            _useRoundedCaps = value;
            ApplyLineStyleToAllInstances();
        }
    }

    /// <summary>
    /// 更新材质滚动参数并立即刷新所有正在显示的线条动画。
    /// </summary>
    public void ApplyMaterialScrollSettings(
        float? speed = null,
        float? textureOffsetX = null,
        float? offsetSpeedCoefficient = null,
        bool? loopAnimation = null)
    {
        if (speed.HasValue)
        {
            _speed = Mathf.Max(0.01f, speed.Value);
        }

        if (textureOffsetX.HasValue)
        {
            _endTextureOffsetX = Mathf.Max(0f, textureOffsetX.Value);
        }

        if (offsetSpeedCoefficient.HasValue)
        {
            _endOffsetSpeedCoefficient = Mathf.Max(0.01f, offsetSpeedCoefficient.Value);
        }

        if (loopAnimation.HasValue)
        {
            _loopAnimation = loopAnimation.Value;
        }

        if (Application.isPlaying)
        {
            RefreshMaterialScrollAnimations();
        }
    }

    /// <summary>按当前 Inspector / 属性值，重播所有活跃线条的材质滚动动画。</summary>
    public void RefreshMaterialScrollAnimations()
    {
        if (!Application.isPlaying || IsClonedLineObject())
        {
            return;
        }

        ApplyLineStyleToAllInstances();
        RefreshLineInstanceScroll(_legacyLine);
        for (int i = 0; i < _addedLines.Count; i++)
        {
            RefreshLineInstanceScroll(_addedLines[i]);
        }
    }

    /// <summary>停止材质动画并隐藏所有线条（供外部过渡控制器调用）。</summary>
    public void StopPath()
    {
        KillLineInstance(_legacyLine);
        ClearLines();
    }

    /// <summary>按零件名添加一条起点→终点攻击路径；可同时存在多条。</summary>
    public bool Add(string startPartName, string endPartName)
    {
        if (string.IsNullOrWhiteSpace(startPartName) || string.IsNullOrWhiteSpace(endPartName))
        {
            Debug.LogWarning("[AttackPathController] Add 失败：起点或终点零件名为空。");
            return false;
        }

        GridLine gridLine = ResolveGridLine();
        if (gridLine == null)
        {
            Debug.LogError("[AttackPathController] Add 失败：未找到 GridLine。");
            return false;
        }

        if (!gridLine.TryGetPartTransform(startPartName.Trim(), out Transform startPart)
            || !gridLine.TryGetPartTransform(endPartName.Trim(), out Transform endPart))
        {
            Debug.LogError($"[AttackPathController] Add 失败：未找到零件 Transform（{startPartName} → {endPartName}）。");
            return false;
        }

        LineInstance instance = CreateClonedLineInstance();
        if (instance == null)
        {
            return false;
        }

        PlayPathSettings settings = ResolvePlayPathSettings(null, false, _loopAnimation);
        PlayPathOnInstance(
            instance,
            new List<Transform> { startPart, endPart },
            settings);
        _addedLines.Add(instance);
        return true;
    }

    /// <summary>清空通过 <see cref="Add"/> 创建的全部连线。</summary>
    public void ClearLines()
    {
        for (int i = _addedLines.Count - 1; i >= 0; i--)
        {
            DestroyLineInstance(_addedLines[i]);
        }

        _addedLines.Clear();
    }

    /// <summary>清空后批量添加攻击链路边；返回成功添加条数。</summary>
    public int ApplyPartLinks(IReadOnlyList<AttackChainPathEntry> entries)
    {
        ClearLines();
        if (entries == null || entries.Count == 0)
        {
            return 0;
        }

        int added = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            AttackChainPathEntry entry = entries[i];
            if (Add(entry.StartPartName, entry.EndPartName))
            {
                added++;
            }
        }

        return added;
    }

    /// <summary>使用指定路点 Transform 列表播放攻击路径（兼容旧逻辑，会清空 Add 创建的连线）。</summary>
    public void PlayPath(
        List<Transform> waypoints,
        float? speed = null,
        bool? closePath = null,
        bool? loopAnimation = null)
    {
        ClearLines();
        PlayPathSettings settings = ResolvePlayPathSettings(speed, closePath, loopAnimation);
        PlayPathOnInstance(_legacyLine, waypoints, settings);
    }

    /// <summary>使用世界坐标路点列表播放攻击路径。</summary>
    public void PlayPath(
        List<Vector3> pathPositions,
        float? speed = null,
        bool? closePath = null,
        bool? loopAnimation = null)
    {
        ClearLines();
        PlayPathSettings settings = ResolvePlayPathSettings(speed, closePath, loopAnimation);
        PlayPathOnInstance(_legacyLine, BuildPathFromPositions(pathPositions, settings.ClosePath), settings);
    }

    private void PlayPathOnInstance(
        LineInstance instance,
        List<Transform> waypoints,
        PlayPathSettings settings)
    {
        if (instance == null)
        {
            return;
        }

        PlayPathOnInstance(instance, BuildPathFromTransforms(waypoints, settings.ClosePath), settings);
    }

    private PlayPathSettings ResolvePlayPathSettings(float? speed, bool? closePath, bool? loopAnimation)
    {
        return new PlayPathSettings
        {
            Speed = speed ?? _speed,
            ClosePath = closePath ?? _closePath,
            LoopAnimation = loopAnimation ?? _loopAnimation
        };
    }

    private struct PlayPathSettings
    {
        public float Speed;
        public bool ClosePath;
        public bool LoopAnimation;
    }

    /// <summary>设置 LineRenderer 顶点并启动材质 Offset 滚动。</summary>
    private void PlayPathOnInstance(LineInstance instance, List<Vector3> path, PlayPathSettings settings)
    {
        KillLineInstance(instance, hideLine: false);

        Transform pathLocalSpace = PathLocalSpace;
        if (instance.LineTransform == null
            || instance.LineRenderer == null
            || pathLocalSpace == null
            || path == null
            || path.Count < 2
            || settings.Speed <= 0f)
        {
            SetLineVisible(instance, false);
            return;
        }

        instance.LineTransform.localPosition = Vector3.zero;
        instance.LineTransform.localRotation = Quaternion.identity;
        ConfigureLineRenderer(instance, path);
        ApplyStretchFillAndAlignStart(instance);
        SetLineVisible(instance, true);

        float pathLength = CalculatePathWorldLength(path, pathLocalSpace);
        if (pathLength <= 0f)
        {
            SetLineVisible(instance, false);
            return;
        }

        StartMaterialScrollAnimation(instance, settings);
        instance.LocalPath = new List<Vector3>(path);
        instance.PathLength = pathLength;
        instance.HasActivePath = true;
    }

    private void RefreshLineInstanceScroll(LineInstance instance)
    {
        if (instance == null
            || !instance.HasActivePath
            || instance.LocalPath == null
            || instance.LocalPath.Count < 2
            || instance.PathLength <= 0f)
        {
            return;
        }

        PlayPathSettings settings = ResolvePlayPathSettings(null, null, null);
        ApplyStretchFillAndAlignStart(instance);
        StartMaterialScrollAnimation(instance, settings);
    }

    /// <summary>
    /// Stretch 模式下一张贴图铺满整线（TilingX=1），Offset=0 使贴图边缘对齐起点，
    /// 再沿负 Offset X 滚动（起点→终点）。滚动速率与线长无关，各线速度一致。
    /// </summary>
    private void StartMaterialScrollAnimation(LineInstance instance, PlayPathSettings settings)
    {
        if (_endTextureOffsetX <= 0f || _endOffsetSpeedCoefficient <= 0f || settings.Speed <= 0f)
        {
            return;
        }

        Material material = GetOrCreateRuntimeMaterial(instance);
        if (material == null)
        {
            return;
        }

        // Offset 单位/秒恒定，不随线长变化，保证长短线滚动速度一致。
        float offsetUnitsPerSecond = settings.Speed * _endOffsetSpeedCoefficient;
        float duration = _endTextureOffsetX / offsetUnitsPerSecond;
        if (duration <= 0f)
        {
            return;
        }

        // Stretch + UV 从起点→终点增大时，负向 Offset 使图案向终点流动。
        Vector2 startOffset = material.mainTextureOffset;
        Vector2 targetOffset = new Vector2(startOffset.x - _endTextureOffsetX, startOffset.y);

        instance.MaterialTween?.Kill();
        Tweener tween = material
            .DOOffset(targetOffset, duration)
            .SetEase(Ease.Linear);

        if (settings.LoopAnimation)
        {
            tween.SetLoops(-1, LoopType.Restart);
        }

        instance.MaterialTween = tween;
    }

    private void ConfigureLineRenderer(LineInstance instance, IReadOnlyList<Vector3> localPath)
    {
        Debug.Log("-----------!!!!!!!!!!!!");
        LineRenderer lineRenderer = instance.LineRenderer;
        lineRenderer.useWorldSpace = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        ApplyCapStyle(lineRenderer);
        lineRenderer.positionCount = localPath.Count;
        for (int i = 0; i < localPath.Count; i++)
        {
            lineRenderer.SetPosition(i, localPath[i]);
        }
    }

    /// <summary>Stretch + TilingX=1 铺满整线，Offset X=0 对齐起点贴图边缘。</summary>
    private void ApplyStretchFillAndAlignStart(LineInstance instance)
    {
        Material material = GetOrCreateRuntimeMaterial(instance);
        if (material == null)
        {
            return;
        }

        Vector2 scale = material.mainTextureScale;
        material.mainTextureScale = new Vector2(1f, scale.y);

        Vector2 offset = material.mainTextureOffset;
        material.mainTextureOffset = new Vector2(0f, offset.y);
    }

    private GridLine ResolveGridLine()
    {
        if (_gridLine == null)
        {
            _gridLine = FindFirstObjectByType<GridLine>();
        }

        return _gridLine;
    }

    private bool IsClonedLineObject()
    {
        return name.EndsWith("_Clone");
    }

    private LineInstance CreateClonedLineInstance()
    {
        if (_lineTransform == null)
        {
            Debug.LogError("[AttackPathController] 未配置 _lineTransform 模板。");
            return null;
        }

        GameObject clone = Instantiate(_lineTransform.gameObject, _lineTransform.parent);
        clone.name = $"{_lineTransform.name}_Clone";

        AttackPathController duplicateController = clone.GetComponent<AttackPathController>();
        if (duplicateController != null && duplicateController != this)
        {
            Destroy(duplicateController);
        }

        LineInstance instance = CreateLineInstance(clone, isTemplate: false);
        SetLineVisible(instance, false);
        return instance;
    }

    private LineInstance CreateLineInstance(GameObject lineObject, bool isTemplate)
    {
        Transform lineTransform = lineObject.transform;
        LineRenderer lineRenderer = EnsureLineRenderer(lineObject);
        return new LineInstance
        {
            LineTransform = lineTransform,
            LineRenderer = lineRenderer,
            IsTemplate = isTemplate
        };
    }

    private LineRenderer EnsureLineRenderer(GameObject lineObject)
    {
        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("[AttackPathController] AttackPathLine 缺少 LineRenderer 组件。");
            return null;
        }

        ConfigureLineRendererDefaults(lineRenderer);
        ApplyLineWidthToRenderer(lineRenderer);
        return lineRenderer;
    }

    private void ConfigureLineRendererDefaults(LineRenderer lineRenderer)
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 4;
        ApplyCapStyle(lineRenderer);
        lineRenderer.alignment = LineAlignment.View;
    }

    private void ApplyCapStyle(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.numCapVertices = _useRoundedCaps ? Mathf.Max(1, _roundedCapVertices) : 0;
    }

    private void ApplyLineWidthToRenderer(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.widthMultiplier = _lineWidth;
        ApplyCapStyle(lineRenderer);
        lineRenderer.textureMode = LineTextureMode.Stretch;
    }

    private void ApplyLineStyleToAllInstances()
    {
        ApplyLineWidthToRenderer(_legacyLine?.LineRenderer);
        if (_legacyLine?.LineTransform != null && _legacyLine.LineRenderer == null)
        {
            ApplyLineWidthToRenderer(_legacyLine.LineTransform.GetComponent<LineRenderer>());
        }

        for (int i = 0; i < _addedLines.Count; i++)
        {
            ApplyLineWidthToRenderer(_addedLines[i]?.LineRenderer);
        }

        if (_lineTransform != null)
        {
            ApplyLineWidthToRenderer(_lineTransform.GetComponent<LineRenderer>());
        }
    }

    private void DestroyLineInstance(LineInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        KillLineInstance(instance);

        if (!instance.IsTemplate && instance.LineTransform != null)
        {
            Destroy(instance.LineTransform.gameObject);
        }
    }

    private void KillLineInstance(LineInstance instance, bool hideLine = true)
    {
        if (instance == null)
        {
            return;
        }

        if (instance.MaterialTween != null && instance.MaterialTween.IsActive())
        {
            instance.MaterialTween.Kill();
        }

        instance.MaterialTween = null;
        instance.HasActivePath = false;
        instance.LocalPath = null;
        instance.PathLength = 0f;

        if (instance.LineRenderer != null)
        {
            instance.LineRenderer.positionCount = 0;
        }

        if (hideLine)
        {
            SetLineVisible(instance, false);
        }
    }

    private Transform PathLocalSpace
    {
        get
        {
            if (_pathLocalRoot != null)
            {
                return _pathLocalRoot;
            }

            return _lineTransform != null ? _lineTransform.parent : null;
        }
    }

    private Vector3 WorldToPathLocal(Vector3 worldPosition)
    {
        Transform pathLocalSpace = PathLocalSpace;
        return pathLocalSpace != null ? pathLocalSpace.InverseTransformPoint(worldPosition) : worldPosition;
    }

    private List<Vector3> BuildPathFromTransforms(List<Transform> waypoints, bool closePath)
    {
        if (waypoints == null)
        {
            return new List<Vector3>();
        }

        List<Vector3> path = waypoints
            .Where(waypoint => waypoint != null)
            .Select(waypoint => WorldToPathLocal(waypoint.position))
            .ToList();

        return ApplyClosePathIfNeeded(path, closePath);
    }

    private List<Vector3> BuildPathFromPositions(List<Vector3> pathPositions, bool closePath)
    {
        if (pathPositions == null || pathPositions.Count == 0)
        {
            return new List<Vector3>();
        }

        var path = new List<Vector3>(pathPositions.Count);
        for (int i = 0; i < pathPositions.Count; i++)
        {
            path.Add(WorldToPathLocal(pathPositions[i]));
        }

        return ApplyClosePathIfNeeded(path, closePath);
    }

    private static List<Vector3> ApplyClosePathIfNeeded(List<Vector3> path, bool closePath)
    {
        if (closePath && path.Count > 0)
        {
            path.Add(path[0]);
        }

        return path;
    }

    private Material GetOrCreateRuntimeMaterial(LineInstance instance)
    {
        if (instance.RuntimeMaterial != null)
        {
            return instance.RuntimeMaterial;
        }

        if (_lineMaterial != null)
        {
            instance.RuntimeMaterial = Instantiate(_lineMaterial);
        }
        else if (instance.LineRenderer != null)
        {
            instance.RuntimeMaterial = instance.LineRenderer.material;
        }
        else
        {
            return null;
        }

        // 清除材质资产上残留的 Tiling/Offset，保证 Stretch 下整线一张贴图且边缘在起点。
        Vector2 scale = instance.RuntimeMaterial.mainTextureScale;
        instance.RuntimeMaterial.mainTextureScale = new Vector2(1f, scale.y);
        Vector2 offset = instance.RuntimeMaterial.mainTextureOffset;
        instance.RuntimeMaterial.mainTextureOffset = new Vector2(0f, offset.y);

        if (instance.LineRenderer != null)
        {
            instance.LineRenderer.material = instance.RuntimeMaterial;
            instance.LineRenderer.textureMode = LineTextureMode.Stretch;
        }

        return instance.RuntimeMaterial;
    }

    private static float CalculatePathWorldLength(IReadOnlyList<Vector3> localPath, Transform localSpace)
    {
        if (localPath == null || localPath.Count < 2 || localSpace == null)
        {
            return 0f;
        }

        float length = 0f;
        Vector3 previousWorld = localSpace.TransformPoint(localPath[0]);
        for (int i = 1; i < localPath.Count; i++)
        {
            Vector3 world = localSpace.TransformPoint(localPath[i]);
            length += Vector3.Distance(previousWorld, world);
            previousWorld = world;
        }

        return length;
    }

    private static void SetLineVisible(LineInstance instance, bool visible)
    {
        if (instance?.LineTransform != null)
        {
            instance.LineTransform.gameObject.SetActive(visible);
        }
    }
}
