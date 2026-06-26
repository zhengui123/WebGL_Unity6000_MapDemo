using System;
using UnityEngine;

/// <summary>
/// 记录操控状态跳转面板的 RectTransform 布局与样式，供重建 UI 时复用。
/// </summary>
[DisallowMultipleComponent]
public class ControlStateJumpPanelLayout : MonoBehaviour
{
    [Serializable]
    public struct RectLayoutData
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector3 LocalScale;
    }

    [SerializeField] private RectLayoutData _layout;
    [SerializeField] private Sprite _instantToggleCheckmarkSprite;

    public RectLayoutData Layout => _layout;
    public Sprite InstantToggleCheckmarkSprite => _instantToggleCheckmarkSprite;

    public void CaptureFrom(RectTransform rectTransform, Sprite instantToggleCheckmarkSprite = null)
    {
        if (rectTransform == null)
        {
            return;
        }

        _layout = new RectLayoutData
        {
            AnchorMin = rectTransform.anchorMin,
            AnchorMax = rectTransform.anchorMax,
            Pivot = rectTransform.pivot,
            AnchoredPosition = rectTransform.anchoredPosition,
            SizeDelta = rectTransform.sizeDelta,
            LocalScale = rectTransform.localScale,
        };
        _instantToggleCheckmarkSprite = instantToggleCheckmarkSprite;
    }

    public void ApplyTo(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = _layout.AnchorMin;
        rectTransform.anchorMax = _layout.AnchorMax;
        rectTransform.pivot = _layout.Pivot;
        rectTransform.anchoredPosition = _layout.AnchoredPosition;
        rectTransform.sizeDelta = _layout.SizeDelta;
        rectTransform.localScale = _layout.LocalScale;
    }

    public static RectLayoutData CreateDefault()
    {
        return new RectLayoutData
        {
            AnchorMin = new Vector2(0f, 1f),
            AnchorMax = new Vector2(0f, 1f),
            Pivot = new Vector2(0f, 1f),
            AnchoredPosition = new Vector2(16f, -16f),
            SizeDelta = new Vector2(360f, 360f),
            LocalScale = Vector3.one,
        };
    }

    /// <summary>从 RectTransform 快照布局数据（编辑器重建 UI 时使用）。</summary>
    public static RectLayoutData CaptureFromRectTransform(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return CreateDefault();
        }

        return new RectLayoutData
        {
            AnchorMin = rectTransform.anchorMin,
            AnchorMax = rectTransform.anchorMax,
            Pivot = rectTransform.pivot,
            AnchoredPosition = rectTransform.anchoredPosition,
            SizeDelta = rectTransform.sizeDelta,
            LocalScale = rectTransform.localScale,
        };
    }
}
