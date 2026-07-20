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
        [Tooltip("连线起点 UI 图（RectTransform），留空则不显示；运行时跟随 start3D 屏幕坐标")]
        public Transform startUI;
        [Tooltip("连线终点消息面板（MessageListPanel）")]
        public MessageListPanel endUI;
    }

    private class GridLineBinding
    {
        public Transform Start3D;
        public Transform StartUI;
        public Transform EndUI;
        public MessageListPanel EndMessageListPanel;
        public Vector3 EndUINormalScale;
        public float DrawProgress;
    }

    public static bool isShowGridLine = true;
    public Material lineMaterial;

    [Header("连线配置（三维物体 → UI）")]
    [SerializeField] private List<GridLineBindingData> _lineBindings = new List<GridLineBindingData>();

    [Header("兼容旧配置（仅当列表为空时生效）")]
    [SerializeField] private Transform start3D;
    [SerializeField] private MessageListPanel m_EndUI;

    [Header("绘制动画（左→右出现，消失反向收起）")]
    [SerializeField] private float _drawDuration = 0.6f;
    [SerializeField] private AnimationCurve _drawEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("线条样式")]
    [SerializeField] private float _lineWidthPixels = 3f;
    [Tooltip("留空则使用 Camera.main；OnPostRender 仅在该相机上绘制")]
    [SerializeField] private Camera _drawCamera;

    [Header("虚线样式（视口空间单位）")]
    [SerializeField] private bool _useDashedLine = true;
    [SerializeField] private float _dashLength = 0.005f;
    [SerializeField] private float _gapLength = 0.005f;

    [Header("endUI 缩放显隐")]
    [SerializeField] private float _endUIScaleDuration = 0.35f;
    [SerializeField] private AnimationCurve _endUIScaleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly Dictionary<string, GridLineBinding> _bindingCache = new Dictionary<string, GridLineBinding>();
    private Coroutine _animationCoroutine;
    private string _activeStart3DName;
    private Vector2 _centerPos;

    public string ActiveStart3DName => _activeStart3DName;
    public bool IsAnimating => _animationCoroutine != null;

    /// <summary>按三维物体名称取绑定的消息面板；未找到返回 null。</summary>
    public MessageListPanel GetEndMessageListPanel(string start3DObjectName)
    {
        if (!TryGetBinding(start3DObjectName, out GridLineBinding binding))
        {
            return null;
        }

        return binding.EndMessageListPanel;
    }

    /// <summary>按零件名取对应三维 Transform；未找到返回 false。</summary>
    public bool TryGetPartTransform(string partName, out Transform partTransform)
    {
        partTransform = null;
        if (!TryGetBinding(partName, out GridLineBinding binding))
        {
            return false;
        }

        partTransform = binding.Start3D;
        return partTransform != null;
    }

    /// <summary>当前激活连线绑定的消息面板。</summary>
    public MessageListPanel ActiveEndMessageListPanel
    {
        get
        {
            if (string.IsNullOrEmpty(_activeStart3DName))
            {
                return m_EndUI;
            }

            return GetEndMessageListPanel(_activeStart3DName) ?? m_EndUI;
        }
    }

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
                StartUI = data.startUI,
                EndUI = data.endUI != null ? data.endUI.transform : null,
                EndMessageListPanel = data.endUI,
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
        SetStartUIVisible(binding, true);
        UpdateStartUIPosition(binding);
    }

    private static void HideBinding(GridLineBinding binding)
    {
        if (binding == null)
        {
            return;
        }

        binding.DrawProgress = 0f;

        if (binding.EndUI != null)
        {
            binding.EndUI.localScale = binding.EndUINormalScale * 0f;
            binding.EndUI.gameObject.SetActive(false);
        }

        SetStartUIVisible(binding, false);
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

        Camera cam = Camera.current;
        Camera drawCam = ResolveDrawCamera();
        if (cam == null || drawCam == null || cam != drawCam)
        {
            return;
        }

        if (!TryGetViewportPoint(drawCam, binding.Start3D, out Vector2 start)
            || !TryGetViewportPoint(drawCam, binding.EndUI, out Vector2 end))
        {
            return;
        }

        float halfWidth = GetLineHalfWidthViewport(drawCam);
        start = ClampViewportPoint(start, halfWidth);
        end = ClampViewportPoint(end, halfWidth);

        Vector2 center = GetCenterPos(start, end);

        GL.PushMatrix();
        lineMaterial.SetPass(0);
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);
        DrawPolyline(start, center, end, binding.DrawProgress, drawCam);
        GL.End();
        GL.PopMatrix();
    }

    private Camera ResolveDrawCamera()
    {
        if (_drawCamera != null)
        {
            return _drawCamera;
        }

        return Camera.main;
    }

    private void LateUpdate()
    {
        if (string.IsNullOrEmpty(_activeStart3DName))
        {
            return;
        }

        if (!TryGetBinding(_activeStart3DName, out GridLineBinding binding) || !IsBindingVisible(binding))
        {
            return;
        }

        UpdateStartUIPosition(binding);
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

    private void UpdateStartUIPosition(GridLineBinding binding)
    {
        if (binding == null || binding.StartUI == null || binding.Start3D == null)
        {
            return;
        }

        Camera cam = ResolveDrawCamera();
        if (cam == null)
        {
            return;
        }

        TrySetUIPositionFromWorld(binding.StartUI, binding.Start3D.position, cam);
    }

    private static void SetStartUIVisible(GridLineBinding binding, bool visible)
    {
        if (binding?.StartUI == null)
        {
            return;
        }

        binding.StartUI.gameObject.SetActive(visible);
    }

    /// <summary>将世界坐标换算为屏幕坐标，并写入 UI RectTransform 的 anchoredPosition。</summary>
    private static bool TrySetUIPositionFromWorld(Transform uiTransform, Vector3 worldPosition, Camera worldCamera)
    {
        if (uiTransform == null || worldCamera == null)
        {
            return false;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
        {
            uiTransform.gameObject.SetActive(false);
            return false;
        }

        RectTransform rect = uiTransform as RectTransform;
        if (rect == null)
        {
            uiTransform.position = screenPoint;
            return true;
        }

        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect == null)
        {
            rect.position = screenPoint;
            return true;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        rect.anchoredPosition = localPoint;
        if (!rect.gameObject.activeSelf)
        {
            rect.gameObject.SetActive(true);
        }

        return true;
    }

    /// <summary>沿折线路径绘制；progress 为 0~1，按路径总长度从左到右裁剪。</summary>
    private void DrawPolyline(Vector2 start, Vector2 center, Vector2 end, float progress, Camera cam)
    {
        float firstLength = Vector2.Distance(start, center);
        float secondLength = Vector2.Distance(center, end);
        float totalLength = firstLength + secondLength;
        if (totalLength < 0.0001f)
        {
            return;
        }

        float visibleLength = Mathf.Clamp01(progress) * totalLength;
        if (visibleLength <= 0f)
        {
            return;
        }

        if (visibleLength <= firstLength)
        {
            Vector2 partialEnd = Vector2.Lerp(start, center, visibleLength / firstLength);
            DrawPathSegment(start, partialEnd, cam);
            return;
        }

        DrawPathSegment(start, center, cam);
        float onSecondSegment = visibleLength - firstLength;
        Vector2 partialEndOnSecond = Vector2.Lerp(center, end, onSecondSegment / secondLength);
        DrawPathSegment(center, partialEndOnSecond, cam);
    }

    private float GetLineHalfWidthViewport(Camera cam)
    {
        float pixelHeight = cam != null && cam.pixelHeight > 0 ? cam.pixelHeight : Screen.height;
        float widthPixels = Mathf.Max(_lineWidthPixels, 1f);
        return (widthPixels / pixelHeight) * 0.5f;
    }

    private void DrawSolidSegment(Vector2 from, Vector2 to, Camera cam)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.00001f)
        {
            return;
        }

        Vector2 direction = delta / length;
        Vector2 normal = new Vector2(-direction.y, direction.x) * GetLineHalfWidthViewport(cam);
        GL.Vertex(from - normal);
        GL.Vertex(from + normal);
        GL.Vertex(to + normal);
        GL.Vertex(to - normal);
    }

    private void DrawPathSegment(Vector2 from, Vector2 to, Camera cam)
    {
        if (_useDashedLine)
        {
            DrawDashedSegment(from, to, _dashLength, _gapLength, cam);
            return;
        }

        DrawSolidSegment(from, to, cam);
    }

    /// <summary>在视口空间将线段切分为多段短实线，间隔处不绘制。</summary>
    private void DrawDashedSegment(Vector2 from, Vector2 to, float dashLength, float gapLength, Camera cam)
    {
        dashLength = Mathf.Max(dashLength, 0.0001f);
        gapLength = Mathf.Max(gapLength, 0f);
        float patternLength = dashLength + gapLength;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.0001f)
        {
            return;
        }

        Vector2 direction = delta / length;
        float traveled = 0f;
        while (traveled < length)
        {
            float dashEnd = Mathf.Min(traveled + dashLength, length);
            Vector2 dashFrom = from + direction * traveled;
            Vector2 dashTo = from + direction * dashEnd;
            DrawSolidSegment(dashFrom, dashTo, cam);
            traveled += patternLength;
        }
    }

    private static Vector2 ScreenPointToViewport(Camera cam, Vector3 screenPoint)
    {
        Rect pixelRect = cam.pixelRect;
        float width = Mathf.Max(pixelRect.width, 1f);
        float height = Mathf.Max(pixelRect.height, 1f);

        // 高分辨率 / DPI 缩放时，UI 屏幕坐标与相机像素尺寸可能不一致
        if (Screen.width > 0 && Screen.height > 0)
        {
            screenPoint.x *= width / Screen.width;
            screenPoint.y *= height / Screen.height;
        }

        return new Vector2(
            (screenPoint.x - pixelRect.x) / width,
            (screenPoint.y - pixelRect.y) / height);
    }

    /// <summary>将 Transform 转为 GL.LoadOrtho 使用的视口坐标（0~1）。</summary>
    private static bool TryGetViewportPoint(Camera cam, Transform target, out Vector2 viewport)
    {
        viewport = default;
        if (cam == null || target == null)
        {
            return false;
        }

        Vector3 screenPoint;
        RectTransform rect = target as RectTransform;
        if (rect != null)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            }
            else
            {
                Camera uiCam = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : cam;
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, rect.position);
            }
        }
        else
        {
            screenPoint = cam.WorldToScreenPoint(target.position);
            if (screenPoint.z < 0f)
            {
                return false;
            }
        }

        viewport = ScreenPointToViewport(cam, screenPoint);
        return true;
    }

    private static Vector2 ClampViewportPoint(Vector2 viewport, float margin)
    {
        margin = Mathf.Max(margin, 0.0001f);
        return new Vector2(
            Mathf.Clamp(viewport.x, margin, 1f - margin),
            Mathf.Clamp(viewport.y, margin, 1f - margin));
    }

    private Vector2 GetCenterPos(Vector2 start, Vector2 end)
    {
        _centerPos.x = start.x + (end.x - start.x) * 0.6f;
        _centerPos.y = end.y;
        return _centerPos;
    }
}
