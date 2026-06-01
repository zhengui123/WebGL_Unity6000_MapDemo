#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成 CarPoint 高清发光贴图、材质与预制体（Assets/CarPoint）。
/// </summary>
public static class CarPointAssetsBuilder
{
    private const string Root = "Assets/CarPoint";
    private const string TexPath = Root + "/Textures/T_CarPointGlow_1024.png";
    private const string MatPath = Root + "/Materials/M_CarPointGlow.mat";
    private const string MatInstancedPath = Root + "/Materials/M_CarPointGlowInstanced.mat";
    private const string PrefabPath = Root + "/Prefabs/CarPoint.prefab";
    private const string ShaderPath = Root + "/Shaders/CarPointGlow.shader";
    private const string ShaderInstancedPath = Root + "/Shaders/CarPointGlowInstanced.shader";
    private const string ResInstancedMatPath = "Assets/Resources/CarPoint/M_CarPointGlowInstanced.mat";

    [MenuItem("Tools/地图/创建 CarPoint 发光资源")]
    public static void BuildAll()
    {
        EnsureFolders();

        Texture2D tex = GenerateAndSaveTexture();
        if (tex == null)
        {
            Debug.LogError("[CarPoint] 贴图生成失败。");
            return;
        }

        Material mat = CreateGlowMaterial(tex);
        Material matInstanced = CreateInstancedGlowMaterial(tex);
        GameObject prefab = CreateOrUpdatePrefab(mat);

        CopyPrefabToResources(prefab);
        CopyInstancedMaterialToResources(matInstanced);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        Debug.Log(
            $"[CarPoint] 资源已生成：\n- {TexPath}\n- {MatPath}\n- {PrefabPath}\n" +
            "请在 sd_map 的 PlateMapVehiclePointController 上将 Point Prefab 指向新预制体。");
    }

    private static void EnsureFolders()
    {
        string[] dirs =
        {
            Root,
            Root + "/Textures",
            Root + "/Materials",
            Root + "/Prefabs",
            Root + "/Shaders",
            Root + "/Editor"
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            if (!Directory.Exists(dirs[i]))
            {
                Directory.CreateDirectory(dirs[i]);
            }
        }
    }

    private static Texture2D GenerateAndSaveTexture()
    {
        const int size = CarPointGlowTextureUtility.DefaultTextureSize;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        CarPointGlowTextureUtility.FillGlowCircle(tex);

        Color center = tex.GetPixel(size / 2, size / 2);
        Color corner = tex.GetPixel(0, 0);
        if (center.a < 0.85f)
        {
            Debug.LogError($"[CarPoint] 中心 Alpha 过低: {center.a:F3}");
            Object.DestroyImmediate(tex);
            return null;
        }

        if (corner.a > 0.001f)
        {
            Debug.LogError($"[CarPoint] 四角未全透明，Alpha={corner.a:F4}");
            Object.DestroyImmediate(tex);
            return null;
        }

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        if (png == null || png.Length < 4096)
        {
            Debug.LogError("[CarPoint] PNG 编码异常。");
            return null;
        }

        File.WriteAllBytes(TexPath, png);
        AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporter(TexPath);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
    }

    private static void ConfigureTextureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    private static Material CreateGlowMaterial(Texture2D tex)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            shader = Shader.Find("Custom/CarPointGlow");
        }

        if (shader == null)
        {
            Debug.LogError("[CarPoint] 未找到 Custom/CarPointGlow 着色器。");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(shader);
        }
        else
        {
            mat.shader = shader;
        }

        mat.name = "M_CarPointGlow";
        mat.mainTexture = tex;
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", new Color(0.25f, 0.88f, 1f, 1f));
        mat.SetFloat("_GlowIntensity", 1.6f);
        ApplyFusionBlendDefaults(mat);
        mat.renderQueue = 3200;

        if (File.Exists(MatPath))
        {
            EditorUtility.SetDirty(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        return mat;
    }

    private static Material CreateInstancedGlowMaterial(Texture2D tex)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderInstancedPath);
        if (shader == null)
        {
            shader = Shader.Find("Custom/CarPointGlowInstanced");
        }

        if (shader == null)
        {
            Debug.LogError("[CarPoint] 未找到 Custom/CarPointGlowInstanced 着色器。");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatInstancedPath);
        if (mat == null)
        {
            mat = new Material(shader);
        }
        else
        {
            mat.shader = shader;
        }

        mat.name = "M_CarPointGlowInstanced";
        mat.enableInstancing = true;
        mat.mainTexture = tex;
        mat.SetTexture("_MainTex", tex);
        ApplyFusionBlendDefaults(mat);
        mat.renderQueue = 3200;

        if (File.Exists(MatInstancedPath))
        {
            EditorUtility.SetDirty(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, MatInstancedPath);
        }

        return mat;
    }

    private static void ApplyFusionBlendDefaults(Material mat)
    {
        if (mat == null)
        {
            return;
        }

        mat.SetFloat("_CenterBrightness", 1f);
        mat.SetFloat("_SelfColorWeight", 0.42f);
        mat.SetFloat("_FusionOpacity", 0.72f);
        mat.SetFloat("_AdditiveGlow", 0.18f);
    }

    private static void CopyInstancedMaterialToResources(Material mat)
    {
        const string resDir = "Assets/Resources/CarPoint";
        if (!Directory.Exists(resDir))
        {
            Directory.CreateDirectory(resDir);
        }

        if (File.Exists(ResInstancedMatPath))
        {
            AssetDatabase.DeleteAsset(ResInstancedMatPath);
        }

        AssetDatabase.CopyAsset(MatInstancedPath, ResInstancedMatPath);
    }

    private static GameObject CreateOrUpdatePrefab(Material mat)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Quad);
        root.name = "CarPoint";

        Object.DestroyImmediate(root.GetComponent<Collider>());

        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // 贴合 sd_map 表面：Quad 默认竖立，绕 X 轴放平（仅作贴图/网格资源参考，运行时由 GPU Instancing 绘制）
        root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        root.transform.localScale = Vector3.one;

        GameObject prefab;
        if (File.Exists(PrefabPath))
        {
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        else
        {
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }

        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CopyPrefabToResources(GameObject prefab)
    {
        const string resDir = "Assets/Resources/CarPoint";
        const string resPath = resDir + "/CarPoint.prefab";
        if (!Directory.Exists(resDir))
        {
            Directory.CreateDirectory(resDir);
        }

        if (File.Exists(resPath))
        {
            AssetDatabase.DeleteAsset(resPath);
        }

        AssetDatabase.CopyAsset(PrefabPath, resPath);
    }
}
#endif
