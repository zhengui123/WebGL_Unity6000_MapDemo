// CarHologram 共享：表面朝向裁剪 + 透明度计算
#ifndef CAR_HOLOGRAM_COMMON_INCLUDED
#define CAR_HOLOGRAM_COMMON_INCLUDED

fixed4 _BaseColor;
fixed4 _GlowColor;
float _Alpha;
float _FillStrength;
float _FacingMinGlow;
float _SurfaceFacingMin;
float _DepthAlphaClip;
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

struct HologramAppdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct HologramV2f
{
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float2 uv : TEXCOORD2;
};

HologramV2f vert(HologramAppdata v)
{
    HologramV2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.uv = v.uv;
    return o;
}

float HologramGridLines(float2 uv, float scale, float lineWidth)
{
    float2 g = uv * scale;
    float2 fw = abs(frac(g - 0.5) - 0.5) / max(fwidth(g), 1e-5);
    float2 gridMask = 1.0 - saturate(fw / max(lineWidth, 0.001));
    return saturate(max(gridMask.x, gridMask.y));
}

// ndvOut：法线与视线夹角余弦（朝相机为 1）
void HologramSurfaceBasis(HologramV2f i, out float ndvOut, out float rimOut, out float facingOut)
{
    float3 n = normalize(i.worldNormal);
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
    ndvOut = saturate(dot(n, viewDir));
    rimOut = pow(1.0 - ndvOut, _FresnelPower);
    facingOut = lerp(_FacingMinGlow, 1.0, rimOut);
}

void HologramClipSurfaceFacing(float ndv)
{
    clip(ndv - _SurfaceFacingMin);
}

float HologramComputeAlpha(HologramV2f i, float ndv, float rim, float facing)
{
    float2 gridUv = i.worldPos.xz * _GridWorldScale + i.uv * _GridScale * 0.15;
    float grid = HologramGridLines(gridUv, _GridScale, _GridLineWidth) * _GridIntensity;
    float scan = (sin((i.worldPos.y + _Time.y * _ScanSpeed) * _ScanDensity) * 0.5 + 0.5) * _ScanIntensity;
    float detailAlpha = saturate(0.25 + rim * _RimAlphaBoost + grid * 0.18 + scan * 0.12);
    return _Alpha * detailAlpha;
}

fixed4 HologramComputeColor(HologramV2f i, float ndv, float rim, float facing)
{
    float2 gridUv = i.worldPos.xz * _GridWorldScale + i.uv * _GridScale * 0.15;
    float grid = HologramGridLines(gridUv, _GridScale, _GridLineWidth) * _GridIntensity;
    float scan = (sin((i.worldPos.y + _Time.y * _ScanSpeed) * _ScanDensity) * 0.5 + 0.5) * _ScanIntensity;

    float3 fill = _BaseColor.rgb * _FillStrength * facing;
    float3 glowBase = _GlowColor.rgb * (_FacingMinGlow * 0.35);
    float3 rimCol = _GlowColor.rgb * rim * _FresnelIntensity;
    float3 gridCol = _GlowColor.rgb * grid * (0.45 + 0.55 * facing);
    float3 scanCol = _GlowColor.rgb * scan * facing * 0.45;
    float3 rgb = fill + glowBase + rimCol + gridCol + scanCol;
    float a = HologramComputeAlpha(i, ndv, rim, facing);
    return fixed4(rgb, a);
}

fixed4 fragDepth(HologramV2f i) : SV_Target
{
    float ndv, rim, facing;
    HologramSurfaceBasis(i, ndv, rim, facing);
    HologramClipSurfaceFacing(ndv);
    float a = HologramComputeAlpha(i, ndv, rim, facing);
    clip(a - _DepthAlphaClip);
    return 0;
}

fixed4 fragColor(HologramV2f i) : SV_Target
{
    float ndv, rim, facing;
    HologramSurfaceBasis(i, ndv, rim, facing);
    HologramClipSurfaceFacing(ndv);
    return HologramComputeColor(i, ndv, rim, facing);
}

#endif
