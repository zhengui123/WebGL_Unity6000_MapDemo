using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridLine : MonoBehaviour
{
    [Serializable]
    public class GridLineBindingData
    {
        [Tooltip("三维物体名称，作为字典 Key；留空则使用 start3D 的 GameObject 名称")]
        public string start3DObjectName;
        public Transform start3D;
        public Transform endUI;
    }

    private class GridLineBinding
    {
        public Transform Start3D;
        public Transform EndUI;
        public Vector3 EndUINormalScale;
        public float DrawProgress;
    }

    public static bool isShowGridLine = true;
    public Material lineMaterial;

    [Header("连线配置（三维物体 → UI）")]
    [SerializeField] private List<GridLineBindingData> _lineBindings = new List<GridLineBindingData>();

    [Header("兼容旧配置（仅当列表为空时生效）")]
    [SerializeField] private Transform start3D;
    [SerializeField] private Transform m_EndUI;

    [Header("绘制动画（左→右出现，消失反向收起）")]
    [SerializeField] private float _drawDuration = 0.6f;
    [SerializeField] private AnimationCurve _drawEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("endUI 缩放显隐")]
    [SerializeField] private float _endUIScaleDuration = 0.35f;
    [SerializeField] private AnimationCurve _endUIScaleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly Dictionary<string, GridLineBinding> _bindingCache = new Dictionary<string, GridLineBinding>();
    private Coroutine _animationCoroutine;
    private string _activeStart3DName;
    private Vector2 _centerPos;

    public string ActiveStart3DName => _activeStart3DName;
    public bool IsAnimating => _animationCoroutine != null;

    private void Awake()
    {
        if (!lineMaterial)
        {
            //lineMaterial = new Material(Shader.Find("Particles/Alpha Blended"));
            //lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            //lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
        }

        BuildBindingCache();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    /// <summary>按三维物体名称显示连线；若已有其它连线在显示，则先收起旧线再显示新线。</summary>
    public bool PlayDrawAnimation(string start3DObjectName, Action onComplete = null)
    {
        if (!TryGetBinding(start3DObjectName, out GridLineBinding targetBinding))
        {
            Debug.LogError($"[GridLine] 未找到三维物体连线配置：{start3DObjectName}");
            onComplete?.Invoke();
            return false;
        }

        if (IsSameActiveAndFullyShown(start3DObjectName, targetBinding))
        {
            onComplete?.Invoke();
            return true;
        }

        StopAnimation();

        if (ShouldSwitchFromAnotherLine(start3DObjectName))
        {
            _animationCoroutine = StartCoroutine(SwitchLineSequence(_activeStart3DName, start3DObjectName, onComplete));
            return true;
        }

        _activeStart3DName = start3DObjectName;
        PrepareBindingForDraw(targetBinding);
        _animationCoroutine = StartCoroutine(PlayDrawSequence(targetBinding, onComplete));
        return true;
    }

    /// <summary>按三维物体名称收起当前连线。</summary>
    public bool PlayReverseAnimation(string start3DObjectName, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(start3DObjectName))
        {
            Debug.LogError("[GridLine] PlayReverseAnimation 需要传入三维物体名称。");
            onComplete?.Invoke();
            return false;
        }

        if (!TryGetBinding(start3DObjectName, out GridLineBinding binding))
        {
            Debug.LogError($"[GridLine] 未找到三维物体连线配置：{start3DObjectName}");
            onComplete?.Invoke();
            return false;
        }

        if (_activeStart3DName != start3DObjectName || !IsBindingVisible(binding))
        {
            onComplete?.Invoke();
            return false;
        }

        StopAnimation();
        binding.EndUI.gameObject.SetActive(true);
        _animationCoroutine = StartCoroutine(PlayReverseSequence(binding, () =>
        {
            HideBinding(binding);
            _activeStart3DName = null;
            onComplete?.Invoke();
        }));
        return true;
    }

    public bool IsLineVisible(string start3DObjectName)
    {
        if (!TryGetBinding(start3DObjectName, out GridLineBinding binding))
        {
            return false;
        }

        return _activeStart3DName == start3DObjectName && IsBindingVisible(binding);
    }

    private void BuildBindingCache()
    {
        _bindingCache.Clear();

        if (_lineBindings.Count == 0 && start3D != null && m_EndUI != null)
        {
            _lineBindings.Add(new GridLineBindingData
            {
                start3DObjectName = start3D.name,
                start3D = start3D,
                endUI = m_EndUI
            });
        }

        for (int i = 0; i < _lineBindings.Count; i++)
        {
            GridLineBindingData data = _lineBindings[i];
            if (data == null || data.start3D == null || data.endUI == null)
            {
                continue;
            }

            string key = ResolveBindingKey(data);
            if (_bindingCache.ContainsKey(key))
            {
                Debug.LogWarning($"[GridLine] 重复的三维物体名称：{key}，后者将覆盖前者。");
            }

            if(string.IsNullOrEmpty(data.start3DObjectName))
            {
                data.start3DObjectName = data.start3D.name;
            }
            GridLineBinding binding = new GridLineBinding
            {
                Start3D = data.start3D,
                EndUI = data.endUI,
                DrawProgress = 0f
            };
            CacheEndUINormalScale(binding);
            HideBinding(binding);
            _bindingCache[key] = binding;
        }
    }

    private static string ResolveBindingKey(GridLineBindingData data)
    {
        if (!string.IsNullOrEmpty(data.start3DObjectName))
        {
            return data.start3DObjectName;
        }

        return data.start3D.name;
    }

    private bool TryGetBinding(string start3DObjectName, out GridLineBinding binding)
    {
        binding = null;
        if (string.IsNullOrEmpty(start3DObjectName))
        {
            return false;
        }

        return _bindingCache.TryGetValue(start3DObjectName, out binding);
    }

    private bool IsSameActiveAndFullyShown(string start3DObjectName, GridLineBinding binding)
    {
        return _activeStart3DName == start3DObjectName
            && binding.DrawProgress >= 1f
            && binding.EndUI != null
            && binding.EndUI.gameObject.activeSelf
            && _animationCoroutine == null;
    }

    private bool ShouldSwitchFromAnotherLine(string newStart3DObjectName)
    {
        if (string.IsNullOrEmpty(_activeStart3DName) || _activeStart3DName == newStart3DObjectName)
        {
            return false;
        }

        if (!TryGetBinding(_activeStart3DName, out GridLineBinding activeBinding))
        {
            return false;
        }

        return IsBindingVisible(activeBinding) || _animationCoroutine != null;
    }

    private static bool IsBindingVisible(GridLineBinding binding)
    {
        return binding != null
            && binding.EndUI != null
            && binding.EndUI.gameObject.activeSelf
            && binding.DrawProgress > 0f;
    }

    private void StopAnimation()
    {
        if (_animationCoroutine == null)
        {
            return;
        }

        StopCoroutine(_animationCoroutine);
        _animationCoroutine = null;
    }

    private IEnumerator SwitchLineSequence(string oldStart3DName, string newStart3DName, Action onComplete)
    {
        if (TryGetBinding(oldStart3DName, out GridLineBinding oldBinding))
        {
            oldBinding.EndUI.gameObject.SetActive(true);
            yield return PlayReverseSequence(oldBinding, null);
            HideBinding(oldBinding);
        }

        if (!TryGetBinding(newStart3DName, out GridLineBinding newBinding))
        {
            _activeStart3DName = null;
            _animationCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        _activeStart3DName = newStart3DName;
        PrepareBindingForDraw(newBinding);
        yield return PlayDrawSequence(newBinding, null);

        _animationCoroutine = null;
        onComplete?.Invoke();
    }

    private void PrepareBindingForDraw(GridLineBinding binding)
    {
        binding.EndUI.gameObject.SetActive(true);
        SetEndUIScaleFactor(binding, 0f);
        binding.DrawProgress = 0f;
    }

    private static void HideBinding(GridLineBinding binding)
    {
        if (binding == null || binding.EndUI == null)
        {
            return;
        }

        binding.DrawProgress = 0f;
        binding.EndUI.localScale = binding.EndUINormalScale * 0f;
        binding.EndUI.gameObject.SetActive(false);
    }

    private IEnumerator PlayDrawSequence(GridLineBinding binding, Action onComplete)
    {
        SetEndUIScaleFactor(binding, 0f);
        yield return AnimateDrawRoutine(binding, 0f, 1f);
        yield return ScaleEndUIRoutine(binding, 0f, 1f);

        _animationCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayReverseSequence(GridLineBinding binding, Action onComplete)
    {
        float endUIFrom = GetEndUIScaleFactor(binding);
        yield return ScaleEndUIRoutine(binding, endUIFrom, 0f);
        yield return AnimateDrawRoutine(binding, binding.DrawProgress, 0f);

        onComplete?.Invoke();
    }

    private IEnumerator AnimateDrawRoutine(GridLineBinding binding, float from, float to)
    {
        if (_drawDuration <= 0f)
        {
            binding.DrawProgress = to;
            yield break;
        }

        float elapsed = 0f;
        binding.DrawProgress = from;
        while (elapsed < _drawDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _drawDuration);
            binding.DrawProgress = Mathf.Lerp(from, to, _drawEase.Evaluate(t));
            yield return null;
        }

        binding.DrawProgress = to;
    }

    private IEnumerator ScaleEndUIRoutine(GridLineBinding binding, float from, float to)
    {
        if (binding.EndUI == null)
        {
            yield break;
        }

        if (_endUIScaleDuration <= 0f)
        {
            SetEndUIScaleFactor(binding, to);
            yield break;
        }

        float elapsed = 0f;
        SetEndUIScaleFactor(binding, from);
        while (elapsed < _endUIScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _endUIScaleDuration);
            SetEndUIScaleFactor(binding, Mathf.Lerp(from, to, _endUIScaleEase.Evaluate(t)));
            yield return null;
        }

        SetEndUIScaleFactor(binding, to);
    }

    private static void CacheEndUINormalScale(GridLineBinding binding)
    {
        if (binding == null || binding.EndUI == null)
        {
            return;
        }

        binding.EndUINormalScale = binding.EndUI.localScale;
        if (binding.EndUINormalScale.sqrMagnitude < 0.0001f)
        {
            binding.EndUINormalScale = Vector3.one;
        }
    }

    private static float GetEndUIScaleFactor(GridLineBinding binding)
    {
        if (binding == null || binding.EndUI == null)
        {
            return 1f;
        }

        float reference = Mathf.Max(Mathf.Abs(binding.EndUINormalScale.x), 0.0001f);
        return binding.EndUI.localScale.x / reference;
    }

    private static void SetEndUIScaleFactor(GridLineBinding binding, float factor)
    {
        if (binding == null || binding.EndUI == null)
        {
            return;
        }

        binding.EndUI.localScale = binding.EndUINormalScale * Mathf.Clamp01(factor);
    }

    private void OnPostRender()
    {
        if (!isShowGridLine || lineMaterial == null || string.IsNullOrEmpty(_activeStart3DName))
        {
            return;
        }

        if (!TryGetBinding(_activeStart3DName, out GridLineBinding binding)
            || binding.Start3D == null
            || binding.EndUI == null)
        {
            return;
        }

        if (binding.DrawProgress <= 0f)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[GridLine] Camera.main 为空。");
            return;
        }

        Vector2 start = cam.WorldToViewportPoint(binding.Start3D.position);
        Vector2 end = cam.ScreenToViewportPoint(binding.EndUI.position);
        Vector2 center = GetCenterPos(start, end);

        GL.PushMatrix();
        lineMaterial.SetPass(0);
        GL.LoadOrtho();
        GL.Begin(GL.LINES);

        if (binding.DrawProgress >= 1f)
        {
            DrawFullPolyline(start, center, end);
        }
        else
        {
            DrawPartialPolylineLeftToRight(start, center, end, binding.DrawProgress);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isShowGridLine = true;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isShowGridLine = false;
        }
    }

    private static void DrawFullPolyline(Vector2 start, Vector2 center, Vector2 end)
    {
        GL.Vertex(start);
        GL.Vertex(center);
        GL.Vertex(center);
        GL.Vertex(end);
    }

    private static void DrawPartialPolylineLeftToRight(Vector2 start, Vector2 center, Vector2 end, float progress)
    {
        float firstLength = Vector2.Distance(start, center);
        float secondLength = Vector2.Distance(center, end);
        float totalLength = firstLength + secondLength;
        if (totalLength < 0.0001f)
        {
            return;
        }

        float visibleLength = progress * totalLength;
        if (visibleLength <= firstLength)
        {
            Vector2 partialEnd = Vector2.Lerp(start, center, visibleLength / firstLength);
            GL.Vertex(start);
            GL.Vertex(partialEnd);
            return;
        }

        float onSecondSegment = visibleLength - firstLength;
        Vector2 partialEndOnSecond = Vector2.Lerp(center, end, onSecondSegment / secondLength);
        GL.Vertex(start);
        GL.Vertex(center);
        GL.Vertex(center);
        GL.Vertex(partialEndOnSecond);
    }

    private Vector2 GetCenterPos(Vector2 start, Vector2 end)
    {
        _centerPos.x = start.x + (end.x - start.x) * 0.6f;
        _centerPos.y = end.y;
        return _centerPos;
    }
}
