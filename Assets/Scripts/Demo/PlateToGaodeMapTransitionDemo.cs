using UnityEngine;

/// <summary>
/// T/Y/U/I：单阶段过渡；G/H：两阶段总控正播/倒播。
/// 省名为空时按当前聚焦板块的 provinceCode 自动配置二维地图。
/// </summary>
public class PlateToGaodeMapTransitionDemo : MonoBehaviour
{
    [SerializeField] private PlateToGaodeMapTransitionController _transitionController;
    [SerializeField] private GaodeToCityTransitionController _cityTransitionController;
    [SerializeField] private PlateToCityMapTransitionOrchestrator _orchestrator;

    [Tooltip("留空则用聚焦板块 provinceCode；也可填省名或 adcode 强制覆盖")]
    [SerializeField] private string _provinceNameOrCode = string.Empty;

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
            ResolvePlateController()?.PlayTransition(GetOverrideOrNull());
        }
        else if (Input.GetKeyDown(_reverseKey))
        {
            ResolvePlateController()?.PlayTransitionReverse(GetOverrideOrNull());
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
            ResolveOrchestrator()?.PlayFullTransition(GetOverrideOrNull());
        }
        else if (Input.GetKeyDown(_fullReverseKey))
        {
            ResolveOrchestrator()?.PlayFullTransitionReverse(GetOverrideOrNull());
        }
    }

    private string GetOverrideOrNull()
    {
        return string.IsNullOrWhiteSpace(_provinceNameOrCode) ? null : _provinceNameOrCode.Trim();
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
