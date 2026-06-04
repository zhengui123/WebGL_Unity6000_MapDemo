Shader "Custom/PlateToGaodeScanlineOverlay"
{
    Properties
    {
        _MainTex ("Overlay", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (0.1, 0.65, 1, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _ScanIntensity ("Scan Intensity", Range(0, 2)) = 1
        _RadialSoftness ("Radial Softness", Range(0.01, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Progress;
            float _ScanIntensity;
            float _RadialSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv - 0.5;
                float radial = 1.0 - saturate(length(centered) / _RadialSoftness);
                float scanLine = sin((i.uv.y - _Time.y * 0.35) * 180.0) * 0.5 + 0.5;
                float band = smoothstep(_Progress - 0.08, _Progress, i.uv.y);
                float mask = radial * band * scanLine;
                fixed4 col = _TintColor;
                col.a *= mask * _ScanIntensity * _Progress;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
