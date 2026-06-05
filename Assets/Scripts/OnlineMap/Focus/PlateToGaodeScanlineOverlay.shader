Shader "Custom/PlateToGaodeScanlineOverlay"
{
    Properties
    {
        _MainTex ("Overlay", 2D) = "white" {}
        [HDR] _TintColor ("Tech Primary Tint", Color) = (0.05, 0.75, 1, 1)
        [HDR] _EdgeColor ("Edge Glow", Color) = (0.85, 0.97, 1, 1)
        [HDR] _GridColor ("Grid Color", Color) = (0.15, 0.55, 1, 0.6)
        [HDR] _LegacyTintColor ("Legacy Tint", Color) = (0.1, 0.65, 1, 0.72)
        _Progress ("Progress", Range(0, 1)) = 0
        _ScanIntensity ("Tech Intensity", Range(0, 2)) = 0.85
        _LegacyIntensity ("Legacy Intensity", Range(0, 2)) = 0.55
        _LegacyWeight ("Legacy Weight", Range(0, 1)) = 0.65
        _TechWeight ("Tech Weight", Range(0, 1)) = 0.75
        _RadialSoftness ("Tech Radial Softness", Range(0.05, 1.5)) = 0.58
        _LegacyRadialSoftness ("Legacy Radial Softness", Range(0.05, 1.5)) = 1
        _LegacyBandWidth ("Legacy Band Width", Range(0.02, 0.2)) = 0.08
        _LegacyScanSpeed ("Legacy Scan Speed", Range(0, 2)) = 0.35
        _GridDensity ("Grid Density", Range(8, 96)) = 36
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.04)) = 0.012
        _EdgeGlowWidth ("Edge Glow Width", Range(0.005, 0.12)) = 0.028
        _TrailWidth ("Trail Width", Range(0.04, 0.4)) = 0.14
        _NoiseAmount ("Digital Noise", Range(0, 1)) = 0.32
        _ScanLineSpeed ("Tech Scan Line Speed", Range(0, 2)) = 0.55
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.45
        _StreakStrength ("Data Streak", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            fixed4 _EdgeColor;
            fixed4 _GridColor;
            fixed4 _LegacyTintColor;
            float _Progress;
            float _ScanIntensity;
            float _LegacyIntensity;
            float _LegacyWeight;
            float _TechWeight;
            float _RadialSoftness;
            float _LegacyRadialSoftness;
            float _LegacyBandWidth;
            float _LegacyScanSpeed;
            float _GridDensity;
            float _GridLineWidth;
            float _EdgeGlowWidth;
            float _TrailWidth;
            float _NoiseAmount;
            float _ScanLineSpeed;
            float _PulseStrength;
            float _StreakStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float TechGrid(float2 uv, float density, float lineWidth)
            {
                float2 g = uv * density;
                float2 f = abs(frac(g) - 0.5);
                float w = lineWidth * density;
                float minor = max(
                    1.0 - smoothstep(0.0, w, f.x),
                    1.0 - smoothstep(0.0, w, f.y)
                );

                float2 majorF = abs(frac(g * 0.25) - 0.5);
                float majorW = w * 2.2;
                float major = max(
                    1.0 - smoothstep(0.0, majorW, majorF.x),
                    1.0 - smoothstep(0.0, majorW, majorF.y)
                );

                return saturate(minor * 0.55 + major * 0.95);
            }

            // 旧版：径向 × 水平扫描带 × 横纹，整体随 Progress 渐强
            fixed4 SampleLegacyLayer(float2 uv, float2 centered)
            {
                float radial = 1.0 - saturate(length(centered) / _LegacyRadialSoftness);
                float scanLine = sin((uv.y - _Time.y * _LegacyScanSpeed) * 180.0) * 0.5 + 0.5;
                float band = smoothstep(_Progress - _LegacyBandWidth, _Progress, uv.y);
                float mask = radial * band * scanLine;

                fixed4 col = _LegacyTintColor;
                col.rgb *= mask;
                col.a = mask * _LegacyIntensity * _Progress;
                return col;
            }

            fixed4 SampleTechLayer(float2 uv, float2 centered)
            {
                float radial = 1.0 - saturate(length(centered) / _RadialSoftness);
                radial = pow(saturate(radial), 1.15);

                float edge = _Progress;
                float pulse = sin(_Time.y * 7.0 + edge * 24.0) * 0.5 + 0.5;

                float passed = 1.0 - smoothstep(edge - 0.02, edge + 0.01, uv.y);
                float trail = smoothstep(edge - _TrailWidth, edge - _TrailWidth * 0.12, uv.y);
                trail *= 1.0 - smoothstep(edge + 0.01, edge + 0.05, uv.y);
                float edgeGlow = exp(-abs(uv.y - edge) / max(_EdgeGlowWidth, 0.001));
                edgeGlow *= 1.0 + pulse * _PulseStrength;

                float grid = TechGrid(uv, _GridDensity, _GridLineWidth);
                float gridMask = saturate(passed * 0.35 + trail * 0.95 + edgeGlow * 1.2);

                float scanLine = sin((uv.y + _Time.y * _ScanLineSpeed) * 140.0) * 0.5 + 0.5;
                scanLine = pow(scanLine, 2.6);

                float streaks = smoothstep(0.94, 1.0, frac((uv.x * 1.3 + uv.y * 0.85 + _Time.y * 0.22) * 36.0));
                streaks *= trail * _StreakStrength;

                float2 noiseUV = floor(uv * float2(120.0, 80.0) + _Time.y * float2(18.0, 11.0));
                float noise = Hash21(noiseUV);
                float noiseBand = edgeGlow * noise * _NoiseAmount;

                float3 rgb =
                    _GridColor.rgb * grid * gridMask * radial * 0.85 +
                    _TintColor.rgb * scanLine * trail * radial * 0.65 +
                    _EdgeColor.rgb * edgeGlow * (1.15 + pulse * _PulseStrength * 0.6) +
                    _EdgeColor.rgb * noiseBand * 1.4 +
                    _TintColor.rgb * streaks * 2.2;

                float active = saturate(passed * 0.45 + trail * 0.75 + edgeGlow * 1.35 + noiseBand);
                float alpha = saturate(active * _ScanIntensity * radial);
                alpha *= smoothstep(0.0, 0.06, _Progress);
                alpha = saturate(alpha + edgeGlow * 0.25 * _ScanIntensity);

                fixed4 col;
                col.rgb = rgb;
                col.a = alpha * _TintColor.a;
                return col;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;

                fixed4 legacy = SampleLegacyLayer(uv, centered);
                fixed4 tech = SampleTechLayer(uv, centered);

                // 旧版打底 + 新版叠加（Premultiplied 合成）
                float legacyW = _LegacyWeight;
                float techW = _TechWeight;
                fixed3 legacyPremul = legacy.rgb * legacy.a * legacyW;
                fixed3 techPremul = tech.rgb * tech.a * techW;
                fixed3 rgb = legacyPremul + techPremul * (1.0 - legacy.a * legacyW);
                float alpha = saturate(legacy.a * legacyW + tech.a * techW * (1.0 - legacy.a * legacyW * 0.5));

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
