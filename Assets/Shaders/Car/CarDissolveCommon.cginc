// 车辆模型溶解：世界空间噪声裁剪 + 边缘发光
#ifndef CAR_DISSOLVE_COMMON_INCLUDED
#define CAR_DISSOLVE_COMMON_INCLUDED

float _DissolveAmount;
float _DissolveEdgeWidth;
fixed4 _DissolveEdgeColor;
float _DissolveNoiseScale;

float CarDissolveHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float CarDissolveNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = CarDissolveHash(i);
    float n100 = CarDissolveHash(i + float3(1, 0, 0));
    float n010 = CarDissolveHash(i + float3(0, 1, 0));
    float n110 = CarDissolveHash(i + float3(1, 1, 0));
    float n001 = CarDissolveHash(i + float3(0, 0, 1));
    float n101 = CarDissolveHash(i + float3(1, 0, 1));
    float n011 = CarDissolveHash(i + float3(0, 1, 1));
    float n111 = CarDissolveHash(i + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float nxy0 = lerp(nx00, nx10, f.y);
    float nxy1 = lerp(nx01, nx11, f.y);
    return lerp(nxy0, nxy1, f.z);
}

void CarDissolveClip(float3 worldPos)
{
    if (_DissolveAmount <= 0.00001)
    {
        return;
    }

    float noise = CarDissolveNoise(worldPos * max(_DissolveNoiseScale, 0.01));
    clip(noise - _DissolveAmount);
}

void ApplyCarDissolve(float3 worldPos, inout fixed4 col)
{
    if (_DissolveAmount <= 0.00001)
    {
        return;
    }

    float noise = CarDissolveNoise(worldPos * max(_DissolveNoiseScale, 0.01));
    clip(noise - _DissolveAmount);

    float edge = smoothstep(_DissolveAmount, _DissolveAmount + max(_DissolveEdgeWidth, 0.001), noise);
    col.rgb += _DissolveEdgeColor.rgb * edge * col.a;
}

#endif
