using UnityEngine;

/// <summary>
/// 键盘测试：B 城市隐藏正播，N 城市隐藏倒播。
/// </summary>
public class CityHideTransitionDemo : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            CityHideTransitionController.Instance?.PlayHideTransition();
        }
        else if (Input.GetKeyDown(KeyCode.N))
        {
            CityHideTransitionController.Instance?.PlayHideTransitionReverse();
        }
    }
}
