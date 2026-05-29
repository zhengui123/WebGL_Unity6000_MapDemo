using UnityEngine;

/// <summary>
/// 仅从项目内 ScriptableObject / 材质资源加载，不调用 Unity 内置 Default-Particle。
/// </summary>
public static class EarthPlateParticleMaterials
{
    private const string ConfigResourcesPath = "EarthPlateTransitionConfig";

    private static EarthPlateTransitionConfig _config;
    private static Texture2D _fallbackTexture;
    private static bool _loggedSource;

    public static void SetConfig(EarthPlateTransitionConfig config)
    {
        _config = config;
    }

    public static EarthPlateTransitionConfig GetConfig()
    {
        if (_config != null)
        {
            return _config;
        }

        _config = Resources.Load<EarthPlateTransitionConfig>(ConfigResourcesPath);
        return _config;
    }

    public static Material CreateSoftCloudMaterial(Color tint)
    {
        Material mat = InstantiateTemplate(GetConfig()?.ParticleCloudMaterial, tint);
        LogSourceOnce(mat, "云雾粒子");
        return mat;
    }

    public static Material CreateStreakMaterial(Color tint)
    {
        Material mat = InstantiateTemplate(GetConfig()?.ParticleStreakMaterial, tint);
        LogSourceOnce(mat, "俯冲速度线");
        return mat;
    }

    public static Material CreateTechScanWaveMaterial(Color main, Color accent)
    {
        EarthPlateTransitionConfig cfg = GetConfig();
        Material mat = cfg != null && cfg.TechScanWaveMaterial != null
            ? new Material(cfg.TechScanWaveMaterial)
            : new Material(Shader.Find("Custom/EarthPlateTransitionScanWorld"));

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", main);
        }

        if (mat.HasProperty("_AccentColor"))
        {
            mat.SetColor("_AccentColor", accent);
        }

        return mat;
    }

    public static Material CreateTechScanLineMaterial(Color accent)
    {
        Material template = GetConfig()?.TechScanLineMaterial;
        if (template != null)
        {
            Material mat = new Material(template);
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", accent);
            }

            return mat;
        }

        Material fallback = new Material(Shader.Find("Sprites/Default"));
        if (fallback.HasProperty("_Color"))
        {
            fallback.SetColor("_Color", accent);
        }

        return fallback;
    }

    public static Texture2D GetSoftParticleTexture()
    {
        EarthPlateTransitionConfig cfg = GetConfig();
        if (cfg != null && cfg.SoftParticleTexture != null)
        {
            return cfg.SoftParticleTexture;
        }

        if (_fallbackTexture != null)
        {
            return _fallbackTexture;
        }

        _fallbackTexture = CreateProceduralSoftCircle(128);
        Debug.LogWarning("[EarthPlateParticleMaterials] 未找到配置贴图，使用临时程序柔圆。请执行 Tools/地图/创建过渡动画资源。");
        return _fallbackTexture;
    }

    private static Material InstantiateTemplate(Material template, Color tint)
    {
        Material mat;
        if (template != null)
        {
            mat = new Material(template);
        }
        else
        {
            mat = CreateAdditiveMaterialFromTexture(GetSoftParticleTexture());
        }

        ApplyTint(mat, tint);
        return mat;
    }

    private static Material CreateAdditiveMaterialFromTexture(Texture2D tex)
    {
        Shader shader = Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Additive")
            ?? Shader.Find("Legacy Shaders/Particles/Additive");

        Material mat = new Material(shader);
        if (tex != null)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
            }
        }

        return mat;
    }

    private static void ApplyTint(Material mat, Color tint)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", tint);
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", tint);
        }
    }

    /// <summary>
    /// 标准粒子柔圆：RGB 恒为白，仅 Alpha 做径向渐变（Additive 粒子正确用法）。
    /// </summary>
    public static void FillSoftParticleCircle(Texture2D tex)
    {
        if (tex == null)
        {
            return;
        }

        int size = tex.width;
        float center = (size - 1) * 0.5f;
        float radius = center * 0.98f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = Mathf.Clamp01(d / radius);
                float a = (1f - t) * (1f - t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
    }

    public static Texture2D CreateProceduralSoftCircle(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "ProceduralSoftParticle_Runtime";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        FillSoftParticleCircle(tex);
        return tex;
    }

    private static void LogSourceOnce(Material mat, string label)
    {
        if (_loggedSource || mat == null)
        {
            return;
        }

        _loggedSource = true;
        string texName = mat.mainTexture != null ? mat.mainTexture.name : "无贴图";
        Debug.Log($"[EarthPlateParticleMaterials] {label} 材质={mat.name}, 贴图={texName}, Shader={mat.shader.name}");
    }
}
