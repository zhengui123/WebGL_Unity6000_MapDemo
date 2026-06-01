using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// GPU Instancing 逐实例数据（与 CarPointGlowInstanced.shader 中 StructuredBuffer 布局一致）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CarPointGpuInstanceData
{
    public Vector4 Color;
    public float GlowIntensity;
    public float Pad0;
    public float Pad1;
    /// <summary>对齐至 32 字节，与 HLSL StructuredBuffer 元素步长一致。</summary>
    public float Pad2;

    // Vector4(16) + 4×float(16) = 32；保留布局供后续扩展，WebGL 走 MPB 逐实例 Vector4。
    public static readonly int Stride = Marshal.SizeOf<CarPointGpuInstanceData>();

    /// <summary>打包为 GPU Instancing 属性（rgb=颜色，a=中心亮度），兼容 WebGL。</summary>
    public Vector4 ToInstancingVector()
    {
        return new Vector4(Color.x, Color.y, Color.z, GlowIntensity);
    }
}
