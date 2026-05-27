#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 分析 mercedes 模型子网格/材质，创建全息材质并应用到场景或 FBX 实例。
/// </summary>
public static class MercedesHologramEditor
{
    private const string FbxPath = "Assets/Model/Car/mercedes.fbx";
    private const string ShaderPath = "Assets/Shaders/Car/CarHologram.shader";
    private const string MaterialPath = "Assets/Materials/Car/M_MercedesHologram.mat";
    private const string ReportPath = "Assets/Model/Car/Mercedes_MeshMaterialAnalysis.md";
    private const string CarScenePath = "Assets/Scenes/Car.unity";

    [MenuItem("Tools/Car/分析 Mercedes 材质与子网格")]
    public static void AnalyzeMercedes()
    {
        string report = BuildAnalysisReport();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Model/Car");
        File.WriteAllText(ReportPath, report, Encoding.UTF8);
        Debug.Log(report);
        Debug.Log($"分析报告已保存: {ReportPath}");
    }

    [MenuItem("Tools/Car/创建全息材质并应用到 Mercedes")]
    public static void CreateAndApplyHologramMaterial()
    {
        Material mat = GetOrCreateHologramMaterial();
        if (mat == null)
        {
            Debug.LogError("全息材质创建失败，请确认 Shader 已编译: " + ShaderPath);
            return;
        }

        int count = ApplyMaterialToMercedesInOpenScenes(mat);
        AnalyzeMercedes();
        Debug.Log($"已将 {MaterialPath} 应用到 {count} 个 MeshRenderer。");
    }

    [MenuItem("Tools/Car/重新导入 Mercedes（应用 FBX 材质映射）")]
    public static void ReimportMercedesWithRemap()
    {
        GetOrCreateHologramMaterial();
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        AnalyzeMercedes();
        Debug.Log("Mercedes FBX 已重新导入，8 个内嵌材质已映射到 M_MercedesHologram。");
    }

