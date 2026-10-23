Shader "Hidden/Car07/Hologram Post BuiltIn"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _CameraDepthNormalsTexture;
        sampler2D _BloomTex;
        float4 _LineColor;
        float _LineWidth, _LineStrength;
        float _NormalStart, _NormalEnd, _DepthStart, _DepthEnd;
        float _BloomThreshold, _BloomStrength;
        float4 _BlurDirection;

        void ReadNormalDepth(float2 uv, out float depth, out float3 normal)
        {
            DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv), depth, normal);
            normal = normalize(normal);
        }
        float Boundary(float depth, float3 normal, float2 uv)
        {
            float nextDepth; float3 nextNormal;
            ReadNormalDepth(uv, nextDepth, nextNormal);
            if (nextDepth >= 0.9999) return 1.0;
            float n = smoothstep(_NormalStart, max(_NormalEnd, _NormalStart + 0.00001),
                1.0 - clamp(dot(normal, nextNormal), -1.0, 1.0));
            // DecodeDepthNormal is linear 0..1 depth. Convert the denominator's
            // 0.1 eye-space floor into that same range to match the web shader.
            float relativeDepth = abs(depth - nextDepth) / max(depth, 0.1 / _ProjectionParams.z);
            float d = smoothstep(_DepthStart, max(_DepthEnd, _DepthStart + 0.00001), relativeDepth);
            return max(n, d);
        }
        float4 fragOutline(v2f_img input) : SV_Target
        {
            float4 source = tex2D(_MainTex, input.uv);
            float2 uv = input.uv;
            #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uv.y = 1.0 - uv.y;
            #endif
            float depth; float3 normal;
            ReadNormalDepth(uv, depth, normal);
            float edge = 0.0;
            if (depth < 0.9999)
            {
                float2 px = abs(_MainTex_TexelSize.xy) * _LineWidth;
                edge = max(max(Boundary(depth, normal, uv + float2(px.x, 0)),
                               Boundary(depth, normal, uv - float2(px.x, 0))),
                           max(Boundary(depth, normal, uv + float2(0, px.y)),
                               Boundary(depth, normal, uv - float2(0, px.y))));
            }
            return float4(source.rgb + _LineColor.rgb * edge * _LineStrength, source.a);
        }
        float3 BrightSample(float2 uv)
        {
            float3 color = tex2D(_MainTex, uv).rgb;
            float brightness = dot(color, float3(0.299, 0.587, 0.114));
            return color * smoothstep(_BloomThreshold, _BloomThreshold + 0.01, brightness);
        }
        float4 fragPrefilter(v2f_img input) : SV_Target
        {
            float2 px = abs(_MainTex_TexelSize.xy) * 0.5;
            // Threshold before downsampling so thin bright contours survive.
            float3 color = (BrightSample(input.uv + px)
                         + BrightSample(input.uv - px)
                         + BrightSample(input.uv + float2(px.x, -px.y))
                         + BrightSample(input.uv + float2(-px.x, px.y))) * 0.25;
            return float4(color, 1);
        }
        float4 fragBlur(v2f_img input) : SV_Target
        {
            float2 offset = _BlurDirection.xy * abs(_MainTex_TexelSize.xy);
            float3 color = tex2D(_MainTex, input.uv).rgb * 0.227027027;
            color += tex2D(_MainTex, input.uv + offset * 1.384615385).rgb * 0.316216216;
            color += tex2D(_MainTex, input.uv - offset * 1.384615385).rgb * 0.316216216;
            color += tex2D(_MainTex, input.uv + offset * 3.230769231).rgb * 0.070270270;
            color += tex2D(_MainTex, input.uv - offset * 3.230769231).rgb * 0.070270270;
            return float4(color, 1);
        }
        float4 fragComposite(v2f_img input) : SV_Target
        {
            float4 color = tex2D(_MainTex, input.uv);
            return float4(color.rgb + tex2D(_BloomTex, input.uv).rgb * _BloomStrength, color.a);
        }
        ENDCG
        Pass
        {
            Name "OUTLINE"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragOutline
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            Name "BLOOM_PREFILTER"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragPrefilter
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            Name "BLOOM_BLUR"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragBlur
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            Name "COMPOSITE"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
