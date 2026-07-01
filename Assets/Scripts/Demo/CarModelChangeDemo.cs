using UnityEngine;

/// <summary>
/// 键盘测试车辆溶解切换，需场景中存在 CarModelDissolveController。
/// C：RealyCar → KJ_Car；V：KJ_Car → RealyCar。
/// </summary>
public class CarModelChangeDemo : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            CarModelDissolveController.Instance?.SwitchToKjCar();
        }
        else if (Input.GetKeyDown(KeyCode.V))
        {
            CarModelDissolveController.Instance?.SwitchToRealyCar();
        }
    }
}
