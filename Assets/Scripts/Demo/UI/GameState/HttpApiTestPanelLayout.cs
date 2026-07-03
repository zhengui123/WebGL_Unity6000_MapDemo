using UnityEngine;

/// <summary>
/// HttpApiTestPanel 内 FormScrollView 的拉伸布局（四边留白，随面板尺寸自适应）。
/// </summary>
[DisallowMultipleComponent]
public class HttpApiTestPanelLayout : MonoBehaviour
{
    public static readonly Vector2 DefaultOffsetMin = new Vector2(12f, 12f);
    public static readonly Vector2 DefaultOffsetMax = new Vector2(-12f, -88f);

    [SerializeField] private Vector2 _offsetMin = DefaultOffsetMin;
    [SerializeField] private Vector2 _offsetMax = DefaultOffsetMax;

    public Vector2 OffsetMin => _offsetMin;
    public Vector2 OffsetMax => _offsetMax;

    public static bool IsValidRect(RectTransform formScrollViewRect)
    {
        if (formScrollViewRect == null)
        {
            return false;
        }

        if (formScrollViewRect.localScale.sqrMagnitude < 0.5f)
        {
            return false;
        }

        Rect rect = formScrollViewRect.rect;
        return rect.width > 10f && rect.height > 10f;
    }

    public static bool IsValidOffsets(Vector2 offsetMin, Vector2 offsetMax)
    {
        float horizontalInset = offsetMin.x - offsetMax.x;
        float verticalInset = offsetMin.y - offsetMax.y;
        return horizontalInset > 10f && verticalInset > 10f;
    }

    public static void ApplyFormScrollViewLayout(
        RectTransform formScrollViewRect,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (formScrollViewRect == null)
        {
            return;
        }

        formScrollViewRect.localScale = Vector3.one;
        formScrollViewRect.anchorMin = Vector2.zero;
        formScrollViewRect.anchorMax = Vector2.one;
        formScrollViewRect.pivot = new Vector2(0.5f, 0.5f);
        formScrollViewRect.anchoredPosition = Vector2.zero;
        formScrollViewRect.offsetMin = offsetMin;
        formScrollViewRect.offsetMax = offsetMax;
    }

    public static void ApplyDefaultFormScrollViewLayout(RectTransform formScrollViewRect)
    {
        ApplyFormScrollViewLayout(formScrollViewRect, DefaultOffsetMin, DefaultOffsetMax);
    }

    public void CaptureFormScrollView(RectTransform formScrollViewRect)
    {
        if (!IsValidRect(formScrollViewRect))
        {
            return;
        }

        _offsetMin = formScrollViewRect.offsetMin;
        _offsetMax = formScrollViewRect.offsetMax;
    }

    public void ApplyTo(RectTransform formScrollViewRect)
    {
        Vector2 offsetMin = IsValidOffsets(_offsetMin, _offsetMax) ? _offsetMin : DefaultOffsetMin;
        Vector2 offsetMax = IsValidOffsets(_offsetMin, _offsetMax) ? _offsetMax : DefaultOffsetMax;
        ApplyFormScrollViewLayout(formScrollViewRect, offsetMin, offsetMax);
    }
}
