using UnityEngine;

public class CarModelController : MonoBehaviour
{
    public GameObject showStandObj;
    void Start()
    {
        showStandObj.SetActive(false);
        EventManager.Instance.OnPlateToVehicleViewTransitionCompleted += OnPlateToVehicleViewTransitionCompleted;
        EventManager.Instance.OnVehicleToPlateViewTransitionStarted += onVehicleToPlateViewTransitionStarted;
    }

    void Update()
    {
        
    }

    public void OnPlateToVehicleViewTransitionCompleted(string provinceName)
    {
        showStandObj.SetActive(true);

    }

     public void onVehicleToPlateViewTransitionStarted(string provinceName)
    {
        showStandObj.SetActive(false);

    }
}
