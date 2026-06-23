using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 攻击路径播放：沿路点移动 <see cref="TrailRenderer"/> 线条，到达终点后驱动材质贴图 Offset X 动画。
/// 路点数据由 <see cref="AttackPathDemo"/> 或外部调用方传入。
/// </summary>
public class AttackPathController : MonoBehaviour
{
    [FormerlySerializedAs("lineTransform")]
    [SerializeField] private Transform _lineTransform;
    [Tooltip("路径本地坐标参照父节点；留空则使用 _lineTransform 的 parent")]
    [SerializeField] private Transform _pathLocalRoot;
    [FormerlySerializedAs("speed")]
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

    private Tween _pathTween;
    private Tween _offsetTween;
    private TrailRenderer _trailRenderer;
    private Material _runtimeMaterial;
    private float _currentPlaySpeed;

    /// <summary>组件禁用时停止路径与贴图动画并隐藏线条。</summary>
    private void OnDisable()
    {
        KillPathTween();
    }

    /// <summary>停止路径与贴图动画并隐藏线条（供外部过渡控制器调用）。</summary>
    public void StopPath()
    {
        KillPathTween();
    }

    /// <summary>
    /// 使用指定路点 Transform 列表播放攻击路径。
    /// </summary>
    /// <param name="waypoints">路点 Transform 列表（至少 2 个非空项）。</param>
    /// <param name="speed">移动速度（世界单位/秒）；为 null 时使用 Inspector 配置。</param>
    /// <param name="closePath">是否闭合首尾；为 null 时使用 Inspector 配置。</param>
    /// <param name="loopAnimation">是否循环播放；为 null 时使用 Inspector 配置。</param>
    public void PlayPath(
        List<Transform> waypoints,
        float? speed = null,
        bool? closePath = null,
        bool? loopAnimation = null)
    {
        PlayPathSettings settings = ResolvePlayPathSettings(speed, closePath, loopAnimation);
        PlayPathInternal(BuildPathFromTransforms(waypoints, settings.ClosePath), settings);
    }

    /// <summary>使用世界坐标路点列表播放攻击路径（内部会转换到路径本地空间）。</summary>
    /// <param name="pathPositions">路点世界坐标列表（至少 2 个点）。</param>
    /// <param name="speed">移动速度（世界单位/秒）；为 null 时使用 Inspector 配置。</param>
    /// <param name="closePath">是否闭合首尾；为 null 时使用 Inspector 配置。</param>
    /// <param name="loopAnimation">是否循环播放；为 null 时使用 Inspector 配置。</param>
    public void PlayPath(
        List<Vector3> pathPositions,
        float? speed = null,
        bool? closePath = null,
        bool? loopAnimation = null)
    {
        PlayPathSettings settings = ResolvePlayPathSettings(speed, closePath, loopAnimation);
        PlayPathInternal(BuildPathFromPositions(pathPositions, settings.ClosePath), settings);
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
    /// <param name="path">路径本地坐标折线（相对 <see cref="PathLocalSpace"/>）。</param>
    /// <param name="settings">本次播放的速度与路径选项。</param>
    private void PlayPathInternal(List<Vector3> path, PlayPathSettings settings)
    {
        KillPathTween();
        _currentPlaySpeed = settings.Speed;

        Transform pathLocalSpace = PathLocalSpace;
        if (_lineTransform == null || pathLocalSpace == null || path == null || path.Count < 2 || settings.Speed <= 0f)
        {
            SetLineVisible(false);
            return;
        }

        SetLineVisible(false);
        _lineTransform.localPosition = path[0];
        ClearTrailRenderer();
        ResetTextureOffset();
        SetLineVisible(true);

        float pathLength = CalculatePathWorldLength(path, pathLocalSpace);
        if (pathLength <= 0f)
        {
            SetLineVisible(false);
            return;
        }

        float duration = pathLength / settings.Speed;
        Tweener pathTween = _lineTransform
            .DOLocalPath(path.ToArray(), duration, PathType.Linear)
            .SetEase(Ease.Linear);

        if (settings.LoopAnimation)
        {
            pathTween.SetLoops(-1, LoopType.Restart);
        }
        else
        {
            pathTween.OnComplete(PlayEndTextureOffsetAnimation);
        }

        _pathTween = pathTween;
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
    private void PlayEndTextureOffsetAnimation()
    {
        if (_endTextureOffsetX <= 0f || _endOffsetSpeedCoefficient <= 0f || _currentPlaySpeed <= 0f)
        {
            return;
        }

        Material material = GetOrCreateRuntimeMaterial();
        if (material == null)
        {
            return;
        }

        float offsetSpeed = _currentPlaySpeed * _endOffsetSpeedCoefficient;
        float duration = _endTextureOffsetX / offsetSpeed;
        Vector2 startOffset = material.mainTextureOffset;
        Vector2 targetOffset = new Vector2(startOffset.x + _endTextureOffsetX, startOffset.y);

        _offsetTween?.Kill();
        _offsetTween = material
            .DOOffset(targetOffset, duration)
            .SetEase(Ease.Linear);
    }

    /// <summary>将运行时材质 MainTex Offset X 重置为 0（保留 Y）。</summary>
    private void ResetTextureOffset()
    {
        Material material = GetOrCreateRuntimeMaterial();
        if (material == null)
        {
            return;
        }

        Vector2 offset = material.mainTextureOffset;
        material.mainTextureOffset = new Vector2(0f, offset.y);
    }

    /// <summary>获取或创建运行时材质实例，并绑定到 <see cref="TrailRenderer"/>。</summary>
    private Material GetOrCreateRuntimeMaterial()
    {
        if (_runtimeMaterial != null)
        {
            return _runtimeMaterial;
        }

        if (_lineMaterial != null)
        {
            _runtimeMaterial = Instantiate(_lineMaterial);
        }
        else
        {
            CacheTrailRenderer();
            if (_trailRenderer == null)
            {
                return null;
            }

            _runtimeMaterial = _trailRenderer.material;
        }

        CacheTrailRenderer();
        if (_trailRenderer != null)
        {
            _trailRenderer.material = _runtimeMaterial;
        }

        return _runtimeMaterial;
    }

    /// <summary>按世界空间折线长度计算时长（<see cref="_speed"/> 仍为世界单位/秒）。</summary>
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

    /// <summary>停止路径/贴图 Tween，清理 Trail 并隐藏线条物体。</summary>
    private void KillPathTween()
    {
        if (_pathTween != null && _pathTween.IsActive())
        {
            _pathTween.Kill();
        }

        if (_offsetTween != null && _offsetTween.IsActive())
        {
            _offsetTween.Kill();
        }

        _pathTween = null;
        _offsetTween = null;
        ClearTrailRenderer();
        SetLineVisible(false);
    }

    /// <summary>懒加载 <see cref="TrailRenderer"/> 引用。</summary>
    private void CacheTrailRenderer()
    {
        if (_trailRenderer == null && _lineTransform != null)
        {
            _trailRenderer = _lineTransform.GetComponentInChildren<TrailRenderer>(true);
        }
    }

    /// <summary>清空 Trail 历史顶点。</summary>
    private void ClearTrailRenderer()
    {
        CacheTrailRenderer();
        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
        }
    }

    /// <summary>显示或隐藏线条根物体。</summary>
    private void SetLineVisible(bool visible)
    {
        if (_lineTransform != null)
        {
            _lineTransform.gameObject.SetActive(visible);
        }
    }
}
