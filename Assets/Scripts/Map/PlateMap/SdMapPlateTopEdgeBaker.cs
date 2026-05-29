using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按每个板块网格顶面的几何外轮廓，烘焙顶点到轮廓的距离（写入顶点色 R，0=轮廓边，1=最内侧）。
/// </summary>
public static class SdMapPlateTopEdgeBaker
{
    private struct EdgeKey
    {
        public int A;
        public int B;

        public EdgeKey(int a, int b)
        {
            A = Mathf.Min(a, b);
            B = Mathf.Max(a, b);
        }

        public long ToLongKey()
        {
            return ((long)A << 32) | (uint)B;
        }
    }

    private struct EdgeSegment2D
    {
        public Vector2 A;
        public Vector2 B;
    }

    /// <summary>
    /// 为 MeshFilter 的网格烘焙顶面轮廓距离场。
    /// </summary>
    public static void Bake(MeshFilter meshFilter, float topNormalThreshold = 0.85f)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Mesh mesh = GetOrCreateBakeMesh(meshFilter);
        if (mesh == null)
        {
            return;
        }

        BakeIntoMesh(mesh, topNormalThreshold);
    }

    private static Mesh GetOrCreateBakeMesh(MeshFilter meshFilter)
    {
        Mesh shared = meshFilter.sharedMesh;
        if (shared == null)
        {
            return null;
        }

#if UNITY_EDITOR
        string sourceName = shared.name
            .Replace(" Instance", string.Empty)
            .Replace("_TopEdgeBake", string.Empty);
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Model/sd_map/sd_map.fbx");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Mesh sourceMesh && sourceMesh.name == sourceName)
            {
                shared = sourceMesh;
                break;
            }
        }