    private static Material GetOrCreateHologramMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? "Assets/Materials/Car");
            mat = new Material(shader) { name = "M_MercedesHologram" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        ApplyDefaultMaterialValues(mat);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void ApplyDefaultMaterialValues(Material mat)
    {
        // 与期望图风格更接近：轮廓线更“霓虹”、扫描是带状扫过、远处依然清晰可见
        mat.SetColor("_BaseColor", new Color(0.05f, 0.25f, 0.45f, 1f));
        mat.SetColor("_GlowColor", new Color(0.25f, 1.1f, 1.6f, 1f));
        mat.SetFloat("_Alpha", 0.55f);
        mat.SetFloat("_FillStrength", 0.65f);
        mat.SetFloat("_FacingMinGlow", 0.15f);
        mat.SetFloat("_FresnelPower", 1.6f);
        mat.SetFloat("_FresnelIntensity", 2.8f);
        mat.SetFloat("_RimAlphaBoost", 0.55f);
        mat.SetFloat("_GridScale", 34f);
        mat.SetFloat("_GridLineWidth", 0.045f);
        mat.SetFloat("_GridIntensity", 1.4f);
        mat.SetFloat("_GridWorldScale", 0.55f);
        mat.SetFloat("_ScanSpeed", 0.8f);
        mat.SetFloat("_ScanDensity", 24f);
        mat.SetFloat("_ScanIntensity", 0.28f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private static int ApplyMaterialToMercedesInOpenScenes(Material mat)
    {
        int total = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsMercedesRoot(root.name))
                {
                    continue;
                }

                total += ApplyToHierarchy(root, mat);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        if (total == 0 && File.Exists(CarScenePath.Replace('/', Path.DirectorySeparatorChar)))
        {
            Scene scene = EditorSceneManager.OpenScene(CarScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsMercedesRoot(root.name))
                {
                    continue;
                }

                total += ApplyToHierarchy(root, mat);
            }

            EditorSceneManager.SaveScene(scene);
        }

        return total;
    }

    private static bool IsMercedesRoot(string name)
    {
        return name.IndexOf("mercedes", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ApplyToHierarchy(GameObject root, Material mat)
    {
        int count = 0;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }

            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);
            count++;
        }

        return count;
    }

    private static string BuildAnalysisReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mercedes 模型 — 材质与子网格分析");
        sb.AppendLine();
        sb.AppendLine("> 由 `Tools/Car/分析 Mercedes 材质与子网格` 自动生成");
        sb.AppendLine();
        sb.AppendLine("## 参考图材质关键词");
        sb.AppendLine();
        sb.AppendLine("| 关键词 | 说明 |");
        sb.AppendLine("|--------|------|");
        sb.AppendLine("| Hologram / 全息 | 半透明数字投影感 |");
        sb.AppendLine("| Fresnel / Rim | 边缘比正面更亮，勾勒轮廓 |");
        sb.AppendLine("| Wireframe / Grid | 表面网格线随几何起伏 |");
        sb.AppendLine("| Emission / Bloom | 青蓝色自发光，需后处理 Bloom |");
        sb.AppendLine("| Transparent / Alpha | 可透视内饰结构 |");
        sb.AppendLine("| Scanlines | 轻微水平扫描纹 |");
        sb.AppendLine("| Cyberpunk / Sci-fi | 整体青蓝霓虹配色 |");
        sb.AppendLine();
        sb.AppendLine("## FBX 内嵌材质（Blender 导出）");
        sb.AppendLine();
        sb.AppendLine("| 材质名 | 典型部位 |");
        sb.AppendLine("|--------|----------|");
        sb.AppendLine("| body | 车身外壳 |");
        sb.AppendLine("| glass | 玻璃 |");
        sb.AppendLine("| interior plastic | 内饰塑料 |");
        sb.AppendLine("| underbody.003 | 底盘 |");
        sb.AppendLine("| wheel1 ~ wheel4 | 四个车轮 |");
        sb.AppendLine();
        sb.AppendLine("贴图引用：`illinoisplatemerc.jpg`、`plate_normal_merc.png`（车牌）");
        sb.AppendLine();
        sb.AppendLine("## Unity 导入后 MeshRenderer 明细");
        sb.AppendLine();

        GameObject modelRoot = LoadMercedesModelRoot();
        if (modelRoot == null)
        {
            sb.AppendLine("_未能加载模型，请确认路径存在：`" + FbxPath + "`_");
            return sb.ToString();
        }

        MeshRenderer[] renderers = modelRoot.GetComponentsInChildren<MeshRenderer>(true);
        sb.AppendLine($"共 **{renderers.Length}** 个 MeshRenderer");
        sb.AppendLine();

        var table = new List<(string path, string mesh, int subMeshes, string materials)>();
        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            int subCount = mesh != null ? mesh.subMeshCount : 0;
            string matNames = FormatMaterials(renderer.sharedMaterials);
            string meshName = mesh != null ? mesh.name : "(无 Mesh)";
            table.Add((GetPath(renderer.transform, modelRoot.transform), meshName, subCount, matNames));
        }

        sb.AppendLine("| 节点路径 | Mesh | SubMesh 数 | 当前材质 |");
        sb.AppendLine("|----------|------|------------|----------|");
        foreach (var row in table)
        {
            sb.AppendLine($"| {row.path} | {row.mesh} | {row.subMeshes} | {row.materials} |");
        }

        Object.DestroyImmediate(modelRoot);
        sb.AppendLine();
        sb.AppendLine("## 项目内全息材质");
        sb.AppendLine();
        sb.AppendLine($"- Shader: `{ShaderPath}`");
        sb.AppendLine($"- Material: `{MaterialPath}`");
        sb.AppendLine("- 菜单: `Tools/Car/创建全息材质并应用到 Mercedes`");
        sb.AppendLine();
        sb.AppendLine("## 建议");
        sb.AppendLine();
        sb.AppendLine("1. 主相机 Background 设为深色；开启 **Bloom**（URP/HDRP 或 Post Processing）增强发光。");
        sb.AppendLine("2. 玻璃子网格如需更透，可复制材质调低 `_Alpha` / `_FillStrength`。");
        sb.AppendLine("3. 当前工具对所有子网格使用同一全息材质，与参考图统一风格一致。");

        return sb.ToString();
    }

    private static GameObject LoadMercedesModelRoot()
    {
        Object asset = AssetDatabase.LoadMainAssetAtPath(FbxPath);
        if (asset == null)
        {
            return null;
        }

        return Object.Instantiate(asset) as GameObject;
    }

    private static string GetPath(Transform t, Transform root)
    {
        var names = new List<string>();
        while (t != null && t != root.parent)
        {
            names.Add(t.name);
            t = t.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string FormatMaterials(Material[] materials)
    {
        if (materials == null || materials.Length == 0)
        {
            return "(无)";
        }

        var names = new List<string>();
        foreach (Material m in materials)
        {
            names.Add(m != null ? m.name : "null");
        }

        return string.Join(", ", names);
    }
}
#endif
