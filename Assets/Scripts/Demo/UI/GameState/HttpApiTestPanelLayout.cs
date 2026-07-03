using UnityEngine;

/// <summary>
/// 记录 HttpApiTestPanel 内 FormScrollView 的 RectTransform，供编辑器重建 UI 时复用。
/// </summary>
[DisallowMultipleComponent]
public class HttpApiTestPanelLayout : MonoBehaviour
{
    [SerializeField] private ControlStateJumpPanelLayout.RectLayoutData _formScrollViewLayout;

    public ControlStateJumpPanelLayout.RectLayoutData FormScrollViewLayout => _formScrollViewLayout;

    public void CaptureFormScrollView(RectTransform formScrollViewRect)
    {
        if (formScrollViewRect == null)
        {
            return;
        }

        _formScrollViewLayout = ControlStateJumpPanelLayout.CaptureFromRectTransform(formScrollViewRect);
    }

    public void ApplyFormScrollView(RectTransform formScrollViewRect)
    {
        if (formScrollViewRect == null)
        {
            return;
        }

        formScrollViewRect.anchorMin = _formScrollViewLayout.AnchorMin;
        formScrollViewRect.anchorMax = _formScrollViewLayout.AnchorMax;
        formScrollViewRect.pivot = _formScrollViewLayout.Pivot;
        formScrollViewRect.anchoredPosition = _formScrollViewLayout.AnchoredPosition;
        formScrollViewRect.sizeDelta = _formScrollViewLayout.SizeDelta;
        formScrollViewRect.localScale = _formScrollViewLayout.LocalScale;
    }

    /// <summary>首次创建 UI 时的 FormScrollView 默认布局（与当前场景手工调整值一致）。</summary>
    public static ControlStateJumpPanelLayout.RectLayoutData CreateDefaultFormScrollViewLayout()
    {
        return new ControlStateJumpPanelLayout.RectLayoutData
        {
            AnchorMin = new Vector2(0f, 1f),
            AnchorMax = new Vector2(0f, 1f),
            Pivot = new Vector2(0f, 1f),
            AnchoredPosition = new Vector2(12f, -88f),
            SizeDelta = new Vector2(336f, 320f),
            LocalScale = Vector3.one,
        };
    }
}
