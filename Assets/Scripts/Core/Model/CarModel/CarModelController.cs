using UnityEngine;

public class CarModelController : MonoBehaviour
{
    public GameObject showStandObj;
    public MouseDragYawRotate carModelRotateController;
    void Start()
    {
        showStandObj.SetActive(false);
        EventManager.Instance.OnPlateToVehicleViewTransitionStarted += OnPlateToVehicleViewTransitionStarted;
        EventManager.Instance.OnPlateToVehicleViewTransitionCompleted += OnPlateToVehicleViewTransitionCompleted;
        EventManager.Instance.OnVehicleToPlateViewTransitionStarted += OnVehicleToPlateViewTransitionStarted;
    }
    
    void OnDestroy()
    {
        EventManager.Instance.OnPlateToVehicleViewTransitionStarted -= OnPlateToVehicleViewTransitionStarted;
        EventManager.Instance.OnPlateToVehicleViewTransitionCompleted -= OnPlateToVehicleViewTransitionCompleted;
        EventManager.Instance.OnVehicleToPlateViewTransitionStarted -= OnVehicleToPlateViewTransitionStarted;
    }

    void Update()
    {
        
    }

    /// <summary>
    /// 车辆板块开始进入
    /// </summary>
    public void OnPlateToVehicleViewTransitionStarted(string provinceName)
    {
        showStandObj.SetActive(false);
        Debug.Log("车辆板块开始进入");
        carModelRotateController.ResetRotation();
        carModelRotateController.SetDragEnabled(false);
    }

    /// <summary>
    /// 车辆板块进入完成
    /// </summary>
    public void OnPlateToVehicleViewTransitionCompleted(string provinceName)
    {
        Debug.Log("车辆板块进入完成");

        showStandObj.SetActive(true);
        carModelRotateController.SetDragEnabled(true);
    }

    /// <summary>
    /// 车辆板块开始退出
    /// </summary>
    public void OnVehicleToPlateViewTransitionStarted(string provinceName)
    {
        Debug.Log("车辆板块开始退出");
        showStandObj.SetActive(false);
        carModelRotateController.SetDragEnabled(false);

    }
}
