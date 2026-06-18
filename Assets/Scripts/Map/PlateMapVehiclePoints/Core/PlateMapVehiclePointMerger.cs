using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图局部坐标下近距离点位合并（网格加速）。仅对原始输入做一次聚合，幂等。
/// </summary>
public static class PlateMapVehiclePointMerger
{
    /// <summary>合并前单点：地图局部坐标 + 业务告警值。</summary>
    public struct InputPoint
    {
        public Vector3 LocalPosition;
        public float AlertValue;
    }

    /// <summary>合并后簇：显示位置、累加告警值、源点数量。</summary>
    public struct MergedPoint
    {
        public Vector3 LocalPosition;
        public float SummedAlertValue;
        public int SourceCount;
    }

    /// <summary>
    /// 将 XZ 平面距离小于 <paramref name="mergeDistance"/> 的点合并为簇。
    /// <paramref name="useAveragePosition"/> 为 true 时位置取簇内平均值（并查集传递合并）；
    /// 为 false 时以遍历顺序下尚未合并的判定点为圆心，吸收半径内点位，位置取圆心坐标。
    /// </summary>
    public static void Merge(
        IReadOnlyList<InputPoint> inputs,
        float mergeDistance,
        List<MergedPoint> output,
        bool useAveragePosition = false)
    {
        output.Clear();
        if (inputs == null || inputs.Count == 0)
        {
            return;
        }

        if (mergeDistance <= 0f)
        {
            EmitSingletons(inputs, output);
            return;
        }

        if (useAveragePosition)
        {
            MergeWithAveragePosition(inputs, mergeDistance, output);
        }
        else
        {
            MergeWithCenterPosition(inputs, mergeDistance, output);
        }
    }

    /// <summary>旧版：并查集传递合并，簇位置为质心（坐标平均）。</summary>
    private static void MergeWithAveragePosition(
        IReadOnlyList<InputPoint> inputs,
        float mergeDistance,
        List<MergedPoint> output)
    {
        int count = inputs.Count;
        var parent = new int[count];
        for (int i = 0; i < count; i++)
        {
            parent[i] = i;
        }

        float cellSize = mergeDistance;
        float mergeDistanceSqr = mergeDistance * mergeDistance;
        var grid = BuildSpatialGrid(inputs, cellSize);

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

    /// <summary>
    /// 以输入顺序下每个尚未吸收的判定点为圆心，将半径内点位合并；
    /// 显示位置固定为圆心点坐标（不做平均）。
    /// </summary>
    private static void MergeWithCenterPosition(
        IReadOnlyList<InputPoint> inputs,
        float mergeDistance,
        List<MergedPoint> output)
    {
        int count = inputs.Count;
        var consumed = new bool[count];
        float cellSize = mergeDistance;
        float mergeDistanceSqr = mergeDistance * mergeDistance;
        var grid = BuildSpatialGrid(inputs, cellSize);
        const int cellSearchRadius = 2;

        for (int centerIndex = 0; centerIndex < count; centerIndex++)
        {
            if (consumed[centerIndex])
            {
                continue;
            }

            InputPoint center = inputs[centerIndex];
            Vector3 centerPos = center.LocalPosition;
            float summedAlert = center.AlertValue;
            int sourceCount = 1;
            consumed[centerIndex] = true;

            int cellX = Mathf.FloorToInt(centerPos.x / cellSize);
            int cellZ = Mathf.FloorToInt(centerPos.z / cellSize);

            for (int ox = -cellSearchRadius; ox <= cellSearchRadius; ox++)
            {
                for (int oz = -cellSearchRadius; oz <= cellSearchRadius; oz++)
                {
                    if (!grid.TryGetValue(PackCellKey(cellX + ox, cellZ + oz), out List<int> bucket))
                    {
                        continue;
                    }

                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int candidateIndex = bucket[b];
                        if (candidateIndex == centerIndex || consumed[candidateIndex])
                        {
                            continue;
                        }

                        Vector3 candidatePos = inputs[candidateIndex].LocalPosition;
                        float dx = centerPos.x - candidatePos.x;
                        float dz = centerPos.z - candidatePos.z;
                        if (dx * dx + dz * dz > mergeDistanceSqr)
                        {
                            continue;
                        }

                        consumed[candidateIndex] = true;
                        summedAlert += inputs[candidateIndex].AlertValue;
                        sourceCount++;
                    }
                }
            }

            output.Add(new MergedPoint
            {
                LocalPosition = centerPos,
                SummedAlertValue = summedAlert,
                SourceCount = sourceCount
            });
        }
    }

    private static void EmitSingletons(IReadOnlyList<InputPoint> inputs, List<MergedPoint> output)
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
    }

    private static Dictionary<long, List<int>> BuildSpatialGrid(IReadOnlyList<InputPoint> inputs, float cellSize)
    {
        var grid = new Dictionary<long, List<int>>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++)
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

        return grid;
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
