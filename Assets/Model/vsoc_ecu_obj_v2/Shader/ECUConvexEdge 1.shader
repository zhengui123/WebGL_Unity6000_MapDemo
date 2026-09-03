// ECU 零件双色线框：不透明填充 + 每个三角面边线（重心坐标）
// Built-in RP / Opaque / 需 Geometry Shader

Shader "VSOC/ECU/ConvexEdge"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("物体颜色", Color) = (0.28, 0.38, 0.48, 1)
        [HDR] _EdgeColor ("边界线颜色", Color) = (0.55, 1.85, 2.2, 1)

        [Header(Wireframe)]
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
            Name "ForwardWireframe"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _BaseColor;
            half4 _EdgeColor;
            float _LineWidth;
            float _LineSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float3 bary : TEXCOORD0;
            };

            v2g vert(appdata v)
            {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream)
            {
                g2f o0;
                o0.pos = input[0].pos;
                o0.bary = float3(1, 0, 0);
                stream.Append(o0);

                g2f o1;
                o1.pos = input[1].pos;
                o1.bary = float3(0, 1, 0);
                stream.Append(o1);

                g2f o2;
                o2.pos = input[2].pos;
                o2.bary = float3(0, 0, 1);
                stream.Append(o2);
            }

            fixed4 frag(g2f i) : SV_Target
            {
                // 到最近三角边的重心距离；fwidth 做屏幕近似线宽
                float3 d = fwidth(i.bary);
                float3 a3 = smoothstep(d * (_LineWidth - _LineSoftness), d * (_LineWidth + _LineSoftness), i.bary);
                float edge = 1.0 - min(min(a3.x, a3.y), a3.z);
                edge = saturate(edge);

                half3 rgb = lerp(_BaseColor.rgb, _EdgeColor.rgb, edge);
                return half4(rgb, 1);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
