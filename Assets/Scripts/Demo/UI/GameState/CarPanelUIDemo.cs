using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 车辆 UI Demo：调用 <see cref="MapApi.OpenCarUI"/> / <see cref="MapApi.CloseCarUI"/>。
/// </summary>
[DisallowMultipleComponent]
public class CarPanelUIDemo : MonoBehaviour
{
    public const string DefaultStart3DObjectName = "Group01";

    [SerializeField] private InputField _start3DObjectNameInput;
    [SerializeField] private Button _openCarUiButton;
    [SerializeField] private Button _closeCarUiButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private void Awake()
    {
        if (_openCarUiButton != null)
        {
            _openCarUiButton.onClick.AddListener(OnOpenCarUiButtonClicked);
        }

        if (_closeCarUiButton != null)
        {
            _closeCarUiButton.onClick.AddListener(OnCloseCarUiButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (_openCarUiButton != null)
        {
            _openCarUiButton.onClick.RemoveListener(OnOpenCarUiButtonClicked);
        }

        if (_closeCarUiButton != null)
        {
            _closeCarUiButton.onClick.RemoveListener(OnCloseCarUiButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void OnOpenCarUiButtonClicked()
    {
        string startName = _start3DObjectNameInput != null ? _start3DObjectNameInput.text : null;
        if (string.IsNullOrWhiteSpace(startName))
        {
            startName = DefaultStart3DObjectName;
        }

        MapApi.Instance.OpenCarUI(startName.Trim());
    }

    private void OnCloseCarUiButtonClicked()
    {
        MapApi.Instance.CloseCarUI();
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[CarPanelUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
