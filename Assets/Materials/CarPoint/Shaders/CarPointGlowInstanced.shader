Shader "Custom/CarPointGlowInstanced"
{
    Properties
    {
        _MainTex ("Glow Texture (R=核心, A=透明形状)", 2D) = "white" {}
        [Header(Center Brightness)]
        _CenterBrightness ("中心亮度（全局统一）", Range(0, 5)) = 1
        [Header(Fusion Blend Parameters)]
        _SelfColorWeight ("自身颜色权重", Range(0, 1)) = 0.42
        _FusionOpacity ("融合不透明度", Range(0, 1)) = 0.72
        _AdditiveGlow ("附加辉光强度", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+120"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_fusion
            #pragma multi_compile_instancing
            #pragma prefer_hlslcc gles
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _SelfColorWeight;
            float _FusionOpacity;
            float _CenterBrightness;
            float4 _FallbackColorAndGlow;

            UNITY_INSTANCING_BUFFER_START(CarPointInstancedProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColorAndGlow)
            UNITY_INSTANCING_BUFFER_END(CarPointInstancedProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float glowIntensity : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void ApplyInstanceVisual(inout float4 tint, inout float glow)
            {
                float4 inst = _FallbackColorAndGlow;
            #if defined(UNITY_INSTANCING_ENABLED)
                inst = UNITY_ACCESS_INSTANCED_PROP(CarPointInstancedProps, _InstanceColorAndGlow);
            #endif
                tint = float4(inst.rgb, 1);
                glow = inst.a;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 tint = float4(0.25, 0.88, 1, 1);
                float glow = 1.6;
                ApplyInstanceVisual(tint, glow);

                o.color = tint;
                o.glowIntensity = glow;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag_fusion(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed alpha = tex.a * _FusionOpacity;
                fixed3 rgb = i.color.rgb * i.glowIntensity * tex.r * _SelfColorWeight * _CenterBrightness;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_additive
            #pragma multi_compile_instancing
            #pragma prefer_hlslcc gles
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AdditiveGlow;
            float _CenterBrightness;
            float4 _FallbackColorAndGlow;

            UNITY_INSTANCING_BUFFER_START(CarPointInstancedProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColorAndGlow)
            UNITY_INSTANCING_BUFFER_END(CarPointInstancedProps)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float glowIntensity : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void ApplyInstanceVisual(inout float4 tint, inout float glow)
            {
                float4 inst = _FallbackColorAndGlow;
            #if defined(UNITY_INSTANCING_ENABLED)
                inst = UNITY_ACCESS_INSTANCED_PROP(CarPointInstancedProps, _InstanceColorAndGlow);
            #endif
                tint = float4(inst.rgb, 1);
                glow = inst.a;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 tint = float4(0.25, 0.88, 1, 1);
                float glow = 1.6;
                ApplyInstanceVisual(tint, glow);

                o.color = tint;
                o.glowIntensity = glow;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag_additive(v2f i) : SV_Target
            {
                if (_AdditiveGlow <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed core = tex.a * tex.r;
                fixed3 rgb = i.color.rgb * i.glowIntensity * core * _AdditiveGlow * _CenterBrightness;
                return fixed4(rgb, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
