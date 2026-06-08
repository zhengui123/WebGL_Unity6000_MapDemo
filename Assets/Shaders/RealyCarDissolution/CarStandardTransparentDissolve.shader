// RealyCar 透明件：Standard 透明 + 溶解
Shader "Custom/CarModelChange/StandardTransparentDissolve"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _BumpMap ("Normal Map", 2D) = "bump" {}

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveNoiseScale ("Dissolve Noise Scale", Range(0.1, 50)) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:premul keepalpha
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;

        #include "CarDissolveCommon.cginc"

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            CarDissolveClip(IN.worldPos);

            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
