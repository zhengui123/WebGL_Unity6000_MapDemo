using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏扫描线叠层（Screen Space Overlay + RawImage）。
/// </summary>
[DisallowMultipleComponent]
public class PlateToGaodeMapScanlineOverlay : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ScanIntensityId = Shader.PropertyToID("_ScanIntensity");

    [SerializeField] private Material _overlayMaterial;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private float _scanIntensity = 0.85f;

    private Tween _progressTween;

    private void Awake()
    {
        EnsureOverlayMaterial();
        EnsureUi();
        SetVisible(false);
    }

    private void EnsureOverlayMaterial()
    {
        if (_overlayMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/PlateToGaodeScanlineOverlay");
        if (shader != null)
        {
            _overlayMaterial = new Material(shader);
        }
    }

    private void OnDestroy()
    {
        KillProgressTween();
    }

    public void SetVisible(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.enabled = visible;
        }
    }

    public void SetProgressImmediate(float progress)
    {
        if (_overlayMaterial == null)
        {
            return;
        }

        _overlayMaterial.SetFloat(ProgressId, Mathf.Clamp01(progress));
        _overlayMaterial.SetFloat(ScanIntensityId, _scanIntensity);
    }

    public Tween TweenProgress(float to, float duration, Ease ease = Ease.InOutCubic)
    {
        KillProgressTween();
        if (_overlayMaterial == null)
        {
            return null;
        }

        float from = _overlayMaterial.GetFloat(ProgressId);
        _progressTween = DOTween.To(
            () => from,
            v =>
            {
                from = v;
                SetProgressImmediate(v);
            },
            Mathf.Clamp01(to),
            duration
        ).SetEase(ease).SetTarget(this);
        return _progressTween;
    }

    public void KillProgressTween()
    {
        if (_progressTween != null && _progressTween.IsActive())
        {
            _progressTween.Kill();
        }

        _progressTween = null;
    }

    private void EnsureUi()
    {
        if (_canvas != null && _rawImage != null)
        {
            return;
        }

        GameObject root = new GameObject("PlateToGaodeScanlineCanvas");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;
        root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject imageGo = new GameObject("ScanlineRawImage");
        imageGo.transform.SetParent(root.transform, false);
        RectTransform rt = imageGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _rawImage = imageGo.AddComponent<RawImage>();
        _rawImage.raycastTarget = false;
        if (_overlayMaterial != null)
        {
            _rawImage.material = _overlayMaterial;
            _rawImage.texture = Texture2D.whiteTexture;
            _rawImage.color = Color.white;
        }
    }
}
