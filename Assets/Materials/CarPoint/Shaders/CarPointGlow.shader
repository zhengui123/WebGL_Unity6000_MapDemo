Shader "Custom/CarPointGlow"
{
    Properties
    {
        _MainTex ("Glow Texture (R=核心, A=透明形状)", 2D) = "white" {}
        _Color ("中心着色", Color) = (0.25, 0.88, 1, 1)
        _GlowIntensity ("中心亮度", Range(0, 5)) = 1.6
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _GlowIntensity;
            float _SelfColorWeight;
            float _FusionOpacity;

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

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag_fusion(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed alpha = tex.a * _FusionOpacity;
                fixed3 rgb = _Color.rgb * _GlowIntensity * tex.r * _SelfColorWeight;
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _GlowIntensity;
            float _AdditiveGlow;

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

            v2f vert(appdata v)
            {
                v2f o;
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
                fixed3 rgb = _Color.rgb * _GlowIntensity * core * _AdditiveGlow;
                return fixed4(rgb, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
