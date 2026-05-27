// 汽车全息边缘线（仅线条，无填充）— 叠加在车身材质之上
Shader "Custom/CarHologramEdgeOutline"
{
    Properties
    {
        _EdgeColor ("边缘颜色", Color) = (0.25, 0.95, 1, 1)
        _EdgeAlpha ("边缘透明度", Range(0, 1)) = 0.9
        _RimPower ("外轮廓衰减", Range(0.5, 8)) = 1.8
        _RimStrength ("外轮廓强度", Range(0, 4)) = 2.2
        _NormalEdgeStrength ("结构线强度", Range(0, 4)) = 1.4
        _NormalEdgeScale ("结构线敏感度", Range(0, 50)) = 12
        _LineSharpness ("线条锐度", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+15"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 150
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma require derivatives
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            float _EdgeAlpha;
            float _RimPower;
            float _RimStrength;
            float _NormalEdgeStrength;
            float _NormalEdgeScale;
            float _LineSharpness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndv = saturate(dot(n, viewDir));

                float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;

                float3 dn = abs(ddx(n)) + abs(ddy(n));
                float structural = saturate(length(dn) * _NormalEdgeScale) * _NormalEdgeStrength;

                float edge = saturate(rim + structural);
                float threshold = lerp(0.02, 0.55, _LineSharpness);
                edge = smoothstep(threshold, threshold + 0.1, edge);

                float a = edge * _EdgeAlpha;
                float3 rgb = _EdgeColor.rgb * edge;
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }

    FallBack Off
}
