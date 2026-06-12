using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 键盘测试车辆 → 零件过渡：正播按名称列表轮播，倒播还原上次正播的零件。
/// 默认 P 正播、O 倒播。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionDemo : MonoBehaviour
{
    [SerializeField] private VehicleToPartTransitionController _controller;

    [Header("零件名称列表（正播每次取下一个；留空则正播不传名）")]
    [SerializeField] private List<string> _partNames = new List<string>();

    [Header("按键")]
    [SerializeField] private KeyCode _playKey = KeyCode.P;
    [SerializeField] private KeyCode _reverseKey = KeyCode.O;

    private int _nextPartNameIndex;

    private void Update()
    {
        VehicleToPartTransitionController controller = ResolveController();
        if (controller == null)
        {
            return;
        }

        if (Input.GetKeyDown(_playKey))
        {
            string partName = GetNextPartNameForForward();
            bool started = string.IsNullOrEmpty(partName)
                ? controller.PlayTransition()
                : controller.PlayTransition(partName);

            if (started)
            {
                Debug.Log($"[VehicleToPartDemo] 正播：{(string.IsNullOrEmpty(partName) ? "列表默认第一项" : partName)}");
            }
        }
        else if (Input.GetKeyDown(_reverseKey))
        {
            bool started = controller.PlayTransitionReverse();
            if (started)
            {
                Debug.Log($"[VehicleToPartDemo] 倒播：{controller.LastPartName}");
            }
        }
    }

    /// <summary>按列表顺序返回下一个零件名；列表为空时返回 null。</summary>
    private string GetNextPartNameForForward()
    {
        if (_partNames == null || _partNames.Count == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < _partNames.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(_partNames[i]))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        for (int attempt = 0; attempt < _partNames.Count; attempt++)
        {
            int index = (_nextPartNameIndex + attempt) % _partNames.Count;
            string candidate = _partNames[index];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            _nextPartNameIndex = (index + 1) % _partNames.Count;
            return candidate.Trim();
        }

        return null;
    }

    private VehicleToPartTransitionController ResolveController()
    {
        if (_controller != null)
        {
            return _controller;
        }

        return VehicleToPartTransitionController.Instance;
    }
}
