// 汽车全息边缘线：深度外壳 + 视线裁剪；屏幕/距离补偿，减轻近景线条消失
Shader "Custom/CarHologramEdgeOutline"
{
    Properties
    {
        [HDR] _EdgeColor ("边缘颜色 (HDR)", Color) = (0.25, 2.5, 4, 1)
        _EdgeHdrIntensity ("HDR 强度倍增", Range(0.25, 8)) = 1
        _EdgeAlpha ("边缘透明度", Range(0, 1)) = 0.9
        _SurfaceFacingMin ("表面朝向阈值", Range(0, 0.5)) = 0.08

        [Header(Rim)]
        _RimPower ("外轮廓衰减", Range(0.5, 8)) = 1.8
        _RimStrength ("外轮廓强度", Range(0, 4)) = 2.2

        [Header(Structural)]
        _NormalEdgeStrength ("结构线强度", Range(0, 4)) = 1.4
        _NormalEdgeScale ("结构线敏感度", Range(0, 50)) = 12
        _LineSharpness ("线条锐度", Range(0, 1)) = 0.72

        [Header(Distance)]
        _EdgeDistReference ("距离参考(米)", Range(0.5, 80)) = 8
        _EdgeNearBoost ("近距离增强", Range(0.25, 4)) = 1.35
        _EdgeFarScale ("远距离缩放", Range(0.25, 2)) = 1
        _EdgeDistanceInfluence ("距离影响强度", Range(0, 2)) = 1
        _EdgeScreenNorm ("屏幕归一化强度", Range(0, 2)) = 1
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

            half4 _EdgeColor;
            float _EdgeHdrIntensity;
            float _EdgeAlpha;
            float _SurfaceFacingMin;
            float _RimPower;
            float _RimStrength;
            float _NormalEdgeStrength;
            float _NormalEdgeScale;
            float _LineSharpness;
            float _EdgeDistReference;
            float _EdgeNearBoost;
            float _EdgeFarScale;
            float _EdgeDistanceInfluence;
            float _EdgeScreenNorm;

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

            // 按相机距离混合近景增强 / 远景缩放
            float EdgeDistanceMultiplier(float dist)
            {
                float refD = max(_EdgeDistReference, 0.01);
                float closeT = saturate(1.0 - dist / refD);
                float farT = saturate((dist - refD) / refD);
                float nearMul = lerp(1.0, _EdgeNearBoost, closeT);
                float farMul = lerp(1.0, _EdgeFarScale, farT);
                return lerp(1.0, nearMul * farMul, _EdgeDistanceInfluence);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndv = saturate(dot(n, viewDir));
                clip(ndv - _SurfaceFacingMin);

                float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;

                // 法线屏幕导数：近大远小，大平面正对相机时导数趋近 0 → 线条消失
                float3 dn = abs(ddx(n)) + abs(ddy(n));
                float rawStructural = length(dn) * _NormalEdgeScale;

                // 用世界坐标屏幕梯度归一化，减弱与像素/距离耦合
                float3 dPosX = ddx(i.worldPos);
                float3 dPosY = ddy(i.worldPos);
                float screenFootprint = max(length(dPosX), length(dPosY));
                float screenNorm = lerp(1.0, 1.0 / max(screenFootprint, 1e-4), _EdgeScreenNorm);

                float dist = length(_WorldSpaceCameraPos.xyz - i.worldPos);
                float distMul = EdgeDistanceMultiplier(dist);

                float facingMask = smoothstep(_SurfaceFacingMin, _SurfaceFacingMin + 0.2, ndv);
                float structural = rawStructural * screenNorm * distMul * _NormalEdgeStrength * facingMask;

                float edge = saturate(rim + structural);

                // 近景略放宽阈值带宽，避免高 LineSharpness 时整片线条被裁掉
                float closeT = saturate(1.0 - dist / max(_EdgeDistReference, 0.01));
                float threshold = lerp(0.02, 0.55, _LineSharpness);
                float band = lerp(0.1, 0.04 + closeT * 0.12, _EdgeDistanceInfluence);

                edge = smoothstep(threshold, threshold + band, edge);

                float a = edge * _EdgeAlpha;
                float3 rgb = _EdgeColor.rgb * _EdgeHdrIntensity * edge;
                return half4(rgb, a);
            }
            ENDCG
        }
    }

    FallBack Off
}
