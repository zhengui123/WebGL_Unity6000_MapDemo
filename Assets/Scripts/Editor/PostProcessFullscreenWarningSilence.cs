#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 关闭 PPv2「expects to be used on a fullscreen Camera」警告。
/// PackageCache 被还原后，编辑器加载 / 打包前会再次注释该 LogWarning。
/// </summary>
[InitializeOnLoad]
public static class PostProcessFullscreenWarningSilence
{
    private const string WarningNeedle =
        "When used with builtin render pipeline, Postprocessing package expects to be used on a fullscreen Camera";

    static PostProcessFullscreenWarningSilence()
    {
        EditorApplication.delayCall += () => TrySilence();
    }

    [MenuItem("Tools/PostProcessing/关闭全屏相机 Viewport 警告")]
    private static void MenuSilence()
    {
        if (TrySilence())
        {
            Debug.Log("[PostProcessFullscreenWarningSilence] 已关闭全屏相机 Viewport 警告。");
        }
        else
        {
            Debug.LogWarning("[PostProcessFullscreenWarningSilence] 未找到可修改的 PostProcessLayer.cs，或已关闭。");
        }
    }

    public static bool TrySilence()
    {
        string packageCache = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));
        if (!Directory.Exists(packageCache))
        {
            return false;
        }

        bool changedAny = false;
        string[] dirs = Directory.GetDirectories(packageCache, "com.unity.postprocessing@*");
        for (int i = 0; i < dirs.Length; i++)
        {
            string layerPath = Path.Combine(dirs[i], "PostProcessing", "Runtime", "PostProcessLayer.cs");
            if (File.Exists(layerPath) && SilenceFile(layerPath))
            {
                changedAny = true;
            }
        }

        return changedAny;
    }

    private static bool SilenceFile(string path)
    {
        string text = File.ReadAllText(path);
        if (!text.Contains(WarningNeedle))
        {
            return false;
        }

        if (!HasActiveFullscreenWarningLog(text))
        {
            return false;
        }

        const string oldBlock =
            "                Rect r = m_Camera.rect;\r\n" +
            "                if (Mathf.Abs(r.x) > 1e-6f || Mathf.Abs(r.y) > 1e-6f || Mathf.Abs(1.0f - r.width) > 1e-6f || Mathf.Abs(1.0f - r.height) > 1e-6f)\r\n" +
            "                {\r\n" +
            "                    Debug.LogWarning(\"When used with builtin render pipeline, Postprocessing package expects to be used on a fullscreen Camera.\\nPlease note that using Camera viewport may result in visual artefacts or some things not working.\", m_Camera);\r\n" +
            "                }";

        const string oldBlockLf =
            "                Rect r = m_Camera.rect;\n" +
            "                if (Mathf.Abs(r.x) > 1e-6f || Mathf.Abs(r.y) > 1e-6f || Mathf.Abs(1.0f - r.width) > 1e-6f || Mathf.Abs(1.0f - r.height) > 1e-6f)\n" +
            "                {\n" +
            "                    Debug.LogWarning(\"When used with builtin render pipeline, Postprocessing package expects to be used on a fullscreen Camera.\\nPlease note that using Camera viewport may result in visual artefacts or some things not working.\", m_Camera);\n" +
            "                }";

        const string newBlock =
            "                // 项目使用非全屏 Viewport（FogCamera）；关闭该警告以免 WebGL/移动端每帧刷屏。\n" +
            "                // Rect r = m_Camera.rect;\n" +
            "                // if (Mathf.Abs(r.x) > 1e-6f || Mathf.Abs(r.y) > 1e-6f || Mathf.Abs(1.0f - r.width) > 1e-6f || Mathf.Abs(1.0f - r.height) > 1e-6f)\n" +
            "                // {\n" +
            "                //     Debug.LogWarning(\"When used with builtin render pipeline, Postprocessing package expects to be used on a fullscreen Camera.\\nPlease note that using Camera viewport may result in visual artefacts or some things not working.\", m_Camera);\n" +
            "                // }";

        string next = text;
        if (text.Contains(oldBlock))
        {
            next = text.Replace(oldBlock, newBlock);
        }
        else if (text.Contains(oldBlockLf))
        {
            next = text.Replace(oldBlockLf, newBlock);
        }
        else
        {
            string warnLine =
                "                    Debug.LogWarning(\"When used with builtin render pipeline, Postprocessing package expects to be used on a fullscreen Camera.\\nPlease note that using Camera viewport may result in visual artefacts or some things not working.\", m_Camera);";
            if (!text.Contains(warnLine))
            {
                return false;
            }

            next = text.Replace(warnLine, "// " + warnLine.TrimStart());
        }

        if (next == text)
        {
            return false;
        }

        File.WriteAllText(path, next);
        return true;
    }

    private static bool HasActiveFullscreenWarningLog(string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            int found = text.IndexOf(WarningNeedle, index, System.StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            int lineStart = text.LastIndexOf('\n', found) + 1;
            string line = text.Substring(lineStart, found - lineStart);
            if (!line.TrimStart().StartsWith("//", System.StringComparison.Ordinal))
            {
                return true;
            }

            index = found + WarningNeedle.Length;
        }

        return false;
    }
}

///// <summary>打包前再次确保已关闭上述警告。</summary>
//public sealed class PostProcessFullscreenWarningSilenceBuildHook : IPreprocessBuildWithReport
//{
//    public int callbackOrder => 0;

//    public void OnPreprocessBuild(BuildReport report)
 //   {
 //       PostProcessFullscreenWarningSilence.TrySilence();
 //   }
//}
#endif
