using System;
using UnityEngine;

/// <summary>车辆点位 JSON：{ "points": [ VehicleMapPointData, ... ] }</summary>
public static class VehicleMapPointJson
{
    [Serializable]
    private class Payload
    {
        public VehicleMapPointData[] points;
    }

    public static string ToJson(VehicleMapPointData[] points)
    {
        return JsonUtility.ToJson(new Payload { points = points ?? Array.Empty<VehicleMapPointData>() });
    }

    public static bool TryParse(string json, out VehicleMapPointData[] points, out string errorMessage)
    {
        points = Array.Empty<VehicleMapPointData>();
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "JSON 为空。";
            return false;
        }

        Payload payload = JsonUtility.FromJson<Payload>(json);
        if (payload?.points == null)
        {
            errorMessage = "JSON 缺少 points 数组。";
            return false;
        }

        points = payload.points;
        return true;
    }
}
