using UnityEngine;

/// <summary>
/// Demo GameState UI 导航：在上层菜单与子功能面板之间切换显隐。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateUINavigator : MonoBehaviour
{
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _controlStateJumpPanel;
    [SerializeField] private GameObject _plateMapHighlightPanel;

    private void Awake()
    {
        ShowMenu();
    }

    public void ShowMenu()
    {
        SetPanelActive(_menuPanel, true);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, false);
    }

    public void ShowControlStateJumpPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, true);
        SetPanelActive(_plateMapHighlightPanel, false);
    }

    public void ShowPlateMapHighlightPanel()
    {
        SetPanelActive(_menuPanel, false);
        SetPanelActive(_controlStateJumpPanel, false);
        SetPanelActive(_plateMapHighlightPanel, true);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
