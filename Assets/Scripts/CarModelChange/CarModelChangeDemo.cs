using UnityEngine;

/// <summary>
/// 键盘测试：C 切换到 KJ_Car，V 切换回 RealyCar。
/// </summary>
public class CarModelChangeDemo : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            CarModelChangeController.Instance?.SwitchToKjCar();
        }
        else if (Input.GetKeyDown(KeyCode.V))
        {
            CarModelChangeController.Instance?.SwitchToRealyCar();
        }
    }
}