#endif

        if (shared.vertexCount == 0)
        {
            Debug.LogWarning($"[SdMapPlateTopEdgeBaker] 网格 {shared.name} 顶点数为 0，跳过烘焙。");
            return null;
        }

        Mesh copy = Object.Instantiate(shared);
        if (copy.vertexCount == 0)
        {
            Debug.LogWarning($"[SdMapPlateTopEdgeBaker] 无法复制网格 {shared.name}，请开启模型 Read/Write。");
            Object.DestroyImmediate(copy);
            return null;
        }

        copy.name = shared.name.Replace(" Instance", string.Empty) + "_TopEdgeBake";
        if (copy.normals == null || copy.normals.Length != copy.vertexCount)
        {
            copy.RecalculateNormals();
        }

        meshFilter.sharedMesh = copy;
        return copy;
    }

    /// <summary>
    /// 将顶面外轮廓距离写入 mesh.colors[].r。
    /// </summary>
    public static void BakeIntoMesh(Mesh mesh, float topNormalThreshold = 0.85f)
    {
        if (mesh == null)
        {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
        {
            return;
        }

        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        int vertexCount = vertices.Length;
        bool[] isTopVertex = new bool[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            // 本模型顶面朝局部 -Y
            isTopVertex[i] = normals[i].y <= -topNormalThreshold;
        }

        float[] distances = ComputeTopInwardGridDistances(vertices, triangles, isTopVertex);
        if (distances == null)
        {
            return;
        }

        Color[] colors = mesh.colors;
        if (colors == null || colors.Length != vertexCount)
        {
            colors = new Color[vertexCount];
        }

        float maxDist = 1e-6f;
        for (int i = 0; i < vertexCount; i++)
        {
            if (isTopVertex[i] && distances[i] > maxDist)
            {
                maxDist = distances[i];
            }
        }

        for (int i = 0; i < vertexCount; i++)
        {
            float normalized = isTopVertex[i] ? Mathf.Clamp01(distances[i] / maxDist) : 0f;
            colors[i] = new Color(normalized, 0f, 0f, 1f);
        }

        mesh.colors = colors;
    }

    /// <summary>
    /// 在顶面 XZ 投影栅格上做向内距离场，避免“顶点全在轮廓上”导致距离为 0。
    /// </summary>
    private static float[] ComputeTopInwardGridDistances(
        Vector3[] vertices,
        int[] triangles,
        bool[] isTopVertex,
        int gridResolution = 128)
    {
        int vertexCount = vertices.Length;
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        int resX = gridResolution;
        int resZ = gridResolution;
        bool[,] inside = new bool[resX, resZ];
        bool hasTopTriangle = false;

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];
            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0);
            if (faceNormal.sqrMagnitude < 1e-10f || Mathf.Abs(faceNormal.normalized.y) < 0.85f)
            {
                continue;
            }

            hasTopTriangle = true;
            ExpandBounds(v0, ref minX, ref maxX, ref minZ, ref maxZ);
            ExpandBounds(v1, ref minX, ref maxX, ref minZ, ref maxZ);
            ExpandBounds(v2, ref minX, ref maxX, ref minZ, ref maxZ);
        }

        if (!hasTopTriangle)
        {
            return null;
        }

        float padding = 0.05f;
        float sizeX = Mathf.Max(maxX - minX, 1e-4f);
        float sizeZ = Mathf.Max(maxZ - minZ, 1e-4f);
        minX -= sizeX * padding;
        maxX += sizeX * padding;
        minZ -= sizeZ * padding;
        maxZ += sizeZ * padding;
        sizeX = Mathf.Max(maxX - minX, 1e-4f);
        sizeZ = Mathf.Max(maxZ - minZ, 1e-4f);

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];
            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0);
            if (faceNormal.sqrMagnitude < 1e-10f || Mathf.Abs(faceNormal.normalized.y) < 0.85f)
            {
                continue;
            }

            RasterizeTopTriangle(v0, v1, v2, inside, resX, resZ, minX, minZ, sizeX, sizeZ);
        }

        float[,] gridDist = BuildInwardDistanceField(inside, resX, resZ, sizeX / resX, sizeZ / resZ);
        int insideCount = 0;
        int interiorCount = 0;
        float gridMax = 0f;
        for (int z = 0; z < resZ; z++)
        {
            for (int x = 0; x < resX; x++)
            {
                if (!inside[x, z])
                {
                    continue;
                }

                insideCount++;
                float d = gridDist[x, z];
                if (d < float.MaxValue * 0.5f && d > 0.0001f)
                {
                    interiorCount++;
                }

                if (d < float.MaxValue * 0.5f && d > gridMax)
                {
                    gridMax = d;
                }
            }
        }

        float[] distances = new float[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 p = new Vector2(vertices[i].x, vertices[i].z);
            float sampled = SampleGridDistance(gridDist, p, resX, resZ, minX, minZ, sizeX, sizeZ);
            distances[i] = isTopVertex[i] ? sampled : 0f;
        }

        return distances;
    }

    private static void RasterizeTopTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        bool[,] inside,
        int resX,
        int resZ,
        float minX,
        float minZ,
        float sizeX,
        float sizeZ)
    {
        float triMinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float triMaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float triMinZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
        float triMaxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));

        int x0 = Mathf.Clamp(Mathf.FloorToInt((triMinX - minX) / sizeX * resX), 0, resX - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((triMaxX - minX) / sizeX * resX), 0, resX - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((triMinZ - minZ) / sizeZ * resZ), 0, resZ - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((triMaxZ - minZ) / sizeZ * resZ), 0, resZ - 1);

        Vector2 p0 = new Vector2(a.x, a.z);
        Vector2 p1 = new Vector2(b.x, b.z);
        Vector2 p2 = new Vector2(c.x, c.z);

        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float px = minX + (x + 0.5f) / resX * sizeX;
                float pz = minZ + (z + 0.5f) / resZ * sizeZ;
                Vector2 p = new Vector2(px, pz);
                if (PointInTriangle2D(p, p0, p1, p2))
                {
                    inside[x, z] = true;
                }
            }
        }
    }

    private static void ExpandBounds(
        Vector3 p,
        ref float minX,
        ref float maxX,
        ref float minZ,
        ref float maxZ)
    {
        if (p.x < minX)
        {
            minX = p.x;
        }

        if (p.x > maxX)
        {
            maxX = p.x;
        }

        if (p.z < minZ)
        {
            minZ = p.z;
        }

        if (p.z > maxZ)
        {
            maxZ = p.z;
        }
    }

    private static bool PointInTriangle2D(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v0 = c - a;
        Vector2 v1 = b - a;
        Vector2 v2 = p - a;
        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);
        float denom = dot00 * dot11 - dot01 * dot01;
        if (Mathf.Abs(denom) < 1e-12f)
        {
            return false;
        }

        float inv = 1f / denom;
        float u = (dot11 * dot02 - dot01 * dot12) * inv;
        float v = (dot00 * dot12 - dot01 * dot02) * inv;
        return u >= 0f && v >= 0f && u + v <= 1f;
    }

    private static float[,] BuildInwardDistanceField(
        bool[,] inside,
        int resX,
        int resZ,
        float cellSizeX,
        float cellSizeZ)
    {
        float[,] dist = new float[resX, resZ];
        var queue = new Queue<Vector2Int>();
        float cellDist = Mathf.Sqrt(cellSizeX * cellSizeX + cellSizeZ * cellSizeZ);

        for (int z = 0; z < resZ; z++)
        {
            for (int x = 0; x < resX; x++)
            {
                dist[x, z] = float.MaxValue;
                if (!inside[x, z])
                {
                    continue;
                }

                if (IsBoundaryCell(inside, x, z, resX, resZ))
                {
                    dist[x, z] = 0f;
                    queue.Enqueue(new Vector2Int(x, z));
                }
            }
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            float baseDist = dist[c.x, c.y];
            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i];
                int nz = c.y + dz[i];
                if (nx < 0 || nz < 0 || nx >= resX || nz >= resZ || !inside[nx, nz])
                {
                    continue;
                }

                float step = (dx[i] != 0) ? cellSizeX : cellSizeZ;
                float next = baseDist + step;
                if (next < dist[nx, nz])
                {
                    dist[nx, nz] = next;
                    queue.Enqueue(new Vector2Int(nx, nz));
                }
            }
        }

        return dist;
    }

    private static bool IsBoundaryCell(bool[,] inside, int x, int z, int resX, int resZ)
    {
        if (!inside[x, z])
        {
            return false;
        }

        if (x == 0 || z == 0 || x == resX - 1 || z == resZ - 1)
        {
            return true;
        }

        return !inside[x - 1, z] || !inside[x + 1, z] || !inside[x, z - 1] || !inside[x, z + 1];
    }

    private static float SampleGridDistance(
        float[,] gridDist,
        Vector2 p,
        int resX,
        int resZ,
        float minX,
        float minZ,
        float sizeX,
        float sizeZ)
    {
        float u = Mathf.Clamp01((p.x - minX) / sizeX);
        float v = Mathf.Clamp01((p.y - minZ) / sizeZ);
        float fx = u * (resX - 1);
        float fz = v * (resZ - 1);
        int x0 = Mathf.FloorToInt(fx);
        int z0 = Mathf.FloorToInt(fz);
        int x1 = Mathf.Min(x0 + 1, resX - 1);
        int z1 = Mathf.Min(z0 + 1, resZ - 1);
        float tx = fx - x0;
        float tz = fz - z0;

        float d00 = gridDist[x0, z0];
        float d10 = gridDist[x1, z0];
        float d01 = gridDist[x0, z1];
        float d11 = gridDist[x1, z1];
        if (d00 >= float.MaxValue * 0.5f)
        {
            return 0f;
        }

        if (d10 >= float.MaxValue * 0.5f)
        {
            return 0f;
        }

        if (d01 >= float.MaxValue * 0.5f)
        {
            return 0f;
        }

        if (d11 >= float.MaxValue * 0.5f)
        {
            return 0f;
        }

        float d0 = Mathf.Lerp(d00, d10, tx);
        float d1 = Mathf.Lerp(d01, d11, tx);
        return Mathf.Lerp(d0, d1, tz);
    }

    private static int CountTrue(bool[] flags)
    {
        int count = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i])
            {
                count++;
            }
        }

        return count;
    }

    private static List<EdgeSegment2D> CollectTopBoundarySegments(
        Vector3[] vertices,
        int[] triangles,
        bool[] isTopVertex)
    {
        var topEdgeCount = new Dictionary<long, int>();
        var boundaryEdges = new HashSet<long>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];

            RegisterEdge(i0, i1, isTopVertex, topEdgeCount, boundaryEdges);
            RegisterEdge(i1, i2, isTopVertex, topEdgeCount, boundaryEdges);
            RegisterEdge(i2, i0, isTopVertex, topEdgeCount, boundaryEdges);
        }

        foreach (KeyValuePair<long, int> pair in topEdgeCount)
        {
            if (pair.Value == 1)
            {
                boundaryEdges.Add(pair.Key);
            }
        }

        var segments = new List<EdgeSegment2D>(boundaryEdges.Count);
        foreach (long key in boundaryEdges)
        {
            int a = (int)(key >> 32);
            int b = (int)(uint)key;
            segments.Add(new EdgeSegment2D
            {
                A = new Vector2(vertices[a].x, vertices[a].z),
                B = new Vector2(vertices[b].x, vertices[b].z)
            });
        }

        return segments;
    }

    private static void RegisterEdge(
        int a,
        int b,
        bool[] isTopVertex,
        Dictionary<long, int> topEdgeCount,
        HashSet<long> boundaryEdges)
    {
        bool topA = isTopVertex[a];
        bool topB = isTopVertex[b];
        if (topA != topB)
        {
            boundaryEdges.Add(new EdgeKey(a, b).ToLongKey());
            return;
        }

        if (!topA || !topB)
        {
            return;
        }

        long key = new EdgeKey(a, b).ToLongKey();
        if (topEdgeCount.TryGetValue(key, out int count))
        {
            topEdgeCount[key] = count + 1;
        }
        else
        {
            topEdgeCount[key] = 1;
        }
    }

    private static float DistancePointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        if (denom < 1e-10f)
        {
            return Vector2.Distance(p, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }
}
