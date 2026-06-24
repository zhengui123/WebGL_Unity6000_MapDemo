using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo GameState 上层菜单：入口按钮绑定到 <see cref="DemoGameStateUINavigator"/>。
/// </summary>
[DisallowMultipleComponent]
public class DemoGameStateMenuUIDemo : MonoBehaviour
{
    [SerializeField] private DemoGameStateUINavigator _navigator;
    [SerializeField] private Button _controlStateJumpEntryButton;

    private void Awake()
    {
        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.AddListener(OnControlStateJumpEntryClicked);
        }
    }

    private void OnDestroy()
    {
        if (_controlStateJumpEntryButton != null)
        {
            _controlStateJumpEntryButton.onClick.RemoveListener(OnControlStateJumpEntryClicked);
        }
    }

    private void OnControlStateJumpEntryClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[DemoGameStateMenuUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowControlStateJumpPanel();
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
