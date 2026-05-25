// 顶面轮廓距离：顶点色 R（0=外轮廓，1=中心），由 SdMapPlateTopEdgeBaker 烘焙
// 顶面渐变：沿外轮廓 → 中心，由亮到暗（edgeOut = 1 - inward）
Shader "Custom/SdMapPlateHud"
{
    Properties
    {
        [Header(Part1 Plate Base)]
        [HDR] _InteriorColor ("板块基底色", Color) = (0.02, 0.07, 0.22, 1)
        _InteriorAlpha ("基底透明度", Range(0, 1)) = 0.65
        _TopInteriorAlphaMin ("顶面基底最低透明度", Range(0, 1)) = 0.55
        _SideInteriorMult ("侧面基底系数", Range(0.3, 1.2)) = 0.78

        [Header(Part2 Top Edge Highlight)]
        [HDR] _EdgeHighlightColor ("顶面轮廓高光色", Color) = (0.35, 0.95, 1.0, 1)
        _EdgeHighlightAlpha ("轮廓高光透明度", Range(0, 1)) = 0.92
        _TopEdgeInwardWidth ("轮廓高光向内宽度", Range(0.01, 0.35)) = 0.07
        _EdgeHighlightPower ("轮廓高光锐利", Range(0.35, 12)) = 4.5

        [Header(Part3 Top Gradient Edge To Center)]
        [HDR] _GradientBrightColor ("顶面靠轮廓亮色", Color) = (0.12, 0.45, 0.85, 1)
        [HDR] _GradientDarkColor ("顶面中心暗色", Color) = (0.02, 0.07, 0.18, 1)
        _GradientAlpha ("渐变层透明度", Range(0, 1)) = 0.78
        _GradientPower ("渐变衰减曲线", Range(0.25, 8)) = 1.65

        [Header(Side Edge)]
        [Toggle] _SideUseEdgeColor ("侧面使用轮廓高光", Float) = 1
        _SideEdgeStrength ("侧面高光强度", Range(0, 1)) = 0.85

        [Header(Face Split)]
        _TopFaceNormalY ("顶面法线Y阈值(局部, <=此值为顶)", Range(-1, 0)) = -0.85
        _WorldUp ("顶面向上 XYZ(备用)", Vector) = (0, -1, 0, 0)
        _TopFaceCos ("顶面判定 cos(备用)", Range(0.7, 0.9999)) = 0.88
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 objectNormal : TEXCOORD1;
                float4 vc : COLOR;
                UNITY_FOG_COORDS(2)
            };

            float4 _InteriorColor;
            float _InteriorAlpha;
            float _TopInteriorAlphaMin;
            float _SideInteriorMult;
            float4 _EdgeHighlightColor;
            float _EdgeHighlightAlpha;
            float _TopEdgeInwardWidth;
            float _EdgeHighlightPower;
            float4 _GradientBrightColor;
            float4 _GradientDarkColor;
            float _GradientAlpha;
            float _GradientPower;
            float _TopFaceNormalY;
            float4 _WorldUp;
            float _TopFaceCos;
            float _SideUseEdgeColor;
            float _SideEdgeStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objectNormal = normalize(v.normal);
                o.vc = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float TopFaceMask(float3 nObj, float3 nWorld)
            {
                float topObj = step(nObj.y, _TopFaceNormalY);
                float3 up = normalize(_WorldUp.xyz);
                float topWorld = step(_TopFaceCos, dot(normalize(nWorld), up));
                return saturate(max(topObj, topWorld));
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float topFace = TopFaceMask(i.objectNormal, N);
                float ndotUp = dot(N, normalize(_WorldUp.xyz));
                float sideFace = (1.0 - topFace) * step(-_TopFaceCos, ndotUp);

                // inward: 0=顶面外轮廓，1=顶面几何中心（与烘焙器一致）
                float inward01 = topFace * saturate(i.vc.r);
                float edgeOut = 1.0 - inward01;

                float edgeNorm = max(_TopEdgeInwardWidth, 1e-4);
                float wEdgeTop = topFace * pow(saturate(1.0 - inward01 / edgeNorm), _EdgeHighlightPower);

                // 外轮廓亮 → 中心暗
                float wGradTop = topFace * pow(saturate(edgeOut), _GradientPower);

                float sideEdge = sideFace * saturate(_SideUseEdgeColor) * _SideEdgeStrength;
                float wEdge = saturate(max(wEdgeTop, sideEdge));
                wGradTop *= (1.0 - sideEdge);

                float3 interiorRgb = _InteriorColor.rgb;
                interiorRgb *= lerp(_SideInteriorMult, 1.0, topFace);

                half baseA = (half)_InteriorAlpha;
                baseA = max(baseA, (half)(topFace * _TopInteriorAlphaMin));
                half3 baseRgb = (half3)interiorRgb;

                half3 gradRgb = (half3)lerp(_GradientDarkColor.rgb, _GradientBrightColor.rgb, wGradTop);
                half gradA = (half)saturate(_GradientAlpha * wGradTop);

                half3 col = gradRgb * gradA + baseRgb * baseA * (1.0 - gradA);
                half a = gradA + baseA * (1.0 - gradA);

                half edgeA = (half)saturate(_EdgeHighlightAlpha * wEdge);
                half3 edgeRgb = (half3)_EdgeHighlightColor.rgb;
                col = edgeRgb * edgeA + col * (1.0 - edgeA);
                a = edgeA + a * (1.0 - edgeA);

                half4 outCol = half4(col, saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
