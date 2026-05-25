// 世界空间扫描波片（水平放置，非全屏）
Shader "Custom/EarthPlateTransitionScanWorld"
{
    Properties
    {
        _ScanLine ("扫描线高度 UV", Range(0, 1)) = 0
        _FillAmount ("已扫描填充", Range(0, 1)) = 0
        _GridStrength ("网格", Range(0, 1)) = 0.5
        _Color ("底色", Color) = (0.04, 0.1, 0.25, 0.35)
        _AccentColor ("扫线色", Color) = (0.25, 0.95, 1, 1)
        _Intensity ("强度", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _ScanLine;
            float _FillAmount;
            float _GridStrength;
            fixed4 _Color;
            fixed4 _AccentColor;
            float _Intensity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                float grid = saturate(step(0.93, frac(i.uv.x * 48.0)) + step(0.93, frac(i.uv.y * 48.0))) * _GridStrength;
                float band = 1.0 - smoothstep(0.0, 0.06, abs(i.uv.y - _ScanLine));
                float filled = step(i.uv.y, _ScanLine) * _FillAmount;
                float a = saturate((band * 0.9 + filled * 0.25 + grid * band) * _Intensity);
                fixed3 rgb = lerp(_Color.rgb, _AccentColor.rgb, band + grid * 0.4);
                return fixed4(rgb, a * _AccentColor.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
