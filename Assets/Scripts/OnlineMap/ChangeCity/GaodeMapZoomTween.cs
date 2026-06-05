using DG.Tweening;
using UnityEngine;

/// <summary>
/// Online Maps floatZoom 渐变驱动。
/// </summary>
public static class GaodeMapZoomTween
{
    public static Tween TweenFloatZoom(OnlineMaps map, float targetZoom, float duration, Ease ease = Ease.InOutQuad)
    {
        if (map == null)
        {
            return null;
        }

        float current = map.floatZoom;
        targetZoom = Mathf.Clamp(targetZoom, OnlineMaps.MINZOOM, OnlineMaps.MAXZOOM_EXT);

        if (duration <= 0f)
        {
            map.floatZoom = targetZoom;
            map.RedrawImmediately();
            return null;
        }

        return DOTween.To(() => current, value =>
            {
                current = value;
                map.floatZoom = value;
            }, targetZoom, duration)
            .SetEase(ease)
            .SetTarget(map)
            .OnComplete(() => map.RedrawImmediately());
    }
}
