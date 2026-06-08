// RealyCar：Built-in Standard 光照 + 溶解
Shader "Custom/CarModelChange/StandardDissolve"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _GlossMapScale ("Smoothness Scale", Float) = 1.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _BumpScale ("Scale", Float) = 1.0
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _Parallax ("Height Scale", Range(0.005,0.08)) = 0.02
        _ParallaxMap ("Height Map", 2D) = "gray" {}
        _OcclusionStrength ("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _EmissionColor ("Color", Color) = (0,0,0)
        _EmissionMap ("Emission", 2D) = "white" {}
        _DetailMask ("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMap ("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale ("Scale", Float) = 1.0
        _DetailNormalMap ("Normal Map", 2D) = "bump" {}

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Range(0.1, 50)) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature _NORMALMAP
        #pragma shader_feature _PARALLAXMAP
        #pragma shader_feature _METALLICGLOSSMAP
        #pragma shader_feature _EMISSION
        #pragma shader_feature _DETAIL_MULX2
        #pragma shader_feature _OCCLUSION

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _ParallaxMap;
        sampler2D _MetallicGlossMap;
        sampler2D _OcclusionMap;
        sampler2D _EmissionMap;
        sampler2D _DetailAlbedoMap;
        sampler2D _DetailNormalMap;
        sampler2D _DetailMask;
        fixed4 _Color;
        half _Glossiness;
        half _GlossMapScale;
        half _Metallic;
        float _BumpScale;
        float _Parallax;
        half _OcclusionStrength;
        fixed4 _EmissionColor;
        float _DetailNormalMapScale;

        #include "CarDissolveCommon.cginc"

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_EmissionMap;
            float2 uv_DetailAlbedoMap;
            float3 viewDir;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            CarDissolveClip(IN.worldPos);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;

            #ifdef _METALLICGLOSSMAP
                fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic = mg.r;
                o.Smoothness = mg.a * _GlossMapScale;
            #else
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
            #endif

            #ifdef _NORMALMAP
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            #endif

            #ifdef _OCCLUSION
                fixed occ = tex2D(_OcclusionMap, IN.uv_MainTex).g;
                o.Occlusion = lerp(1, occ, _OcclusionStrength);
            #endif

            #ifdef _EMISSION
                o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb * _EmissionColor.rgb;
            #endif

            #ifdef _DETAIL_MULX2
                fixed4 mask = tex2D(_DetailMask, IN.uv_MainTex);
                fixed4 detail = tex2D(_DetailAlbedoMap, IN.uv_DetailAlbedoMap);
                o.Albedo = lerp(o.Albedo, o.Albedo * detail.rgb * 2, mask.r);
            #endif

            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
