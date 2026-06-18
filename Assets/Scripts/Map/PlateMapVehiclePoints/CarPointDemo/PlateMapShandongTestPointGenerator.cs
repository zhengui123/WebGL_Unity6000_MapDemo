using System.Collections.Generic;

/// <summary>山东省内测试车辆点位生成。</summary>
public static class PlateMapShandongTestPointGenerator
{
    public static VehicleMapPointData[] Generate(
        string plateMapName,
        PlateMapShandongProvincePointFilter provinceFilter,
        int count,
        int randomSeed = 0,
        bool useProvinceBoundarySampling = true)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(plateMapName))
        {
            return System.Array.Empty<VehicleMapPointData>();
        }

        if (useProvinceBoundarySampling &&
            provinceFilter != null &&
            provinceFilter.StrictProvinceBoundary &&
            !provinceFilter.EnsureProvinceBoundaryLoaded())
        {
            return System.Array.Empty<VehicleMapPointData>();
        }

        PlateMapVehiclePointEvents.Instance.PublishGeoConverterRebuild(plateMapName);

        System.Random rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        var list = new List<VehicleMapPointData>(count);

        for (int i = 0; i < count; i++)
        {
            bool sampled = false;
            double lon = 0;
            double lat = 0;

            if (provinceFilter == null)
            {
                continue;
            }

            if (useProvinceBoundarySampling)
            {
                sampled = provinceFilter.TrySampleRandomLongitudeLatitude(plateMapName, rng, out lon, out lat);
            }
            else
            {
                sampled = provinceFilter.TrySampleRandomInFallbackRectangle(rng, out lon, out lat);
            }

            if (!sampled)
            {
                continue;
            }

            list.Add(new VehicleMapPointData
            {
                vehicleId = $"SD-{list.Count + 1:D3}",
                longitude = lon,
                latitude = lat,
                alertValue = (float)rng.NextDouble()
            });
        }

        return list.ToArray();
    }

    public static VehicleMapPointData[] GenerateFiltered(
        string plateMapName,
        PlateMapShandongProvincePointFilter provinceFilter,
        int count,
        int randomSeed = 0,
        bool useProvinceBoundarySampling = true)
    {
        VehicleMapPointData[] points = Generate(
            plateMapName, provinceFilter, count, randomSeed, useProvinceBoundarySampling);
        if (!useProvinceBoundarySampling || provinceFilter == null || points.Length == 0)
        {
            return points;
        }

        return provinceFilter.FilterVehiclePointsInProvince(points);
    }
}
