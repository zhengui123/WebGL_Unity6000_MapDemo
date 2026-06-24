using UnityEngine;

/// <summary>
/// Demo GameState UI 导航：在上层菜单与子功能面板之间切换显隐。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateUINavigator : MonoBehaviour
{
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _controlStateJumpPanel;

    private void Awake()
    {
        ShowMenu();
    }

    public void ShowMenu()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(true);
        }

        if (_controlStateJumpPanel != null)
        {
            _controlStateJumpPanel.SetActive(false);
        }
    }

    public void ShowControlStateJumpPanel()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(false);
        }

        if (_controlStateJumpPanel != null)
        {
            _controlStateJumpPanel.SetActive(true);
        }
    }
}
