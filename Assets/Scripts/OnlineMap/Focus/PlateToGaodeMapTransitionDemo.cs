using UnityEngine;

/// <summary>
/// T：板块→GaodeMap；Y：板块过渡倒放；U：GaodeMap→City；I：City→GaodeMap 倒放。
/// </summary>
public class PlateToGaodeMapTransitionDemo : MonoBehaviour
{
    [SerializeField] private PlateToGaodeMapTransitionController _transitionController;
    [SerializeField] private GaodeToCityTransitionController _cityTransitionController;
    [SerializeField] private string _provinceName = "山东";
    [SerializeField] private KeyCode _playKey = KeyCode.T;
    [SerializeField] private KeyCode _reverseKey = KeyCode.Y;
    [SerializeField] private KeyCode _cityTransitionKey = KeyCode.U;
    [SerializeField] private KeyCode _cityReverseKey = KeyCode.I;

    private void Update()
    {
        if (Input.GetKeyDown(_playKey))
        {
            ResolvePlateController()?.PlayTransition(_provinceName);
        }
        else if (Input.GetKeyDown(_reverseKey))
        {
            ResolvePlateController()?.PlayTransitionReverse(_provinceName);
        }
        else if (Input.GetKeyDown(_cityTransitionKey))
        {
            ResolveCityController()?.PlayTransition();
        }
        else if (Input.GetKeyDown(_cityReverseKey))
        {
            ResolveCityController()?.PlayTransitionReverse();
        }
    }

    private PlateToGaodeMapTransitionController ResolvePlateController()
    {
        return _transitionController != null
            ? _transitionController
            : PlateToGaodeMapTransitionController.Instance;
    }

    private GaodeToCityTransitionController ResolveCityController()
    {
        return _cityTransitionController != null
            ? _cityTransitionController
            : GaodeToCityTransitionController.Instance;
    }
}
