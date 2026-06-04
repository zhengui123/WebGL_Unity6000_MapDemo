using UnityEngine;

/// <summary>
/// T：正向过渡（AllPlateMap → GaodeMap）；Y：倒放过渡（GaodeMap → AllPlateMap）。
/// </summary>
public class PlateToGaodeMapTransitionDemo : MonoBehaviour
{
    [SerializeField] private PlateToGaodeMapTransitionController _transitionController;
    [SerializeField] private string _provinceName = "山东";
    [SerializeField] private KeyCode _playKey = KeyCode.T;
    [SerializeField] private KeyCode _reverseKey = KeyCode.Y;

    private void Update()
    {
        PlateToGaodeMapTransitionController controller = ResolveController();
        if (controller == null)
        {
            return;
        }

        if (Input.GetKeyDown(_playKey))
        {
            controller.PlayTransition(_provinceName);
        }
        else if (Input.GetKeyDown(_reverseKey))
        {
            controller.PlayTransitionReverse(_provinceName);
        }
    }

    private PlateToGaodeMapTransitionController ResolveController()
    {
        return _transitionController != null
            ? _transitionController
            : PlateToGaodeMapTransitionController.Instance;
    }
}
