// ECU 零件双色线框：不透明填充 + 边线
// 模式：硬折角（默认，去对角）/ 三角面全边线（重心坐标）
// Built-in RP / Opaque / 需 Geometry Shader

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
        _LineWidth ("线宽(像素近似)", Range(0, 8)) = 1.5
        _LineSoftness ("线边缘柔和", Range(0.01, 2)) = 0.5
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
            #pragma target 4.0
            #pragma require derivatives
            #pragma multi_compile_local _EDGEMODE_CREASE _EDGEMODE_TRIANGLE
            #pragma vertex vert
            #pragma geometry geom
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                noperspective float3 bary : TEXCOORD2;
            };

            v2g vert(appdata v)
            {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream)
            {
                g2f o0;
                o0.pos = input[0].pos;
                o0.worldPos = input[0].worldPos;
                o0.worldNormal = input[0].worldNormal;
                o0.bary = float3(1, 0, 0);
                stream.Append(o0);

                g2f o1;
                o1.pos = input[1].pos;
                o1.worldPos = input[1].worldPos;
                o1.worldNormal = input[1].worldNormal;
                o1.bary = float3(0, 1, 0);
                stream.Append(o1);

                g2f o2;
                o2.pos = input[2].pos;
                o2.worldPos = input[2].worldPos;
                o2.worldNormal = input[2].worldNormal;
                o2.bary = float3(0, 0, 1);
                stream.Append(o2);
            }

            float ComputeTriangleEdge(float3 bary)
            {
                float width = max(_LineWidth, 0.5);
                float soft = max(_LineSoftness, 0.01);
                float3 d = fwidth(bary);
                float3 a3 = smoothstep(d * (width - soft), d * (width + soft), bary);
                return saturate(1.0 - min(min(a3.x, a3.y), a3.z));
            }

            // 共面四边形对角线两侧法线几乎不变 → 不画；真实折角 + 外轮廓才画
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

            fixed4 frag(g2f i) : SV_Target
            {
                float edge = 0;

            #if defined(_EDGEMODE_TRIANGLE)
                edge = ComputeTriangleEdge(i.bary);
            #else
                edge = ComputeCreaseEdge(i.worldNormal, i.worldPos);
            #endif

                half3 rgb = lerp(_BaseColor.rgb, _EdgeColor.rgb, edge);
                return half4(rgb, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
