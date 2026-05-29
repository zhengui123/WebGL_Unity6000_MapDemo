using UnityEngine;

/// <summary>
/// 过渡动画资源引用（贴图、材质、预制体），全部存放在 Assets/Transition 下。
/// </summary>
[CreateAssetMenu(fileName = "EarthPlateTransitionConfig", menuName = "地图/过渡动画配置")]
public class EarthPlateTransitionConfig : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Transition/Config/EarthPlateTransitionConfig.asset";

    [Header("贴图")]
    public Texture2D SoftParticleTexture;

    [Header("材质")]
    public Material ParticleCloudMaterial;
    public Material ParticleStreakMaterial;
    public Material TechScanWaveMaterial;
    public Material TechScanLineMaterial;

    [Header("预制体")]
    public GameObject CloudFogPrefab;
    public GameObject TechScanPrefab;
    public GameObject DiveRevealPrefab;
}
