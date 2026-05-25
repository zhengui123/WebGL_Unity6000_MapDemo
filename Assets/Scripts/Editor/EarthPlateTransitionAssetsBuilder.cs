#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成过渡动画贴图、材质、预制体与配置文件（不依赖 Default-Particle 内置资源）。
/// </summary>
public static class EarthPlateTransitionAssetsBuilder
{
    private const string Root = "Assets/Transition";
    private const string TexPngPath = Root + "/Textures/T_SoftParticle.png";
    private const string MatCloudPath = Root + "/Materials/M_ParticleCloud.mat";
    private const string MatStreakPath = Root + "/Materials/M_ParticleStreak.mat";
    private const string MatScanPath = Root + "/Materials/M_TechScanWave.mat";
    private const string MatLinePath = Root + "/Materials/M_TechScanLine.mat";
    private const string ConfigPath = Root + "/Config/EarthPlateTransitionConfig.asset";
    private const string ResourcesConfigPath = "Assets/Resources/EarthPlateTransitionConfig.asset";
    private const string PrefabCloud = Root + "/Prefabs/P_CloudFogTransition.prefab";
    private const string PrefabScan = Root + "/Prefabs/P_TechScanTransition.prefab";
    private const string PrefabDive = Root + "/Prefabs/P_DiveRevealTransition.prefab";

    [MenuItem("Tools/地图/创建过渡动画资源")]
    [MenuItem("Tools/Map/Create Transition Assets")]
    public static void BuildAll()
    {
        EnsureFolders();

        Texture2D tex = GenerateAndSaveSoftTexture();
        if (tex == null)
        {
            Debug.LogError("[地图] 贴图生成失败，已中止。");
            return;
        }

        Material matCloud = CreateParticleMaterial(MatCloudPath, tex, new Color(1f, 1f, 1f, 1f));
        Material matStreak = CreateParticleMaterial(MatStreakPath, tex, new Color(1f, 1f, 1f, 1f));
        Material matScan = CreateScanMaterial(MatScanPath);
        Material matLine = CreateLineMaterial(MatLinePath);

        GameObject cloudPrefab = BuildCloudFogPrefab(matCloud);
        GameObject scanPrefab = BuildTechScanPrefab(matScan, matLine);
        GameObject divePrefab = BuildDivePrefab(matStreak);

        EarthPlateTransitionConfig config = CreateOrLoadConfig();
        config.SoftParticleTexture = tex;
        config.ParticleCloudMaterial = matCloud;
        config.ParticleStreakMaterial = matStreak;
        config.TechScanWaveMaterial = matScan;
        config.TechScanLineMaterial = matLine;
        config.CloudFogPrefab = cloudPrefab;
        config.TechScanPrefab = scanPrefab;
        config.DiveRevealPrefab = divePrefab;

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CopyConfigToResources(config);

        AssignConfigToPlayers(config);

        Debug.Log("[地图] 过渡资源已生成：\n" +
                  $"- 贴图 {TexPngPath}\n" +
                  $"- 材质 {Root}/Materials/\n" +
                  $"- 预制体 {Root}/Prefabs/\n" +
                  $"- 配置 {ConfigPath}\n" +
                  $"- Resources {ResourcesConfigPath}");
    }

    private static void EnsureFolders()
    {
        string[] dirs =
        {
            Root,
            Root + "/Textures",
            Root + "/Materials",
            Root + "/Prefabs",
            Root + "/Config",
            "Assets/Resources"
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            if (!Directory.Exists(dirs[i]))
            {
                Directory.CreateDirectory(dirs[i]);
            }
        }
    }

    private static Texture2D GenerateAndSaveSoftTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        EarthPlateParticleMaterials.FillSoftParticleCircle(tex);

        Color center = tex.GetPixel(size / 2, size / 2);
        if (center.a < 0.9f)
        {
            Debug.LogError($"[地图] 程序生成柔圆失败，中心 Alpha={center.a:F2}");
            Object.DestroyImmediate(tex);
            return null;
        }

        byte[] png = tex.EncodeToPNG();
        if (png == null || png.Length < 800)
        {
            Debug.LogError($"[地图] PNG 编码失败，字节数={png?.Length ?? 0}");
            Object.DestroyImmediate(tex);
            return null;
        }

