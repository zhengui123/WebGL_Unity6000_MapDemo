#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// Car 场景：Post Process Volume（自动曝光 + Bloom），配合 HDR 边缘线。
/// 需已安装 com.unity.postprocessing（Built-in 管线）。
/// </summary>
public static class CarScenePostProcessEditor
{
    private const string CarScenePath = "Assets/Scenes/Car.unity";
    private const string ProfilePath = "Assets/Settings/Car/CarPostProcessProfile.asset";
    private const string VolumeObjectName = "Car Post Process Volume";

    [MenuItem("Tools/Car/场景：添加后期 Volume（曝光 + Bloom）")]
    public static void SetupCarScenePostProcess()
    {
        PostProcessProfile profile = GetOrCreateProfile();
        if (profile == null)
        {
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Car.unity"))
        {
            if (File.Exists(CarScenePath))
            {
                scene = EditorSceneManager.OpenScene(CarScenePath, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError("[CarScenePostProcess] 请先打开 Car 场景。");
                return;
            }
        }

        EnsureVolumeInScene(profile);
        EnsureLayerOnMainCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CarScenePostProcess] 已配置全局 Post Process Volume（自动曝光 + Bloom）。请确认 Main Camera 已勾选 HDR。");
    }

    private static PostProcessProfile GetOrCreateProfile()
    {
        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
        if (profile != null)
        {
            ApplyProfileSettings(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        string dir = Path.GetDirectoryName(ProfilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        ApplyProfileSettings(profile);
        AssetDatabase.CreateAsset(profile, ProfilePath);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void ApplyProfileSettings(PostProcessProfile profile)
    {
        AutoExposure autoExposure = profile.GetSetting<AutoExposure>();
        if (autoExposure == null)
        {
            autoExposure = profile.AddSettings<AutoExposure>();
        }

        autoExposure.enabled.Override(true);
        autoExposure.filtering.Override(new Vector2(0.35f, 0.95f));
        autoExposure.minLuminance.Override(-1f);
        autoExposure.maxLuminance.Override(1f);
        autoExposure.keyValue.Override(0.5f);
        autoExposure.eyeAdaptation.Override(EyeAdaptation.Progressive);
        autoExposure.speedUp.Override(2f);
        autoExposure.speedDown.Override(1f);

        Bloom bloom = profile.GetSetting<Bloom>();
        if (bloom == null)
        {
            bloom = profile.AddSettings<Bloom>();
        }

        bloom.enabled.Override(true);
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(0.9f);
        bloom.softKnee.Override(0.55f);
        bloom.diffusion.Override(7f);
        bloom.fastMode.Override(false);

        ColorGrading grading = profile.GetSetting<ColorGrading>();
        if (grading == null)
        {
            grading = profile.AddSettings<ColorGrading>();
        }

        grading.enabled.Override(true);
        grading.postExposure.Override(0.35f);
        //grading.toneMode.Override(ToneMapping.ACES);
    }

    private static void EnsureVolumeInScene(PostProcessProfile profile)
    {
        PostProcessVolume existing = Object.FindFirstObjectByType<PostProcessVolume>();
        GameObject volumeGo;

        if (existing != null)
        {
            volumeGo = existing.gameObject;
        }
        else
        {
            volumeGo = new GameObject(VolumeObjectName);
            Undo.RegisterCreatedObjectUndo(volumeGo, "Create Car Post Process Volume");
            existing = volumeGo.AddComponent<PostProcessVolume>();
        }

        existing.isGlobal = true;
        existing.weight = 1f;
        existing.priority = 0f;
        existing.sharedProfile = profile;
        EditorUtility.SetDirty(existing);
    }

    private static void EnsureLayerOnMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindFirstObjectByType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogWarning("[CarScenePostProcess] 场景中未找到 Camera。");
            return;
        }

        cam.allowHDR = true;

        PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            layer = Undo.AddComponent<PostProcessLayer>(cam.gameObject);
        }

        layer.volumeLayer = -1;
        layer.volumeTrigger = cam.transform;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
        EditorUtility.SetDirty(layer);
        EditorUtility.SetDirty(cam);
    }
}
#endif
