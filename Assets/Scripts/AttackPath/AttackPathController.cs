using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 攻击路径播放：沿路点移动 <see cref="TrailRenderer"/> 线条，到达终点后驱动材质贴图 Offset X 动画。
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
    [Tooltip("沿路径移动速度（世界单位/秒）")]
    [SerializeField] private float _speed = 8f;
    [Tooltip("勾选后路径首尾相连（闭合路径）")]
    [SerializeField] private bool _closePath;
    [Tooltip("勾选后 DOPath 动画循环播放")]
    [SerializeField] private bool _loopAnimation;

    [Header("到达终点贴图动画")]
    [Tooltip("留空则从 TrailRenderer 材质获取并实例化")]
    [SerializeField] private Material _lineMaterial;
    [Tooltip("到达终点后贴图 MainTex Offset X 的增量")]
    [SerializeField] private float _endTextureOffsetX = 100f;
    [Tooltip("Offset X 增速 = 移动速度 × 该系数")]
    [SerializeField] private float _endOffsetSpeedCoefficient = 1f;

    private readonly List<LineInstance> _addedLines = new List<LineInstance>();
    private LineInstance _legacyLine;

    /// <summary>单条攻击路径实例（模板或克隆体）。</summary>
    private sealed class LineInstance
    {
        public Transform LineTransform;
        public TrailRenderer TrailRenderer;
        public Material RuntimeMaterial;
        public Tween PathTween;
        public Tween OffsetTween;
        public float CurrentPlaySpeed;
        public bool IsTemplate;
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

    /// <summary>组件禁用时停止路径与贴图动画并隐藏线条。</summary>
    private void OnDisable()
    {
        if (IsClonedLineObject())
        {
            return;
        }

        StopPath();
    }

    /// <summary>停止路径与贴图动画并隐藏所有线条（供外部过渡控制器调用）。</summary>
    public void StopPath()
    {
        KillLineInstance(_legacyLine);
        ClearLines();
    }

    /// <summary>
    /// 按零件名添加一条起点→终点攻击路径；可同时存在多条。
    /// 零件名与 <see cref="GridLine"/> 中 start3DObjectName 一致。
    /// </summary>
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

    /// <summary>
    /// 使用指定路点 Transform 列表播放攻击路径（兼容旧逻辑，会清空 Add 创建的连线）。
    /// </summary>
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

    /// <summary>使用世界坐标路点列表播放攻击路径（内部会转换到路径本地空间）。</summary>
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

    /// <summary>合并本次播放参数：可空入参优先，否则回退到 Inspector 字段。</summary>
    private PlayPathSettings ResolvePlayPathSettings(float? speed, bool? closePath, bool? loopAnimation)
    {
        return new PlayPathSettings
        {
            Speed = speed ?? _speed,
            ClosePath = closePath ?? _closePath,
            LoopAnimation = loopAnimation ?? _loopAnimation
        };
    }

    /// <summary>单次播放运行时参数（不修改 Inspector 序列化字段）。</summary>
    private struct PlayPathSettings
    {
        public float Speed;
        public bool ClosePath;
        public bool LoopAnimation;
    }

    /// <summary>
    /// 核心播放逻辑：隐藏复位 → 瞬移起点 → DOPath 移动 → 非循环时在终点触发贴图 Offset 动画。
    /// </summary>
    private void PlayPathOnInstance(LineInstance instance, List<Vector3> path, PlayPathSettings settings)
    {
        KillLineInstance(instance, hideLine: false);
        instance.CurrentPlaySpeed = settings.Speed;

        Transform pathLocalSpace = PathLocalSpace;
        if (instance.LineTransform == null
            || pathLocalSpace == null
            || path == null
            || path.Count < 2
            || settings.Speed <= 0f)
        {
            SetLineVisible(instance, false);
            return;
        }

        SetLineVisible(instance, false);
        instance.LineTransform.localPosition = path[0];
        ClearTrailRenderer(instance);
        ResetTextureOffset(instance);
        SetLineVisible(instance, true);

        float pathLength = CalculatePathWorldLength(path, pathLocalSpace);
        if (pathLength <= 0f)
        {
            SetLineVisible(instance, false);
            return;
        }

        float duration = pathLength / settings.Speed;
        Tweener pathTween = instance.LineTransform
            .DOLocalPath(path.ToArray(), duration, PathType.Linear)
            .SetEase(Ease.Linear);

        if (settings.LoopAnimation)
        {
            pathTween.SetLoops(-1, LoopType.Restart);
        }
        else
        {
            pathTween.OnComplete(() => PlayEndTextureOffsetAnimation(instance));
        }

        instance.PathTween = pathTween;
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

    private static LineInstance CreateLineInstance(GameObject lineObject, bool isTemplate)
    {
        Transform lineTransform = lineObject.transform;
        TrailRenderer trailRenderer = lineObject.GetComponentInChildren<TrailRenderer>(true);
        return new LineInstance
        {
            LineTransform = lineTransform,
            TrailRenderer = trailRenderer,
            IsTemplate = isTemplate
        };
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

        if (instance.PathTween != null && instance.PathTween.IsActive())
        {
            instance.PathTween.Kill();
        }

        if (instance.OffsetTween != null && instance.OffsetTween.IsActive())
        {
            instance.OffsetTween.Kill();
        }

        instance.PathTween = null;
        instance.OffsetTween = null;
        ClearTrailRenderer(instance);

        if (hideLine)
        {
            SetLineVisible(instance, false);
        }
    }

    /// <summary>路径本地坐标参照：优先 <see cref="_pathLocalRoot"/>，否则为 <see cref="_lineTransform"/> 的父节点。</summary>
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

    /// <summary>将世界坐标路点转换到路径本地空间。</summary>
    private Vector3 WorldToPathLocal(Vector3 worldPosition)
    {
        Transform pathLocalSpace = PathLocalSpace;
        return pathLocalSpace != null ? pathLocalSpace.InverseTransformPoint(worldPosition) : worldPosition;
    }

    /// <summary>从 Transform 路点构建本地坐标路径，并按需闭合首尾。</summary>
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

    /// <summary>将世界坐标路点转换到本地空间后构建路径。</summary>
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

    /// <summary>若 <paramref name="closePath"/> 为 true，在路径末尾追加起点坐标形成闭合。</summary>
    private static List<Vector3> ApplyClosePathIfNeeded(List<Vector3> path, bool closePath)
    {
        if (closePath && path.Count > 0)
        {
            path.Add(path[0]);
        }

        return path;
    }

    /// <summary>到达终点后：MainTex Offset X 按「速度 × 系数」逐渐增加。</summary>
    private void PlayEndTextureOffsetAnimation(LineInstance instance)
    {
        if (_endTextureOffsetX <= 0f || _endOffsetSpeedCoefficient <= 0f || instance.CurrentPlaySpeed <= 0f)
        {
            return;
        }

        Material material = GetOrCreateRuntimeMaterial(instance);
        if (material == null)
        {
            return;
        }

        float offsetSpeed = instance.CurrentPlaySpeed * _endOffsetSpeedCoefficient;
        float duration = _endTextureOffsetX / offsetSpeed;
        Vector2 startOffset = material.mainTextureOffset;
        Vector2 targetOffset = new Vector2(startOffset.x + _endTextureOffsetX, startOffset.y);

        instance.OffsetTween?.Kill();
        instance.OffsetTween = material
            .DOOffset(targetOffset, duration)
            .SetEase(Ease.Linear);
    }

    /// <summary>将运行时材质 MainTex Offset X 重置为 0（保留 Y）。</summary>
    private void ResetTextureOffset(LineInstance instance)
    {
        Material material = GetOrCreateRuntimeMaterial(instance);
        if (material == null)
        {
            return;
        }

        Vector2 offset = material.mainTextureOffset;
        material.mainTextureOffset = new Vector2(0f, offset.y);
    }

    /// <summary>获取或创建运行时材质实例，并绑定到 <see cref="TrailRenderer"/>。</summary>
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
        else if (instance.TrailRenderer != null)
        {
            instance.RuntimeMaterial = instance.TrailRenderer.material;
        }
        else
        {
            return null;
        }

        if (instance.TrailRenderer != null)
        {
            instance.TrailRenderer.material = instance.RuntimeMaterial;
        }

        return instance.RuntimeMaterial;
    }

    /// <summary>按世界空间折线长度计算时长（速度仍为世界单位/秒）。</summary>
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

    /// <summary>清空 Trail 历史顶点。</summary>
    private static void ClearTrailRenderer(LineInstance instance)
    {
        if (instance?.TrailRenderer != null)
        {
            instance.TrailRenderer.Clear();
        }
    }

    /// <summary>显示或隐藏线条根物体。</summary>
    private static void SetLineVisible(LineInstance instance, bool visible)
    {
        if (instance?.LineTransform != null)
        {
            instance.LineTransform.gameObject.SetActive(visible);
        }
    }
}