        File.WriteAllBytes(TexPngPath, png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TexPngPath, ImportAssetOptions.ForceUpdate);
        ConfigurePngImporter(TexPngPath);

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPngPath);
        Debug.Log($"[地图] 柔圆贴图已生成：中心 Alpha={center.a:F2}，PNG={png.Length} 字节，导入={imported != null}");
        return imported;
    }

    private static void ConfigurePngImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Material CreateParticleMaterial(string path, Texture2D tex, Color tint)
    {
        Shader shader = Shader.Find("Mobile/Particles/Additive")
            ?? Shader.Find("Particles/Additive")
            ?? Shader.Find("Legacy Shaders/Particles/Additive");

        Material mat = new Material(shader);
        mat.name = Path.GetFileNameWithoutExtension(path);
        mat.mainTexture = tex;
        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", tex);
        }

        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", tint);
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", tint);
        }

        return SaveMaterial(path, mat);
    }

    private static Material CreateScanMaterial(string path)
    {
        Shader shader = Shader.Find("Custom/EarthPlateTransitionScanWorld");
        Material mat = new Material(shader);
        mat.name = "M_TechScanWave";
        mat.SetColor("_Color", new Color(0.04f, 0.1f, 0.25f, 0.35f));
        mat.SetColor("_AccentColor", new Color(0.25f, 0.95f, 1f, 1f));
        mat.SetFloat("_GridStrength", 0.55f);
        return SaveMaterial(path, mat);
    }

    private static Material CreateLineMaterial(string path)
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.name = "M_TechScanLine";
        mat.SetColor("_Color", new Color(0.25f, 0.95f, 1f, 0.85f));
        return SaveMaterial(path, mat);
    }

    private static Material SaveMaterial(string path, Material mat)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            Object.DestroyImmediate(mat);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static GameObject BuildCloudFogPrefab(Material cloudMat)
    {
        GameObject root = new GameObject("P_CloudFogTransition");
        CreateFogPs(root.transform, "MistNear", cloudMat, 180f, 120f, 35f);
        CreateFogPs(root.transform, "CloudMid", cloudMat, 420f, 220f, 18f);
        CreateFogPs(root.transform, "HazeFar", cloudMat, 900f, 380f, 8f);
        return SavePrefab(root, PrefabCloud);
    }

    private static void CreateFogPs(Transform parent, string name, Material mat, float radius, float size, float speed)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5f;
        main.startSpeed = speed;
        main.startSize = size;
        main.maxParticles = 800;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.material = mat;
    }

    private static GameObject BuildTechScanPrefab(Material scanMat, Material lineMat)
    {
        GameObject root = new GameObject("P_TechScanTransition");

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.name = "ScanPlane";
        Object.DestroyImmediate(plane.GetComponent<Collider>());
        plane.transform.SetParent(root.transform, false);
        plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        plane.transform.localScale = new Vector3(6000f, 6000f, 1f);
        plane.GetComponent<MeshRenderer>().sharedMaterial = scanMat;

        GameObject ring = new GameObject("ScanRing");
        ring.transform.SetParent(root.transform, false);
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.widthMultiplier = 8f;
        line.positionCount = 64;
        line.material = lineMat;
        line.startColor = lineMat.color;
        line.endColor = lineMat.color;

        return SavePrefab(root, PrefabScan);
    }

    private static GameObject BuildDivePrefab(Material streakMat)
    {
        GameObject root = new GameObject("P_DiveRevealTransition");
        GameObject psGo = new GameObject("DiveStreaks");
        psGo.transform.SetParent(root.transform, false);
        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 25f;
        main.maxParticles = 1200;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.z = new ParticleSystem.MinMaxCurve(280f);
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.alignment = ParticleSystemRenderSpace.View;
        r.lengthScale = 3.5f;
        r.velocityScale = 0.2f;
        r.material = streakMat;

        return SavePrefab(root, PrefabDive);
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static EarthPlateTransitionConfig CreateOrLoadConfig()
    {
        EarthPlateTransitionConfig config = AssetDatabase.LoadAssetAtPath<EarthPlateTransitionConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EarthPlateTransitionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        return config;
    }

    private static void CopyConfigToResources(EarthPlateTransitionConfig source)
    {
        EarthPlateTransitionConfig copy = AssetDatabase.LoadAssetAtPath<EarthPlateTransitionConfig>(ResourcesConfigPath);
        if (copy == null)
        {
            copy = ScriptableObject.CreateInstance<EarthPlateTransitionConfig>();
            AssetDatabase.CreateAsset(copy, ResourcesConfigPath);
        }

        copy.SoftParticleTexture = source.SoftParticleTexture;
        copy.ParticleCloudMaterial = source.ParticleCloudMaterial;
        copy.ParticleStreakMaterial = source.ParticleStreakMaterial;
        copy.TechScanWaveMaterial = source.TechScanWaveMaterial;
        copy.TechScanLineMaterial = source.TechScanLineMaterial;
        copy.CloudFogPrefab = source.CloudFogPrefab;
        copy.TechScanPrefab = source.TechScanPrefab;
        copy.DiveRevealPrefab = source.DiveRevealPrefab;
        EditorUtility.SetDirty(copy);
    }

    private static void AssignConfigToPlayers(EarthPlateTransitionConfig config)
    {
        EarthPlateMapTransitionPlayer[] players = Object.FindObjectsOfType<EarthPlateMapTransitionPlayer>(true);
        for (int i = 0; i < players.Length; i++)
        {
            SerializedObject so = new SerializedObject(players[i]);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(players[i]);
        }
    }
}
#endif
