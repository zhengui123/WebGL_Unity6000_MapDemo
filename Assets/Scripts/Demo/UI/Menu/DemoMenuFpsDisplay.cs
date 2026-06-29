using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo FPS 显示：开关在功能菜单顶部；数值在独立 Overlay 最前方，不被其它 Demo 面板遮挡。
/// </summary>
[DisallowMultipleComponent]
public class DemoMenuFpsDisplay : MonoBehaviour
{
    [SerializeField] private Text _fpsValueLabel;
    [SerializeField] private Toggle _showFpsToggle;
    [SerializeField] private float _updateInterval = 0.25f;

    private float _accumulatedTime;
    private int _frameCount;

    private void Awake()
    {
        if (_showFpsToggle != null)
        {
            _showFpsToggle.onValueChanged.AddListener(OnShowFpsToggleChanged);
            ApplyFpsVisible(_showFpsToggle.isOn);
        }
        else
        {
            ApplyFpsVisible(false);
        }

        EnsureFpsOverlayOnTop();
    }

    private void OnDestroy()
    {
        if (_showFpsToggle != null)
        {
            _showFpsToggle.onValueChanged.RemoveListener(OnShowFpsToggleChanged);
        }
    }

    private void LateUpdate()
    {
        EnsureFpsOverlayOnTop();
    }

    private void Update()
    {
        if (_fpsValueLabel == null || !_fpsValueLabel.gameObject.activeInHierarchy)
        {
            return;
        }

        _frameCount++;
        _accumulatedTime += Time.unscaledDeltaTime;
        if (_accumulatedTime < _updateInterval)
        {
            return;
        }

        float fps = _frameCount / _accumulatedTime;
        _frameCount = 0;
        _accumulatedTime = 0f;
        _fpsValueLabel.text = $"FPS: {Mathf.RoundToInt(fps)}";
    }

    private void OnShowFpsToggleChanged(bool show)
    {
        ApplyFpsVisible(show);
    }

    private void ApplyFpsVisible(bool show)
    {
        if (_fpsValueLabel == null)
        {
            return;
        }

        _fpsValueLabel.gameObject.SetActive(show);
        if (show)
        {
            _fpsValueLabel.text = "FPS: --";
            _frameCount = 0;
            _accumulatedTime = 0f;
        }
    }

    private void EnsureFpsOverlayOnTop()
    {
        if (_fpsValueLabel == null)
        {
            return;
        }

        Transform overlay = _fpsValueLabel.transform.parent;
        if (overlay != null)
        {
            overlay.SetAsLastSibling();
        }
    }
}
