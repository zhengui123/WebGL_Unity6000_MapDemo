using UnityEngine;

// Built-in render pipeline; intended for a camera rendering the car on black.
// This outlines all opaque geometry visible to that camera. For a dashboard,
// isolate the car using the camera's culling mask and a dedicated RenderTexture.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class Car07HologramCamera : MonoBehaviour
{
    [Tooltip("Assign Car07HologramPost.shader explicitly so builds retain it.")]
    public Shader postShader;

    [Header("Visible edges (linear HDR values)")]
    public Vector3 lineColor = new Vector3(0f, 1.3f, 1.65f);
    [Range(0.5f, 3f)] public float lineWidth = 1.5f;
    [Range(0f, 3f)] public float lineStrength = 1f;
    [Range(0f, 1f)] public float normalStart = 0.045f;
    [Range(0f, 1f)] public float normalEnd = 0.20f;
    [Range(0f, 0.1f)] public float depthStart = 0.008f;
    [Range(0f, 0.1f)] public float depthEnd = 0.022f;

    [Header("Bloom (included; do not add a second bloom)")]
    [Range(0f, 2f)] public float bloomStrength = 0.35f;
    [Min(0f)] public float bloomThreshold = 0.88f;
    [Tooltip("Blur radius in half-resolution pixels; not Three.js Bloom.radius.")]
    [Range(0.5f, 4f)] public float blurRadius = 1.2f;
    [Range(1, 5)] public int blurIterations = 3;

    private Camera targetCamera;
    private Material material;

    private void Reset()
    {
        postShader = Shader.Find("Hidden/Car07/Hologram Post BuiltIn");
    }

    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
        targetCamera.allowHDR = true;
    }

    private void OnPreCull()
    {
        targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
    }

    private void DisposeMaterial()
    {
        if (!material) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        material = null;
    }

    private void OnDisable()
    {
        DisposeMaterial();
        // Leave the camera's depth flag intact: other effects may also need it.
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!postShader || !postShader.isSupported || targetCamera.stereoEnabled)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (material && material.shader != postShader) DisposeMaterial();
        if (!material)
            material = new Material(postShader) { hideFlags = HideFlags.HideAndDontSave };

        material.SetVector("_LineColor", new Vector4(lineColor.x, lineColor.y, lineColor.z, 0));
        material.SetFloat("_LineWidth", lineWidth);
        material.SetFloat("_LineStrength", lineStrength);
        material.SetFloat("_NormalStart", normalStart);
        material.SetFloat("_NormalEnd", normalEnd);
        material.SetFloat("_DepthStart", depthStart);
        material.SetFloat("_DepthEnd", depthEnd);
        material.SetFloat("_BloomStrength", bloomStrength);
        material.SetFloat("_BloomThreshold", bloomThreshold);

        RenderTexture outlined = null, bloom = null, ping = null;
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.DefaultHDR;
        try
        {
            outlined = RenderTexture.GetTemporary(source.width, source.height, 0, format);
            outlined.filterMode = FilterMode.Bilinear;
            outlined.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(source, outlined, material, 0);
            if (bloomStrength <= 0f)
            {
                Graphics.Blit(outlined, destination);
                return;
            }

            int width = Mathf.Max(1, source.width / 2);
            int height = Mathf.Max(1, source.height / 2);
            bloom = RenderTexture.GetTemporary(width, height, 0, format);
            ping = RenderTexture.GetTemporary(width, height, 0, format);
            bloom.filterMode = ping.filterMode = FilterMode.Bilinear;
            bloom.wrapMode = ping.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(outlined, bloom, material, 1);
            for (int i = 0; i < Mathf.Clamp(blurIterations, 1, 5); i++)
            {
                material.SetVector("_BlurDirection", new Vector4(blurRadius, 0, 0, 0));
                Graphics.Blit(bloom, ping, material, 2);
                material.SetVector("_BlurDirection", new Vector4(0, blurRadius, 0, 0));
                Graphics.Blit(ping, bloom, material, 2);
            }
            material.SetTexture("_BloomTex", bloom);
            Graphics.Blit(outlined, destination, material, 3);
        }
        finally
        {
            material.SetTexture("_BloomTex", null);
            if (outlined) RenderTexture.ReleaseTemporary(outlined);
            if (bloom) RenderTexture.ReleaseTemporary(bloom);
            if (ping) RenderTexture.ReleaseTemporary(ping);
        }
    }
}
