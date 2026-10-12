// ECU 三角全边线：网格 COLOR 重心（WebGL 无 GS）
// 需挂 EcuWireframeMeshPrep 生成重心顶点色

Shader "VSOC/ECU/ConvexEdge1"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("物体颜色", Color) = (0.28, 0.38, 0.48, 1)
        [HDR] _EdgeColor ("边界线颜色", Color) = (0.55, 1.85, 2.2, 1)

        [Header(Wireframe)]
        _LineWidth ("线宽/墨量(0=无线,可<1)", Range(0, 4)) = 0.5
        _LineSoftness ("滤波半径(防断线)", Range(0.35, 1.5)) = 0.75
        [HideInInspector] _MinPixelWidth ("(弃用)", Range(0, 3)) = 0

        [Header(Fallback Crease)]
        _CreaseSensitivity ("硬折角敏感度", Range(0.1, 80)) = 22
        _CreaseStrength ("硬折角强度", Range(0, 8)) = 2.4
        _CreaseSharpness ("硬折角锐度", Range(0, 1)) = 0.72
        _RimStrength ("外轮廓强度", Range(0, 2)) = 0.35
        _RimPower ("外轮廓衰减", Range(0.5, 8)) = 2.5
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
            Name "ForwardWireframe"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _BaseColor;
            half4 _EdgeColor;
            float _LineWidth;
            float _LineSoftness;
            float _MinPixelWidth;
            float _CreaseSensitivity;
            float _CreaseStrength;
            float _CreaseSharpness;
            float _RimStrength;
            float _RimPower;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float edge;
                float s = i.bary.x + i.bary.y + i.bary.z;
                if (s > 0.5 && s < 1.5)
                {
                    float w = _LineWidth;
                    if (w <= 1e-4)
                    {
                        edge = 0.0;
                    }
                    else
                    {
                        float dist = min(min(i.bary.x, i.bary.y), i.bary.z);
                        float fw = max(fwidth(dist), 1e-6);
                        float d = dist / fw;
                        float aa = max(_LineSoftness, 0.55);
                        float peak = saturate(sqrt(w / aa));
                        float core = max(w - aa * 0.35, 0.0);
                        float cover = 1.0 - smoothstep(core, core + aa, d);
                        edge = saturate(peak * cover);
                    }
                }
                else
                {
                    float3 n = normalize(i.worldNormal);
                    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                    float ndv = saturate(dot(n, viewDir));
                    float3 dn = abs(ddx(n)) + abs(ddy(n));
                    float structural = length(dn) * _CreaseSensitivity * _CreaseStrength;
                    float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;
                    edge = saturate(structural + rim);
                    float threshold = lerp(0.05, 0.55, _CreaseSharpness);
                    float band = lerp(0.14, 0.035, _CreaseSharpness);
                    edge = smoothstep(threshold, threshold + band, edge);
                }

                half3 rgb = lerp(_BaseColor.rgb, _EdgeColor.rgb, edge);
                return half4(rgb, 1);
            }
            ENDCG
        }
    }

    FallBack "Mobile/Diffuse"
}
