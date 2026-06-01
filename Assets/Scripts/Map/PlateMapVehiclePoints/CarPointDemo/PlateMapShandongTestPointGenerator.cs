using System.Collections.Generic;

/// <summary>山东省内测试车辆点位生成。</summary>
public static class PlateMapShandongTestPointGenerator
{
    public static VehicleMapPointData[] Generate(
        string plateMapName,
        PlateMapShandongProvincePointFilter provinceFilter,
        int count,
        int randomSeed = 0)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(plateMapName))
        {
            return System.Array.Empty<VehicleMapPointData>();
        }

        if (provinceFilter != null &&
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
            if (provinceFilter == null ||
                !provinceFilter.TrySampleRandomLongitudeLatitude(plateMapName, rng, out double lon, out double lat))
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
}
