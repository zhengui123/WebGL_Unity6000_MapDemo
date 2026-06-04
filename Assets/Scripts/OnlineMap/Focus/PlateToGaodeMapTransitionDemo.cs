using UnityEngine;

/// <summary>
/// 按 T：播放扫描线过渡，AllPlateMap 淡出，GaodeMap 显示。
/// </summary>
public class PlateToGaodeMapTransitionDemo : MonoBehaviour
{
    [SerializeField] private PlateToGaodeMapTransitionController _transitionController;
    [SerializeField] private string _provinceName = "山东";
    [SerializeField] private KeyCode _playKey = KeyCode.T;

    private void Update()
    {
        if (!Input.GetKeyDown(_playKey))
        {
            return;
        }

        PlateToGaodeMapTransitionController controller = _transitionController != null
            ? _transitionController
            : PlateToGaodeMapTransitionController.Instance;

        if (controller != null)
        {
            controller.PlayTransition(_provinceName);
        }
    }
}
