Shader "Car07/Hologram Surface BuiltIn"
{
    Properties
    {
        _LowBlue ("Lower Blue (linear RGB)", Vector) = (0.0001, 0.003, 0.018, 0)
        _HighBlue ("Upper Blue (linear RGB)", Vector) = (0.0001, 0.016, 0.30, 0)
        _RimColor ("Rim Color (linear HDR RGB)", Vector) = (0, 0.78, 1.1, 0)
        _RimPower ("Rim Power", Range(1, 10)) = 4.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.4
        _ScanStrength ("Scan Strength (0 = off)", Range(0, 0.5)) = 0.13
        _ScanCycles ("Scan Cycles Across Model Height", Float) = 21.4531
        _ScanSpeed ("Scan Phase Speed (radians/sec)", Float) = 1.7
        _MeshMinY ("Mesh Local Minimum Y", Float) = 0
        _MeshHeight ("Mesh Local Height", Float) = 0.336985
    }
    SubShader
    {
        // Opaque lets Unity's depth/normals replacement pass see this mesh.
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off
        Pass
        {
            Tags { "LightMode"="Always" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _LowBlue, _HighBlue, _RimColor;
            float _RimPower, _RimStrength, _ScanStrength, _ScanCycles, _ScanSpeed;
            float _MeshMinY, _MeshHeight;
            struct Attributes { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 localNormal : TEXCOORD2;
                float height01 : TEXCOORD3;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.localNormal = input.normal;
                output.height01 = (input.vertex.y - _MeshMinY) / max(abs(_MeshHeight), 0.000001);
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.worldNormal);
                float3 view = normalize(UnityWorldSpaceViewDir(input.worldPosition));
                float rim = pow(1.0 - saturate(dot(n, view)), _RimPower);
                // Original Car07 local height range: 0 ... 0.336985.
                float top = smoothstep(0.075 / 0.336985, 0.31 / 0.336985, input.height01);
                top *= 0.86 + 0.14 * max(normalize(input.localNormal).y, 0.0);
                float3 blue = lerp(_LowBlue.rgb, _HighBlue.rgb, top);
                float bands = 0.5 + 0.5 * sin(input.height01 * _ScanCycles * 6.28318530718 - _Time.y * _ScanSpeed);
                blue *= 1.0 - _ScanStrength * bands;
                return float4(blue + _RimColor.rgb * rim * _RimStrength, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
