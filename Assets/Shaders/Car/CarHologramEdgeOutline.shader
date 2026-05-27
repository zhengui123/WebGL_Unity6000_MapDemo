// 汽车全息边缘线：深度外壳 + 视线裁剪，抑制内饰/内侧结构线穿透
Shader "Custom/CarHologramEdgeOutline"
{
    Properties
    {
        _EdgeColor ("边缘颜色", Color) = (0.25, 0.95, 1, 1)
        _EdgeAlpha ("边缘透明度", Range(0, 1)) = 0.9
        _SurfaceFacingMin ("表面朝向阈值", Range(0, 0.5)) = 0.08
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

        // 整模外表深度（与车身全息一致），避免内侧线条参与深度竞争
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

            float _SurfaceFacingMin;

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

            fixed4 fragDepth(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndv = saturate(dot(n, viewDir));
                clip(ndv - _SurfaceFacingMin);
                return 0;
            }
            ENDCG
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma require derivatives
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _EdgeColor;
            float _EdgeAlpha;
            float _SurfaceFacingMin;
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
                clip(ndv - _SurfaceFacingMin);

                float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;

                float3 dn = abs(ddx(n)) + abs(ddy(n));
                float structural = saturate(length(dn) * _NormalEdgeScale) * _NormalEdgeStrength;

                // 背向相机的结构线进一步衰减，减少内腔折线
                float facingMask = smoothstep(_SurfaceFacingMin, _SurfaceFacingMin + 0.2, ndv);
                float edge = saturate(rim + structural * facingMask);

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
