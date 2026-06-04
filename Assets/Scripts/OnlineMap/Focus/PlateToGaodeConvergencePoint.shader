Shader "Custom/PlateToGaodeConvergencePoint"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.85, 0.2, 1)
        _Glow ("Glow", Range(0, 4)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Glow;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 n : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fresnel = pow(1.0 - saturate(dot(normalize(i.n), float3(0, 0, 1))), 2.0);
                fixed4 c = _Color * (fresnel * _Glow + 0.2);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
