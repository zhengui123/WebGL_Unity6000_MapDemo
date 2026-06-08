Shader "Custom/CarModelChange/HologramDissolve"
{
    Properties
    {
        [Header(Base)]
        [HDR] _BaseColor ("基色", Color) = (0.08, 0.45, 0.65, 1)
        [HDR] _GlowColor ("发光色", Color) = (0.3, 0.95, 1.0, 1)
        _Alpha ("整体透明度", Range(0, 1)) = 0.72
        _FillStrength ("表面填充强度", Range(0, 2)) = 0.85
        _FacingMinGlow ("正面最低发光", Range(0, 1)) = 0.45

        [Header(Surface Occlusion)]
        _SurfaceFacingMin ("表面朝向阈值", Range(0, 0.5)) = 0.08
        _DepthAlphaClip ("深度预Pass Alpha裁切", Range(0, 0.5)) = 0.02

        [Header(Fresnel Rim)]
        _FresnelPower ("轮廓衰减", Range(0.5, 8)) = 2.2
        _FresnelIntensity ("轮廓强度", Range(0, 4)) = 1.6
        _RimAlphaBoost ("轮廓透明度加成", Range(0, 1)) = 0.35

        [Header(Grid)]
        _GridScale ("网格密度", Range(1, 120)) = 28
        _GridLineWidth ("网格线宽", Range(0.001, 0.2)) = 0.06
        _GridIntensity ("网格亮度", Range(0, 2)) = 1.1
        _GridWorldScale ("世界空间网格缩放", Range(0.01, 2)) = 0.35

        [Header(Scanlines)]
        _ScanSpeed ("扫描速度", Range(0, 5)) = 0.6
        _ScanDensity ("扫描密度", Range(1, 80)) = 18
        _ScanIntensity ("扫描亮度", Range(0, 1)) = 0.22

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.2)) = 0.04
        [HDR]_DissolveEdgeColor ("Dissolve Edge Color", Color) = (0.15, 0.75, 1, 1)
        _DissolveNoiseScale ("Dissolve Noise Scale", Range(0.1, 50)) = 12
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            Name "DepthPrepass"
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragDepth
            #include "UnityCG.cginc"
            #include "CarHologramDissolveCommon.cginc"
            ENDCG
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragColor
            #include "UnityCG.cginc"
            #include "CarHologramDissolveCommon.cginc"
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
