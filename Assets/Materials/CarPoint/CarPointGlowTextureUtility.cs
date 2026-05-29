using UnityEngine;

/// <summary>
/// 车辆点位发光贴图：A=径向 Alpha（外缘必须为 0 全透明）；R=核心遮罩（仅中心随亮度变化）。
/// </summary>
public static class CarPointGlowTextureUtility
{
    public const int DefaultTextureSize = 1024;

    /// <summary>
    /// 生成发光贴图。外圈 Alpha 严格衰减到 0，避免四周半透明晕边。
    /// </summary>
    public static void FillGlowCircle(
        Texture2D tex,
        float glowRadius01 = 0.55f,
        float transparentFrom01 = 0.72f)
    {
        if (tex == null)
        {
            return;
        }

        int size = tex.width;
        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = Mathf.Clamp01(d / radius);

                // 外缘全透明：超过 transparentFrom 后 Alpha 恒为 0
                float alpha = 0f;
                if (t < transparentFrom01)
                {
                    float u = t / transparentFrom01;
                    float body = 1f - u;
                    alpha = body * body * body;
                }

                // 核心遮罩：仅中心响应亮度（与 Alpha 形状分离）
                float coreMask = 0f;
                if (t < glowRadius01)
                {
                    float u = t / glowRadius01;
                    coreMask = 1f - u * u;
                }

                tex.SetPixel(x, y, new Color(coreMask, coreMask, coreMask, alpha));
            }
        }

        tex.Apply();
    }

    public static Texture2D CreateProcedural(int size = DefaultTextureSize)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "CarPointGlow_Runtime";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        FillGlowCircle(tex);
        return tex;
    }
}
