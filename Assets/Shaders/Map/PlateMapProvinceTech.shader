Shader "Custom/PlateMapProvinceTech"
{
    Properties
    {
        [HDR] _BaseColor ("底色", Color) = (0.02, 0.06, 0.18, 0.75)
        [HDR] _EmissionColor ("发光色", Color) = (0.15, 0.75, 1.0, 1)
        _EmissionIntensity ("发光强度", Range(0, 8)) = 2.2
        [HDR] _RimColor ("边缘高光", Color) = (0.3, 0.95, 1.0, 1)
        _RimPower ("边缘菲涅尔幂", Range(0.5, 8)) = 2.2
        _RimIntensity ("边缘强度", Range(0, 6)) = 3.5
        [HDR] _GridColor ("网格发光", Color) = (0.1, 0.55, 1.0, 1)
        _GridScale ("网格密度", Range(1, 80)) = 22
        _GridLineWidth ("网格线宽", Range(0.01, 0.5)) = 0.08
        _GridIntensity ("网格强度", Range(0, 2)) = 0.65
        _CircuitScale ("电路纹密度", Range(1, 120)) = 48
        _CircuitIntensity ("电路纹强度", Range(0, 1)) = 0.22
        _NoiseScale ("噪点密度", Range(1, 200)) = 85
        _NoiseIntensity ("噪点强度", Range(0, 0.5)) = 0.12
        _PulseSpeed ("呼吸速度", Range(0, 3)) = 0.6
        _PulseAmount ("呼吸幅度", Range(0, 0.5)) = 0.08
        [Header(Overall Visibility)]
        // 0=完全隐藏，1=完全显示；材质 Inspector 进度条或脚本 SetOverallAlpha 均可驱动
        _Alpha ("整体透明度", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _BaseColor;
        fixed4 _EmissionColor;
        half _EmissionIntensity;
        fixed4 _RimColor;
        half _RimPower;
        half _RimIntensity;
        fixed4 _GridColor;
        half _GridScale;
        half _GridLineWidth;
        half _GridIntensity;
        half _CircuitScale;
        half _CircuitIntensity;
        half _NoiseScale;
        half _NoiseIntensity;
        half _PulseSpeed;
        half _PulseAmount;
        half _Alpha;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
            o.worldPos = worldPos.xyz;
            o.uv_MainTex = v.texcoord;
        }

        float Hash21(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float GridLines(float2 uv, float scale, float width)
        {
            float2 gv = uv * scale;
            float2 grid = abs(frac(gv - 0.5) - 0.5);
            float2 deriv = fwidth(gv);
            float2 gridMask = smoothstep(width * deriv, deriv * 0.5, grid);
            return 1.0 - saturate(min(gridMask.x, gridMask.y));
        }

        float CircuitPattern(float2 uv, float scale)
        {
            float2 p = uv * scale;
            float h = sin(p.x) * sin(p.y) + sin(p.x * 0.37 + p.y * 0.61);
            float ring = abs(frac(length(frac(p) - 0.5) * 4.0) - 0.5);
            return saturate(h * 0.25 + (1.0 - ring) * 0.35);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = WorldNormalVector(IN, o.Normal);
            float3 v = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float fresnel = pow(1.0 - saturate(dot(n, v)), _RimPower);

            float2 mapUv = IN.uv_MainTex;
            if (length(mapUv) < 0.001)
            {
                mapUv = IN.worldPos.xz;
            }

            float grid = GridLines(mapUv, _GridScale, _GridLineWidth);
            float fineGrid = GridLines(mapUv, _GridScale * 4.0, _GridLineWidth * 0.6);
            float circuit = CircuitPattern(mapUv, _CircuitScale);
            float noise = ValueNoise(mapUv * _NoiseScale + _Time.y * 0.15);

            float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
            float pattern = saturate(grid * 1.0 + fineGrid * 0.35 + circuit * _CircuitIntensity);
            pattern += (noise - 0.5) * _NoiseIntensity;

            float3 baseCol = _BaseColor.rgb;
            float3 emission = _EmissionColor.rgb * _EmissionIntensity * (0.25 + pattern * _GridIntensity);
            emission += _GridColor.rgb * pattern * 0.6;
            emission += _RimColor.rgb * fresnel * _RimIntensity;
            emission *= pulse;

            float localAlpha = saturate(0.35 + fresnel * 0.65 + pattern * 0.25);
            half masterAlpha = saturate(_Alpha);

            o.Albedo = baseCol;
            o.Metallic = 0.0;
            o.Smoothness = 0.55;
            o.Emission = emission * masterAlpha;
            o.Alpha = localAlpha * masterAlpha;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
