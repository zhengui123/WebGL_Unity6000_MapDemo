using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为三角面线框生成重心坐标：拆成独立三角顶点，COLOR.rgb = (1,0,0)/(0,1,0)/(0,0,1)。
/// WebGL 无 Geometry Shader 时由 <see cref="EcuWireframeMeshPrep"/> + ConvexEdge Shader 使用。
/// </summary>
public static class EcuBarycentricMeshUtility
{
    private static readonly Color Bary0 = new Color(1f, 0f, 0f, 1f);
    private static readonly Color Bary1 = new Color(0f, 1f, 0f, 1f);
    private static readonly Color Bary2 = new Color(0f, 0f, 1f, 1f);

    /// <summary>由源网格生成带重心 COLOR 的新网格（不修改源资源）。</summary>
    public static Mesh CreateBarycentricMesh(Mesh source)
    {
        if (source == null)
        {
            return null;
        }

        int[] srcTris = source.triangles;
        if (srcTris == null || srcTris.Length < 3)
        {
            return null;
        }

        Vector3[] srcVerts = source.vertices;
        Vector3[] srcNormals = source.normals;
        Vector2[] srcUv0 = source.uv;
        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasUv0 = srcUv0 != null && srcUv0.Length == srcVerts.Length;

        int triCount = srcTris.Length / 3;
        int vertCount = triCount * 3;

        Vector3[] verts = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uv0 = hasUv0 ? new Vector2[vertCount] : null;
        Color[] colors = new Color[vertCount];
        int[] tris = new int[vertCount];

        for (int t = 0; t < triCount; t++)
        {
            int i0 = srcTris[t * 3];
            int i1 = srcTris[t * 3 + 1];
            int i2 = srcTris[t * 3 + 2];
            int o = t * 3;

            verts[o] = srcVerts[i0];
            verts[o + 1] = srcVerts[i1];
            verts[o + 2] = srcVerts[i2];

            if (hasNormals)
            {
                normals[o] = srcNormals[i0];
                normals[o + 1] = srcNormals[i1];
                normals[o + 2] = srcNormals[i2];
            }
            else
            {
                Vector3 n = Vector3.Normalize(Vector3.Cross(verts[o + 1] - verts[o], verts[o + 2] - verts[o]));
                normals[o] = n;
                normals[o + 1] = n;
                normals[o + 2] = n;
            }

            if (hasUv0)
            {
                uv0[o] = srcUv0[i0];
                uv0[o + 1] = srcUv0[i1];
                uv0[o + 2] = srcUv0[i2];
            }

            colors[o] = Bary0;
            colors[o + 1] = Bary1;
            colors[o + 2] = Bary2;

            tris[o] = o;
            tris[o + 1] = o + 1;
            tris[o + 2] = o + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = source.name + "_BaryWire";
        if (vertCount > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = verts;
        mesh.normals = normals;
        if (hasUv0)
        {
            mesh.uv = uv0;
        }

        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 若 MeshFilter 尚未准备过，则实例化带重心的网格并替换 mf.mesh。
    /// </summary>
    public static bool TryPrepareMeshFilter(MeshFilter meshFilter, HashSet<int> preparedInstanceIds = null)
    {
        if (meshFilter == null)
        {
            return false;
        }

        Mesh shared = meshFilter.sharedMesh;
        if (shared == null)
        {
            return false;
        }

        int id = meshFilter.GetInstanceID();
        if (preparedInstanceIds != null && preparedInstanceIds.Contains(id))
        {
            return false;
        }

        // 已是重心网格（名称后缀）则跳过
        if (meshFilter.mesh != null && meshFilter.mesh.name.EndsWith("_BaryWire"))
        {
            preparedInstanceIds?.Add(id);
            return false;
        }

        Mesh bary = CreateBarycentricMesh(shared);
        if (bary == null)
        {
            return false;
        }

        meshFilter.mesh = bary;
        preparedInstanceIds?.Add(id);
        return true;
    }
}
