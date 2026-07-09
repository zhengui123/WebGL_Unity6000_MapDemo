using UnityEngine;

public class POI_Demo : MonoBehaviour
{

    public POI_Manager poiManager;
    public PlateMapVehiclePointController plateMapVehiclePointController;
    [SerializeField] private string provinceCode = "370000";
    [SerializeField] private POIType poiType = POIType.yellow;

    void Start()
    {
        if (poiManager == null || plateMapVehiclePointController == null)
        {
            Debug.LogWarning("[POI_Demo] 缺少 poiManager 或 plateMapVehiclePointController 引用。");
            return;
        }

        for (int i = 0; i < plateMapVehiclePointController.VehiclePoints.Length; i++)
        {
            poiManager.SpawnPoi(
                provinceCode,
                poiType,
                plateMapVehiclePointController.VehiclePoints[i].longitude,
                plateMapVehiclePointController.VehiclePoints[i].latitude);
        }

      
    }

    void Update()
    {
        
    }
}
