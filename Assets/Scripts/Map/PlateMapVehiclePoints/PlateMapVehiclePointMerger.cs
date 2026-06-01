using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图局部坐标下近距离点位合并（并查集 + 网格加速）。仅对原始输入做一次聚合，幂等。
/// </summary>
public static class PlateMapVehiclePointMerger
{
    /// <summary>合并前单点：地图局部坐标 + 业务告警值。</summary>
    public struct InputPoint
    {
        public Vector3 LocalPosition;
        public float AlertValue;
    }

    /// <summary>合并后簇：质心位置、累加告警值、源点数量。</summary>
    public struct MergedPoint
    {
        public Vector3 LocalPosition;
        public float SummedAlertValue;
        public int SourceCount;
    }

    /// <summary>
    /// 将距离小于 mergeDistance 的点合并为簇：位置取平均，业务值累加。
    /// </summary>
    public static void Merge(
        IReadOnlyList<InputPoint> inputs,
        float mergeDistance,
        List<MergedPoint> output)
    {
        output.Clear();
        if (inputs == null || inputs.Count == 0)
        {
            return;
        }

        if (mergeDistance <= 0f)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                InputPoint p = inputs[i];
                output.Add(new MergedPoint
                {
                    LocalPosition = p.LocalPosition,
                    SummedAlertValue = p.AlertValue,
                    SourceCount = 1
                });
            }

            return;
        }

        int count = inputs.Count;
        var parent = new int[count];
        for (int i = 0; i < count; i++)
        {
            parent[i] = i;
        }

        float cellSize = mergeDistance;
        float mergeDistanceSqr = mergeDistance * mergeDistance;
        var grid = new Dictionary<long, List<int>>(count);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = inputs[i].LocalPosition;
            int cellX = Mathf.FloorToInt(pos.x / cellSize);
            int cellZ = Mathf.FloorToInt(pos.z / cellSize);
            long key = PackCellKey(cellX, cellZ);
            if (!grid.TryGetValue(key, out List<int> bucket))
            {
                bucket = new List<int>(4);
                grid[key] = bucket;
            }

            bucket.Add(i);
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 posI = inputs[i].LocalPosition;
            int cellX = Mathf.FloorToInt(posI.x / cellSize);
            int cellZ = Mathf.FloorToInt(posI.z / cellSize);

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (!grid.TryGetValue(PackCellKey(cellX + ox, cellZ + oz), out List<int> bucket))
                    {
                        continue;
                    }

                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int j = bucket[b];
                        if (j <= i)
                        {
                            continue;
                        }

                        Vector3 posJ = inputs[j].LocalPosition;
                        float dx = posI.x - posJ.x;
                        float dz = posI.z - posJ.z;
                        if (dx * dx + dz * dz <= mergeDistanceSqr)
                        {
                            Union(parent, i, j);
                        }
                    }
                }
            }
        }

        var clusters = new Dictionary<int, MergedPoint>();
        for (int i = 0; i < count; i++)
        {
            int root = Find(parent, i);
            InputPoint p = inputs[i];
            if (!clusters.TryGetValue(root, out MergedPoint merged))
            {
                merged = new MergedPoint
                {
                    LocalPosition = p.LocalPosition,
                    SummedAlertValue = p.AlertValue,
                    SourceCount = 1
                };
            }
            else
            {
                merged.LocalPosition += p.LocalPosition;
                merged.SummedAlertValue += p.AlertValue;
                merged.SourceCount++;
            }

            clusters[root] = merged;
        }

        foreach (KeyValuePair<int, MergedPoint> pair in clusters)
        {
            MergedPoint m = pair.Value;
            if (m.SourceCount > 1)
            {
                float inv = 1f / m.SourceCount;
                m.LocalPosition *= inv;
            }

            output.Add(m);
        }
    }

    /// <summary>将网格单元坐标打包为 Dictionary 键（XZ 平面均匀网格加速邻域查询）。</summary>
    private static long PackCellKey(int cellX, int cellZ)
    {
        return ((long)cellX << 32) | (uint)cellZ;
    }

    /// <summary>并查集 Find（路径压缩）。</summary>
    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    /// <summary>并查集 Union：将 b 的根挂到 a 的根。</summary>
    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a);
        int rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
    }
}
