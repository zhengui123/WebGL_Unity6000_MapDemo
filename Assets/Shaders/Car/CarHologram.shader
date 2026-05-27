// 汽车全息展示（Built-in）：全距离/全角度可见的 Fresnel + 网格 + 扫描线
Shader "Custom/CarHologram"
{
    Properties
    {
        [Header(Base)]
        [HDR] _BaseColor ("基色", Color) = (0.08, 0.45, 0.65, 1)
        [HDR] _GlowColor ("发光色", Color) = (0.3, 0.95, 1.0, 1)
        _Alpha ("整体透明度", Range(0, 1)) = 0.72
        _FillStrength ("表面填充强度", Range(0, 2)) = 0.85
        _FacingMinGlow ("正面最低发光", Range(0, 1)) = 0.45

        [Header(Fresnel Rim)]
        _FresnelPower ("轮廓衰减", Range(0.5, 8)) = 2.2
        _FresnelIntensity ("轮廓强度", Range(0, 4)) = 1.6
        _RimAlphaBoost ("轮廓透明度加成", Range(0, 1)) = 0.35

        [Header(Grid)]
        _GridScale ("网格密度", Range(1, 120)) = 28
        _GridLineWidth ("网格线宽", Range(0.001, 0.2)) = 0.06
        _GridIntensity ("网格亮度", Range(0, 2)) = 1.1
        _GridWorldScale ("世界空间网格缩放", Range(0.01, 2)) = 0.35

        [Header(Scanlines)]
        _ScanSpeed ("扫描速度", Range(0, 5)) = 0.6
        _ScanDensity ("扫描密度", Range(1, 80)) = 18
        _ScanIntensity ("扫描亮度", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _GlowColor;
            float _Alpha;
            float _FillStrength;
            float _FacingMinGlow;
            float _FresnelPower;
            float _FresnelIntensity;
            float _RimAlphaBoost;
            float _GridScale;
            float _GridLineWidth;
            float _GridIntensity;
            float _GridWorldScale;
            float _ScanSpeed;
            float _ScanDensity;
            float _ScanIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            float GridLines(float2 uv, float scale, float lineWidth)
            {
                float2 g = uv * scale;
                float2 fw = abs(frac(g - 0.5) - 0.5) / max(fwidth(g), 1e-5);
                float2 gridMask = 1.0 - saturate(fw / max(lineWidth, 0.001));
                return saturate(max(gridMask.x, gridMask.y));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndv = saturate(dot(n, viewDir));

                // Fresnel 仅作边缘增强，正面保留最低亮度，避免“只有近看/侧看才明显”
                float rim = pow(1.0 - ndv, _FresnelPower);
                float rimBoost = lerp(_FacingMinGlow, 1.0, rim);

                // 世界空间网格：与相机距离无关，全场景一致
                float2 gridUv = i.worldPos.xz * _GridWorldScale + i.uv * _GridScale * 0.15;
                float grid = GridLines(gridUv, _GridScale, _GridLineWidth) * _GridIntensity;

                float scan = (sin((i.worldPos.y + _Time.y * _ScanSpeed) * _ScanDensity) * 0.5 + 0.5) * _ScanIntensity;

                float3 fill = _BaseColor.rgb * _FillStrength * rimBoost;
                float3 glowBase = _GlowColor.rgb * (_FacingMinGlow * 0.6 + 0.25);
                float3 rimCol = _GlowColor.rgb * rim * _FresnelIntensity;
                float3 gridCol = _GlowColor.rgb * grid;
                float3 scanCol = _GlowColor.rgb * scan * rimBoost;

                float3 rgb = fill + glowBase + rimCol + gridCol + scanCol;

                // 透明度：基底恒定 + 边缘/网格略增，不随距离衰减
                float a = saturate(_Alpha + rim * _RimAlphaBoost + grid * 0.2);
                a = max(a, _Alpha * 0.85);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
