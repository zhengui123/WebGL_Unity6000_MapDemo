using UnityEngine;

/// <summary>
/// T/Y/U/I：单阶段过渡；G/H：两阶段总控正播/倒播。
/// </summary>
public class PlateToGaodeMapTransitionDemo : MonoBehaviour
{
    [SerializeField] private PlateToGaodeMapTransitionController _transitionController;
    [SerializeField] private GaodeToCityTransitionController _cityTransitionController;
    [SerializeField] private PlateToCityMapTransitionOrchestrator _orchestrator;
    [SerializeField] private string _provinceName = "山东";
    [SerializeField] private KeyCode _playKey = KeyCode.T;
    [SerializeField] private KeyCode _reverseKey = KeyCode.Y;
    [SerializeField] private KeyCode _cityTransitionKey = KeyCode.U;
    [SerializeField] private KeyCode _cityReverseKey = KeyCode.I;
    [SerializeField] private KeyCode _fullPlayKey = KeyCode.G;
    [SerializeField] private KeyCode _fullReverseKey = KeyCode.H;

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
        else if (Input.GetKeyDown(_fullPlayKey))
        {
            ResolveOrchestrator()?.PlayFullTransition(_provinceName);
        }
        else if (Input.GetKeyDown(_fullReverseKey))
        {
            ResolveOrchestrator()?.PlayFullTransitionReverse(_provinceName);
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

    private PlateToCityMapTransitionOrchestrator ResolveOrchestrator()
    {
        return _orchestrator != null
            ? _orchestrator
            : PlateToCityMapTransitionOrchestrator.Instance;
    }
}
