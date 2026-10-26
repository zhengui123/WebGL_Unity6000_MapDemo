using UnityEngine;

/// <summary>
/// Built-in 全息描边后处理。effectStrength 与 KJ_Car 溶解可见度同步：
/// visible = 1 - DissolveAmount（0=溶尽，1=全显）。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class Car07HologramCamera : MonoBehaviour
{
    [Tooltip("Assign Car07HologramPost.shader explicitly so builds retain it.")]
    public Shader postShader;

    [Header("效果强度")]
    [Tooltip("0=不显示后处理效果，1=描边与 Bloom 全开；运行时跟随 KJ_Car 溶解")]
    [Range(0f, 1f)]
    public float effectStrength;

    [Header("Clipping")]
    [Tooltip("effectStrength>0 时写入 Camera.farClipPlane；=0 时恢复缓存默认值")]
    [SerializeField] private float _hologramFarClip = 1000f;

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
    private float _defaultFarClip;
    private bool _hasCachedDefaultFar;

    private void Reset()
    {
        postShader = Shader.Find("Hidden/Car07/Hologram Post BuiltIn");
        effectStrength = 0f;
        _hologramFarClip = 1000f;
    }

    private void Awake()
    {
        effectStrength = 0f;
        CacheDefaultFarClip();
    }

    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
        targetCamera.allowHDR = true;
        CacheDefaultFarClip();

        if (!Application.isPlaying)
        {
            return;
        }

        SubscribeTransitionEvents();
        // 热重载/中途启用：按当前层级对齐；溶解过程中会由事件覆盖
        SyncStrengthToGameManagerImmediate();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            UnsubscribeTransitionEvents();
        }

        // 禁用时恢复默认 far，避免残留 1000
        RestoreDefaultFarClip();
        DisposeMaterial();
    }

    private void OnPreCull()
    {
        if (targetCamera != null)
        {
            targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
        }
    }

    private void SubscribeTransitionEvents()
    {
        CarModelDissolveController.OnKjCarVisibleStrengthChanged += HandleKjVisibleStrengthChanged;

        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        // 车辆级到达/KJ 显现完成：强制显示终态（strength=1 + far=全息）
        em.OnPlateToVehicleViewTransitionCompleted += HandleEnterVehicleCompleted;
        em.OnVehicleToPartTransitionReverseCompleted += HandleEnterVehicleCompleted;
        em.OnAttackPathToVehicleTransitionCompleted += HandleEnterVehicleCompletedVoid;
        em.OnCarSwitchToKjCarCompleted += HandleEnterVehicleCompletedVoid;

        // 离开车辆完成：强制隐藏终态
        em.OnVehicleToPlateViewTransitionCompleted += HandleLeaveVehicleCompleted;
        em.OnVehicleToPartTransitionCompleted += HandleLeaveVehicleCompleted;
        em.OnVehicleToAttackPathTransitionCompleted += HandleLeaveVehicleCompletedVoid;
        em.OnCarSwitchToRealyCarCompleted += HandleLeaveVehicleCompletedVoid;
    }

    private void UnsubscribeTransitionEvents()
    {
        CarModelDissolveController.OnKjCarVisibleStrengthChanged -= HandleKjVisibleStrengthChanged;

        EventManager em = EventManager.Instance;
        if (em == null)
        {
            return;
        }

        em.OnPlateToVehicleViewTransitionCompleted -= HandleEnterVehicleCompleted;
        em.OnVehicleToPartTransitionReverseCompleted -= HandleEnterVehicleCompleted;
        em.OnAttackPathToVehicleTransitionCompleted -= HandleEnterVehicleCompletedVoid;
        em.OnCarSwitchToKjCarCompleted -= HandleEnterVehicleCompletedVoid;

        em.OnVehicleToPlateViewTransitionCompleted -= HandleLeaveVehicleCompleted;
        em.OnVehicleToPartTransitionCompleted -= HandleLeaveVehicleCompleted;
        em.OnVehicleToAttackPathTransitionCompleted -= HandleLeaveVehicleCompletedVoid;
        em.OnCarSwitchToRealyCarCompleted -= HandleLeaveVehicleCompletedVoid;
    }

    private void HandleKjVisibleStrengthChanged(float visibleStrength)
    {
        SetEffectStrengthImmediate(visibleStrength);
    }

    private void HandleEnterVehicleCompleted(string _) => ApplyVehicleDisplayFinalState();

    private void HandleLeaveVehicleCompleted(string _) => ApplyVehicleHiddenFinalState();

    private void HandleEnterVehicleCompletedVoid() => ApplyVehicleDisplayFinalState();

    private void HandleLeaveVehicleCompletedVoid() => ApplyVehicleHiddenFinalState();

    /// <summary>车辆显示终态：全息全开 + far=全息远裁。</summary>
    private void ApplyVehicleDisplayFinalState()
    {
        SetEffectStrengthImmediate(1f);
    }

    /// <summary>离开车辆终态：关闭全息 + far 恢复默认。</summary>
    private void ApplyVehicleHiddenFinalState()
    {
        SetEffectStrengthImmediate(0f);
    }

    /// <summary>按 GameManager 当前态瞬时对齐（无过渡时）；仅车辆级=显示终态。</summary>
    private void SyncStrengthToGameManagerImmediate()
    {
        GameManager gm = GameManager.Instance;
        bool atVehicle = gm != null && gm.CurrentState == GameManager.ControlState.VehicleLevel;
        if (atVehicle)
        {
            ApplyVehicleDisplayFinalState();
        }
        else
        {
            ApplyVehicleHiddenFinalState();
        }
    }

    private void SetEffectStrengthImmediate(float value)
    {
        effectStrength = Mathf.Clamp01(value);

        ApplyFarClipForStrength();
    }

    private void CacheDefaultFarClip()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null || _hasCachedDefaultFar)
        {
            return;
        }

        _defaultFarClip = targetCamera.farClipPlane;
        _hasCachedDefaultFar = true;
    }

    /// <summary>strength≈0 → 默认 far；否则 → 全息 far（默认 1000）。</summary>
    private void ApplyFarClipForStrength()
    {
        if (targetCamera == null)
        {
            return;
        }

        CacheDefaultFarClip();
        targetCamera.farClipPlane = effectStrength <= 0.0001f
            ? _defaultFarClip
            : Mathf.Max(0.01f, _hologramFarClip);
    }

    private void RestoreDefaultFarClip()
    {
        if (targetCamera == null || !_hasCachedDefaultFar)
        {
            return;
        }

        targetCamera.farClipPlane = _defaultFarClip;
    }

    private void DisposeMaterial()
    {
        if (!material)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }

        material = null;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        float strength = Mathf.Clamp01(effectStrength);
        if (strength <= 0.0001f || !postShader || !postShader.isSupported ||
            targetCamera == null || targetCamera.stereoEnabled)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (material && material.shader != postShader)
        {
            DisposeMaterial();
        }

        if (!material)
        {
            material = new Material(postShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        float scaledLineStrength = lineStrength * strength;
        float scaledBloomStrength = bloomStrength * strength;

        material.SetVector("_LineColor", new Vector4(lineColor.x, lineColor.y, lineColor.z, 0));
        material.SetFloat("_LineWidth", lineWidth);
        material.SetFloat("_LineStrength", scaledLineStrength);
        material.SetFloat("_NormalStart", normalStart);
        material.SetFloat("_NormalEnd", normalEnd);
        material.SetFloat("_DepthStart", depthStart);
        material.SetFloat("_DepthEnd", depthEnd);
        material.SetFloat("_BloomStrength", scaledBloomStrength);
        material.SetFloat("_BloomThreshold", bloomThreshold);

        RenderTexture outlined = null;
        RenderTexture bloom = null;
        RenderTexture ping = null;
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf
            : RenderTextureFormat.DefaultHDR;

        try
        {
            outlined = RenderTexture.GetTemporary(source.width, source.height, 0, format);
            outlined.filterMode = FilterMode.Bilinear;
            outlined.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(source, outlined, material, 0);
            if (scaledBloomStrength <= 0f)
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
            if (outlined)
            {
                RenderTexture.ReleaseTemporary(outlined);
            }

            if (bloom)
            {
                RenderTexture.ReleaseTemporary(bloom);
            }

            if (ping)
            {
                RenderTexture.ReleaseTemporary(ping);
            }
        }
    }
}
