using UnityEngine;

public class POI_Demo : MonoBehaviour
{

    public POI_Manager poiManager;
    public PlateMapVehiclePointController plateMapVehiclePointController;

    void Start()
    {
        for (int i = 0; i < plateMapVehiclePointController.VehiclePoints.Length; i++)
        {
            poiManager.SpawnPoi( plateMapVehiclePointController.VehiclePoints[i].longitude, plateMapVehiclePointController.VehiclePoints[i].latitude);
        }

      
    }

    void Update()
    {
        
    }
}
