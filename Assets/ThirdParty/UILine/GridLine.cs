using System;
using System.Collections;
using UnityEngine;

public class GridLine : MonoBehaviour
{
    public static bool isShowGridLine = true;
    public Material lineMaterial;
    public Transform start3D;
    public Transform m_EndUI;

    [Header("绘制动画（左→右出现，消失反向收起）")]
    [SerializeField] private float _drawDuration = 0.6f;
    [SerializeField] private AnimationCurve _drawEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("m_EndUI 缩放显隐")]
    [SerializeField] private float _endUIScaleDuration = 0.35f;
    [SerializeField] private AnimationCurve _endUIScaleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float _drawProgress = 1f;
    private Coroutine _animationCoroutine;
    private Vector3 _endUINormalScale = Vector3.one;
    private Vector2 _centerPos;

    public float DrawProgress => _drawProgress;
    public bool IsAnimating => _animationCoroutine != null;

    private void Awake()
    {
        if (!lineMaterial)
        {
            //lineMaterial = new Material(Shader.Find("Particles/Alpha Blended"));
            //lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            //lineMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
        }

        CacheEndUINormalScale(m_EndUI);
    }


    private void OnEnable()
    {
        PlayDrawAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    /// <summary>从左向右绘制线条，完成后缩放显示 endUI。</summary>
    public void PlayDrawAnimation(Transform _endUI = null, Action onComplete = null)
    {
        
        StopAnimation();
        m_EndUI.gameObject.SetActive(true);

        _animationCoroutine = StartCoroutine(PlayDrawSequence(onComplete));
    }

    /// <summary>先缩放隐藏 endUI，再从右向左收起线条。</summary>
    public void PlayReverseAnimation(Transform _endUI = null, Action onComplete = null)
    {
        StopAnimation();
        m_EndUI.gameObject.SetActive(true);

        _animationCoroutine = StartCoroutine(PlayReverseSequence(onComplete));
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

    private IEnumerator PlayDrawSequence(Action onComplete)
    {
        SetEndUIScaleFactor(0f);
        yield return AnimateDrawRoutine(0f, 1f);
        yield return ScaleEndUIRoutine(0f, 1f);

        _animationCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayReverseSequence(Action onComplete)
    {
        float endUIFrom = GetEndUIScaleFactor();
        yield return ScaleEndUIRoutine(endUIFrom, 0f);
        yield return AnimateDrawRoutine(_drawProgress, 0f);

        _animationCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateDrawRoutine(float from, float to)
    {
        if (_drawDuration <= 0f)
        {
            _drawProgress = to;
            yield break;
        }

        float elapsed = 0f;
        _drawProgress = from;
        while (elapsed < _drawDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _drawDuration);
            _drawProgress = Mathf.Lerp(from, to, _drawEase.Evaluate(t));
            yield return null;
        }

        _drawProgress = to;
    }

    private IEnumerator ScaleEndUIRoutine(float from, float to)
    {
        if (m_EndUI == null)
        {
            yield break;
        }

        if (_endUIScaleDuration <= 0f)
        {
            SetEndUIScaleFactor(to);
            yield break;
        }

        float elapsed = 0f;
        SetEndUIScaleFactor(from);
        while (elapsed < _endUIScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _endUIScaleDuration);
            SetEndUIScaleFactor(Mathf.Lerp(from, to, _endUIScaleEase.Evaluate(t)));
            yield return null;
        }

        SetEndUIScaleFactor(to);
    }

    private void CacheEndUINormalScale(Transform _endUI)
    {
        if (_endUI == null)
        {
            return;
        }
        m_EndUI = _endUI;
        m_EndUI.gameObject.SetActive(false);

        _endUINormalScale = _endUI.localScale;
        if (_endUINormalScale.sqrMagnitude < 0.0001f)
        {
            _endUINormalScale = Vector3.one;
        }
    }

    private float GetEndUIScaleFactor()
    {
        if (m_EndUI == null)
        {
            return 1f;
        }

        float reference = Mathf.Max(Mathf.Abs(_endUINormalScale.x), 0.0001f);
        return m_EndUI.localScale.x / reference;
    }

    private void SetEndUIScaleFactor(float factor)
    {
        if (m_EndUI == null)
        {
            return;
        }

        m_EndUI.localScale = _endUINormalScale * Mathf.Clamp01(factor);
    }

    private void OnPostRender()
    {
        if (!isShowGridLine || lineMaterial == null || start3D == null || m_EndUI == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Camera is null");
            return;
        }

        Vector2 start = cam.WorldToViewportPoint(start3D.position);
        Vector2 end = cam.ScreenToViewportPoint(m_EndUI.position);
        Vector2 center = GetCenterPos(start, end);

        if (_drawProgress <= 0f)
        {
            return;
        }

        GL.PushMatrix();
        lineMaterial.SetPass(0);
        GL.LoadOrtho();
        GL.Begin(GL.LINES);

        if (_drawProgress >= 1f)
        {
            DrawFullPolyline(start, center, end);
        }
        else
        {
            DrawPartialPolylineLeftToRight(start, center, end, _drawProgress);
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

    private void DrawFullPolyline(Vector2 start, Vector2 center, Vector2 end)
    {
        GL.Vertex(start);
        GL.Vertex(center);
        GL.Vertex(center);
        GL.Vertex(end);
    }

    private void DrawPartialPolylineLeftToRight(Vector2 start, Vector2 center, Vector2 end, float progress)
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
