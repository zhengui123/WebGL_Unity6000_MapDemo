// ECU 零件双色线框：不透明填充 + 边线
// Crease：法线导数硬折角（无需 GS）
// Triangle：网格 COLOR 重心坐标全边线（WebGL 可用；需 EcuWireframeMeshPrep）
// Built-in RP / Opaque / 全平台无 Geometry Shader

Shader "VSOC/ECU/ConvexEdge"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("物体颜色", Color) = (0.28, 0.38, 0.48, 1)
        [HDR] _EdgeColor ("边界线颜色", Color) = (0.55, 1.85, 2.2, 1)

        [Header(Mode)]
        [KeywordEnum(Crease, Triangle)] _EdgeMode ("边线模式", Float) = 0

        [Header(Hard Crease)]
        _CreaseSensitivity ("硬折角敏感度", Range(0.1, 80)) = 22
        _CreaseStrength ("硬折角强度", Range(0, 8)) = 2.4
        _CreaseSharpness ("硬折角锐度", Range(0, 1)) = 0.72
        _RimStrength ("外轮廓强度", Range(0, 2)) = 0.35
        _RimPower ("外轮廓衰减", Range(0.5, 8)) = 2.5

        [Header(Triangle Wireframe)]
        _LineWidth ("线宽/墨量(0=无线,可<1)", Range(0, 4)) = 0.5
        _LineSoftness ("滤波半径(防断线)", Range(0.35, 1.5)) = 0.75
        [HideInInspector] _MinPixelWidth ("(弃用)", Range(0, 3)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        LOD 200
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardEdge"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma multi_compile_local _EDGEMODE_CREASE _EDGEMODE_TRIANGLE
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _BaseColor;
            half4 _EdgeColor;
            float _CreaseSensitivity;
            float _CreaseStrength;
            float _CreaseSharpness;
            float _RimStrength;
            float _RimPower;
            float _LineWidth;
            float _LineSoftness;
            float _MinPixelWidth; // 保留兼容旧材质序列化，Coverage AA 不再使用

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 bary : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.bary = v.color.rgb;
                return o;
            }

            float ComputeTriangleEdge(float3 bary)
            {
                // Coverage AA：细线不加粗实心带宽，用固定滤波核保证连续，线宽只控墨量/观感粗细
                float w = _LineWidth;
                if (w <= 1e-4)
                {
                    return 0.0;
                }

                float dist = min(min(bary.x, bary.y), bary.z);
                float fw = max(fwidth(dist), 1e-6);
                float d = dist / fw; // 距边像素距离

                float aa = max(_LineSoftness, 0.55); // 滤波半径，保证亚像素不断线
                // 细线：peak < 1 → 更淡/更细；粗线：peak=1 + core 实心带
                float peak = saturate(sqrt(w / aa));
                float core = max(w - aa * 0.35, 0.0);
                float cover = 1.0 - smoothstep(core, core + aa, d);
                return saturate(peak * cover);
            }

            float ComputeCreaseEdge(float3 worldNormal, float3 worldPos)
            {
                float3 n = normalize(worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                float ndv = saturate(dot(n, viewDir));

                float3 dn = abs(ddx(n)) + abs(ddy(n));
                float structural = length(dn) * _CreaseSensitivity * _CreaseStrength;
                float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;
                float edge = saturate(structural + rim);

                float threshold = lerp(0.05, 0.55, _CreaseSharpness);
                float band = lerp(0.14, 0.035, _CreaseSharpness);
                return smoothstep(threshold, threshold + band, edge);
            }

            // 未准备重心时 COLOR 常为白 (1,1,1)，sum≈3；准备后插值 sum≈1
            bool HasValidBarycentric(float3 bary)
            {
                float s = bary.x + bary.y + bary.z;
                return s > 0.5 && s < 1.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float edge = 0;

            #if defined(_EDGEMODE_TRIANGLE)
                if (HasValidBarycentric(i.bary))
                {
                    edge = ComputeTriangleEdge(i.bary);
                }
                else
                {
                    // 未挂 Prep 时回退折角，避免全黑/全亮
                    edge = ComputeCreaseEdge(i.worldNormal, i.worldPos);
                }
            #else
                edge = ComputeCreaseEdge(i.worldNormal, i.worldPos);
            #endif

                half3 rgb = lerp(_BaseColor.rgb, _EdgeColor.rgb, edge);
                return half4(rgb, 1);
            }
            ENDCG
        }
    }

    FallBack "Mobile/Diffuse"
}
